﻿namespace IronPigeon.Relay.Tests.Controllers {
	using System;
	using System.Web.Mvc;

	using IronPigeon.Relay.Controllers;

	using NUnit.Framework;

	[TestFixture]
	public class InboxControllerTest {
		[Test]
		public void List() {
			var controller = new InboxController();
			string thumbprint = null;
			ActionResult result = controller.List(thumbprint).Result;
			Assert.That(result, Is.Not.Null);
		}

		[Test, Ignore("Not yet implemented")]
		public void PostNotification() {

		}

		[Test, Ignore("Not yet implemented")]
		public void GetNotification() {

		}

		[Test, Ignore("Not yet implemented")]
		public void DeleteNotification() {

		}
	}
}