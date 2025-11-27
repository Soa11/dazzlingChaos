// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System.Net;
    using Result = UnityEngine.Networking.UnityWebRequest.Result;
    using NUnit.Framework;
    using UnityEngine;

    /// <summary>
    /// Edit mode unit tests for <see cref="RequestException"/>.
    /// </summary>
    public class RequestExceptionTests
    {
        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Succeeds_For_ConnectionError_Result(string response, HttpStatusCode responseCode, string error)
        {
            using var logger = Log.Log.GetLogger<RequestExceptionTests>().NoWatermark();

            var success = RequestException.TryParse(Result.ConnectionError, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.HttpStatusCode, Is.EqualTo(responseCode));
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Succeeds_For_DataProcessingError_Result(string response, HttpStatusCode responseCode, string error)
        {
            using var logger = Log.Log.GetLogger<RequestExceptionTests>().NoWatermark();

            var success = RequestException.TryParse(Result.DataProcessingError, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.HttpStatusCode, Is.EqualTo(responseCode));
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Succeeds_For_ProtocolError_Result(string response, HttpStatusCode responseCode, string error)
        {
            using var logger = Log.Log.GetLogger<RequestExceptionTests>().NoWatermark();

            var success = RequestException.TryParse(Result.ProtocolError, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.HttpStatusCode, Is.EqualTo(responseCode));
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Fails_For_Sucess_Result(string response, HttpStatusCode responseCode, string error)
        {
            using var logger = Log.Log.GetLogger<RequestExceptionTests>().NoWatermark();

            var success = RequestException.TryParse(Result.Success, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.False);
            Assert.That(exception, Is.Null);
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Fails_For_InProgress_Result(string response, HttpStatusCode responseCode, string error)
        {
            using var logger = Log.Log.GetLogger<RequestExceptionTests>().NoWatermark();

            var success = RequestException.TryParse(Result.InProgress, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.False);
            Assert.That(exception, Is.Null);
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Fails_For_Unnamed_Result(string response, HttpStatusCode responseCode, string error)
        {
            using var logger = Log.Log.GetLogger<RequestExceptionTests>().NoWatermark();

            var success = RequestException.TryParse((Result)(-100), response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.False);
            Assert.That(exception, Is.Null);
        }
    }
}
