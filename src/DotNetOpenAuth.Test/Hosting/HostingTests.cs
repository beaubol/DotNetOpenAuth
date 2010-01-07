﻿//-----------------------------------------------------------------------
// <copyright file="HostingTests.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.Test.Hosting {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Text;
	using DotNetOpenAuth.Test.OpenId;
	using NUnit.Framework;

	[TestFixture]
	public class HostingTests : TestBase {
		[TestCase]
		public void AspHostBasicTest() {
			try {
				using (AspNetHost host = AspNetHost.CreateHost(TestWebDirectory)) {
					HttpWebRequest request = (HttpWebRequest)WebRequest.Create(host.BaseUri);
					using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) {
						Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
						using (StreamReader sr = new StreamReader(response.GetResponseStream())) {
							string content = sr.ReadToEnd();
							StringAssert.Contains(content, "Test home page");
						}
					}
				}
			} catch (FileNotFoundException ex) {
				Assert.Inconclusive("Unable to execute hosted ASP.NET tests because {0} could not be found.  {1}", ex.FileName, ex.FusionLog);
			}
		}
	}
}
