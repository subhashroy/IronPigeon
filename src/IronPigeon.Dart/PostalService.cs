﻿namespace IronPigeon.Dart {
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Composition;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	using Validation;
	using ReadOnlyListOfMessage = System.Collections.Generic.IReadOnlyList<Message>;
	using ReadOnlyListOfPayload = System.Collections.Generic.IReadOnlyList<Payload>;

	/// <summary>
	/// An email sending and receiving service.
	/// </summary>
	[Export]
	[Shared]
	public class PostalService {
		/// <summary>
		/// Gets or sets the channel used to send and receive messages.
		/// </summary>
		[Import]
		public Channel Channel { get; set; }

		/// <summary>
		/// Sends the specified dart to the recipients specified in the message.
		/// </summary>
		/// <param name="message">The dart to send.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The asynchronous result.</returns>
		public virtual Task PostAsync(Message message, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(message, "message");

			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			writer.SerializeDataContract(message);
			writer.Flush();
			ms.Position = 0;

			var payload = new Payload(ms.ToArray(), Message.ContentType);
			var allRecipients = new List<Endpoint>(message.Recipients);
			if (message.CarbonCopyRecipients != null) {
				allRecipients.AddRange(message.CarbonCopyRecipients);
			}

			var readOnlyRecipients = new ReadOnlyCollection<Endpoint>(allRecipients);
			return this.Channel.PostAsync(payload, readOnlyRecipients, message.ExpirationUtc, cancellationToken);
		}

		/// <summary>
		/// Retrieves all messages waiting for pickup at our endpoint.
		/// </summary>
		/// <param name="longPoll">if set to <c>true</c> [long poll].</param>
		/// <param name="progress">A callback to invoke for each downloaded message as it arrives.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// A task whose result is the complete list of received messages.
		/// </returns>
		public virtual async Task<ReadOnlyListOfMessage> ReceiveAsync(bool longPoll = false, IProgress<Message> progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			var messages = new List<Message>();
			ReadOnlyListOfPayload payloads = null;
			var payloadProgress = new ProgressWithCompletion<Payload>(
				async payload => {
					var message = FromPayload(payload);
					if (message != null) {
						// Sterilize the message of its claimed endpoint's claimed identifiers,
						// so that only verifiable identifiers are passed onto our application.
						var verifiedIdentifiers = await this.Channel.GetVerifiableIdentifiersAsync(message.Author, cancellationToken);
						message.Author.AuthorizedIdentifiers = verifiedIdentifiers.ToArray();

						lock (messages) {
							messages.Add(message);
						}

						if (progress != null) {
							progress.Report(message);
						}
					}
				});

			payloads = await this.Channel.ReceiveAsync(longPoll, payloadProgress, cancellationToken);

			// Ensure that we've receives the asynchronous progress notifications for all the payloads
			// so we don't return a partial result.
			await payloadProgress.WaitAsync();

			return messages;
		}

		/// <summary>
		/// Deletes the specified message from its online inbox so it won't be retrieved again.
		/// </summary>
		/// <param name="message">The message to delete from its online location.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The asynchronous result.</returns>
		public virtual Task DeleteAsync(Message message, CancellationToken cancellationToken = default(CancellationToken)) {
			Requires.NotNull(message, "message");
			Requires.Argument(message.OriginatingPayload != null, "message", "Original message payload no longer available.");
			return this.Channel.DeleteInboxItemAsync(message.OriginatingPayload, cancellationToken);
		}

		/// <summary>
		/// Extracts a message from its serialized payload wrapper.
		/// </summary>
		/// <param name="payload">The payload to extract the message from.</param>
		/// <returns>The extracted message.</returns>
		private static Message FromPayload(Payload payload) {
			Requires.NotNull(payload, "payload");

			if (payload.ContentType != Message.ContentType) {
				return null;
			}

			using (var reader = new BinaryReader(new MemoryStream(payload.Content))) {
				var message = reader.DeserializeDataContract<Message>();
				message.OriginatingPayload = payload;
				return message;
			}
		}
	}
}
