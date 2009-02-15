﻿//-----------------------------------------------------------------------
// <copyright file="StandardWebRequestHandler.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.Messaging {
	using System;
	using System.IO;
	using System.Net;
	using System.Net.Sockets;
	using System.Reflection;
	using DotNetOpenAuth.Messaging;

	/// <summary>
	/// The default handler for transmitting <see cref="HttpWebRequest"/> instances
	/// and returning the responses.
	/// </summary>
	internal class StandardWebRequestHandler : IDirectWebRequestHandler {
		/// <summary>
		/// The set of options this web request handler supports.
		/// </summary>
		private const DirectWebRequestOptions SupportedOptions = DirectWebRequestOptions.AcceptAllHttpResponses;

		/// <summary>
		/// The value to use for the User-Agent HTTP header.
		/// </summary>
		private static string userAgentValue = Assembly.GetExecutingAssembly().GetName().Name + "/" + Assembly.GetExecutingAssembly().GetName().Version;

		#region IWebRequestHandler Members

		/// <summary>
		/// Determines whether this instance can support the specified options.
		/// </summary>
		/// <param name="options">The set of options that might be given in a subsequent web request.</param>
		/// <returns>
		/// 	<c>true</c> if this instance can support the specified options; otherwise, <c>false</c>.
		/// </returns>
		public bool CanSupport(DirectWebRequestOptions options) {
			return (options & ~SupportedOptions) == 0;
		}

		/// <summary>
		/// Prepares an <see cref="HttpWebRequest"/> that contains an POST entity for sending the entity.
		/// </summary>
		/// <param name="request">The <see cref="HttpWebRequest"/> that should contain the entity.</param>
		/// <returns>
		/// The writer the caller should write out the entity data to.
		/// </returns>
		/// <exception cref="ProtocolException">Thrown for any network error.</exception>
		/// <remarks>
		/// 	<para>The caller should have set the <see cref="HttpWebRequest.ContentLength"/>
		/// and any other appropriate properties <i>before</i> calling this method.</para>
		/// 	<para>Implementations should catch <see cref="WebException"/> and wrap it in a
		/// <see cref="ProtocolException"/> to abstract away the transport and provide
		/// a single exception type for hosts to catch.</para>
		/// </remarks>
		public Stream GetRequestStream(HttpWebRequest request) {
			return this.GetRequestStream(request, DirectWebRequestOptions.None);
		}

		/// <summary>
		/// Prepares an <see cref="HttpWebRequest"/> that contains an POST entity for sending the entity.
		/// </summary>
		/// <param name="request">The <see cref="HttpWebRequest"/> that should contain the entity.</param>
		/// <param name="options">The options to apply to this web request.</param>
		/// <returns>
		/// The writer the caller should write out the entity data to.
		/// </returns>
		/// <exception cref="ProtocolException">Thrown for any network error.</exception>
		/// <remarks>
		/// 	<para>The caller should have set the <see cref="HttpWebRequest.ContentLength"/>
		/// and any other appropriate properties <i>before</i> calling this method.</para>
		/// 	<para>Implementations should catch <see cref="WebException"/> and wrap it in a
		/// <see cref="ProtocolException"/> to abstract away the transport and provide
		/// a single exception type for hosts to catch.</para>
		/// </remarks>
		public Stream GetRequestStream(HttpWebRequest request, DirectWebRequestOptions options) {
			ErrorUtilities.VerifyArgumentNotNull(request, "request");
			ErrorUtilities.VerifySupported(this.CanSupport(options), MessagingStrings.DirectWebRequestOptionsNotSupported, options, this.GetType().Name);

			return GetRequestStreamCore(request, options);
		}

		/// <summary>
		/// Processes an <see cref="HttpWebRequest"/> and converts the
		/// <see cref="HttpWebResponse"/> to a <see cref="DirectWebResponse"/> instance.
		/// </summary>
		/// <param name="request">The <see cref="HttpWebRequest"/> to handle.</param>
		/// <returns>
		/// An instance of <see cref="DirectWebResponse"/> describing the response.
		/// </returns>
		/// <exception cref="ProtocolException">Thrown for any network error.</exception>
		/// <remarks>
		/// Implementations should catch <see cref="WebException"/> and wrap it in a
		/// <see cref="ProtocolException"/> to abstract away the transport and provide
		/// a single exception type for hosts to catch.
		/// </remarks>
		public DirectWebResponse GetResponse(HttpWebRequest request) {
			return this.GetResponse(request, DirectWebRequestOptions.None);
		}

		/// <summary>
		/// Processes an <see cref="HttpWebRequest"/> and converts the
		/// <see cref="HttpWebResponse"/> to a <see cref="DirectWebResponse"/> instance.
		/// </summary>
		/// <param name="request">The <see cref="HttpWebRequest"/> to handle.</param>
		/// <param name="options">The options to apply to this web request.</param>
		/// <returns>
		/// An instance of <see cref="DirectWebResponse"/> describing the response.
		/// </returns>
		/// <exception cref="ProtocolException">Thrown for any network error.</exception>
		/// <remarks>
		/// Implementations should catch <see cref="WebException"/> and wrap it in a
		/// <see cref="ProtocolException"/> to abstract away the transport and provide
		/// a single exception type for hosts to catch.
		/// </remarks>
		public DirectWebResponse GetResponse(HttpWebRequest request, DirectWebRequestOptions options) {
			ErrorUtilities.VerifyArgumentNotNull(request, "request");
			ErrorUtilities.VerifySupported(this.CanSupport(options), MessagingStrings.DirectWebRequestOptionsNotSupported, options, this.GetType().Name);

			// This request MAY have already been prepared by GetRequestStream, but
			// we have no guarantee, so do it just to be safe.
			PrepareRequest(request, false);

			try {
				Logger.DebugFormat("HTTP {0} {1}", request.Method, request.RequestUri);
				HttpWebResponse response = (HttpWebResponse)request.GetResponse();
				return new NetworkDirectWebResponse(request.RequestUri, response);
			} catch (WebException ex) {
				if ((options & DirectWebRequestOptions.AcceptAllHttpResponses) != 0 && ex.Response != null) {
					var response = (HttpWebResponse)ex.Response;
					Logger.InfoFormat("The HTTP error code {0} {1} is being accepted because the {2} flag is set.", (int)response.StatusCode, response.StatusCode, DirectWebRequestOptions.AcceptAllHttpResponses);
					return new NetworkDirectWebResponse(request.RequestUri, response);
				}

				if (Logger.IsErrorEnabled) {
					if (ex.Response != null) {
						using (var reader = new StreamReader(ex.Response.GetResponseStream())) {
							Logger.ErrorFormat("WebException from {0}: {1}{2}", ex.Response.ResponseUri, Environment.NewLine, reader.ReadToEnd());
						}
					} else {
						Logger.ErrorFormat("WebException {1} from {0}, no response available.", request.RequestUri, ex.Status);
					}
				}

				throw ErrorUtilities.Wrap(ex, MessagingStrings.ErrorInRequestReplyMessage);
			}
		}

		#endregion

		/// <summary>
		/// Initiates a POST request and prepares for sending data.
		/// </summary>
		/// <param name="request">The HTTP request with information about the remote party to contact.</param>
		/// <param name="options">The options to apply to this specific web request.</param>
		/// <returns>
		/// The stream where the POST entity can be written.
		/// </returns>
		private static Stream GetRequestStreamCore(HttpWebRequest request, DirectWebRequestOptions options) {
			PrepareRequest(request, true);

			try {
				return request.GetRequestStream();
			} catch (SocketException ex) {
				throw ErrorUtilities.Wrap(ex, MessagingStrings.WebRequestFailed, request.RequestUri);
			} catch (WebException ex) {
				using (HttpWebResponse response = (HttpWebResponse)ex.Response) {
					if (response != null && response.StatusCode == HttpStatusCode.ExpectationFailed &&
						request.ServicePoint.Expect100Continue) {
						// Some OpenID servers doesn't understand the Expect header and send 417 error back.
						// If this server just failed from that, we're trying again without sending the
						// "Expect: 100-Continue" HTTP header. (see Google Code Issue 72)
						// We don't just set Expect100Continue = !avoidSendingExpect100Continue
						// so that future requests don't reset this and have to try twice as well.
						// We don't want to blindly set all ServicePoints to not use the Expect header
						// as that would be a security hole allowing any visitor to a web site change
						// the web site's global behavior when calling that host.
						request = request.Clone();
						request.ServicePoint.Expect100Continue = false; // TODO: investigate that CAS may throw here, and we can use request.Expect instead.
						// request.Expect = "";  // alternative to ServicePoint if we don't have permission to set that, but be sure to change the if clause above if we use this.
						return GetRequestStreamCore(request, options);
					} else {
						throw ErrorUtilities.Wrap(ex, MessagingStrings.WebRequestFailed, request.RequestUri);
					}
				}
			}
		}

		/// <summary>
		/// Prepares an HTTP request.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <param name="preparingPost"><c>true</c> if this is a POST request whose headers have not yet been sent out; <c>false</c> otherwise.</param>
		private static void PrepareRequest(HttpWebRequest request, bool preparingPost) {
			ErrorUtilities.VerifyArgumentNotNull(request, "request");

			// Be careful to not try to change the HTTP headers that have already gone out.
			if (preparingPost || request.Method == "GET") {
				// Some sites, such as Technorati, return 403 Forbidden on identity
				// pages unless a User-Agent header is included.
				if (string.IsNullOrEmpty(request.UserAgent)) {
					request.UserAgent = userAgentValue;
				}
			}
		}
	}
}