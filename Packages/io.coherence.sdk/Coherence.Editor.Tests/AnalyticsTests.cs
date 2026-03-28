// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using Debug = System.Diagnostics.Debug;

    /// <summary>
    /// Tests for <see cref="Coherence.Editor.Analytics"/>.
    /// </summary>
    public sealed class AnalyticsTests
    {
        private const string exceptionMessage = "Test exception message";
        private static string sharedLogStackTrace;
        private static bool storedReportAnalytics;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            storedReportAnalytics = ProjectSettings.instance.reportAnalytics;
            ProjectSettings.instance.reportAnalytics = false;
        }

        [SetUp]
        public void SetUp()
        {
            sharedLogStackTrace = null;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            ProjectSettings.instance.reportAnalytics = storedReportAnalytics;
        }

        private static void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            sharedLogStackTrace = stackTrace;
        }

        [Test]
        public void StackTrace_FromUnityConsoleStackTrace_HasFrames()
        {
            // Unity throws in extra stuff like "Exception: " to the start, so we just catch it all.
            LogAssert.Expect(LogType.Exception, new Regex(".*" + Regex.Escape(exceptionMessage) + ".*"));
            UnityEngine.Debug.LogException(new Exception(exceptionMessage));

            var stackTrace = Analytics.ExceptionData.StackTrace.FromUnityConsoleStackTrace(sharedLogStackTrace);
            Assert.IsTrue(stackTrace.frames.Length > 0);
        }

        [Test]
        [TestCase("")]
        [TestCase("C:/path/to/file?.cs")]
        [TestCase("C:\\random:")]
        [TestCase("/foo/bar<.cs")]
        [TestCase("/space in path.cs")]
        [TestCase("/unicode/路径.cs")]
        [TestCase("/space-after-ext.cs ")]
        [TestCase("lorem ipsum 42")]
        [TestCase("&^#$#")]
        public void StackTrace_FromUnityConsoleStackTrace_InvalidPaths(string path)
        {
            var stackTraceString = $"Coherence.Foo.Bar:Baz () (at {path}:164)";
            // construct the stack trace to see if it throws
            _ = Analytics.ExceptionData.StackTrace.FromUnityConsoleStackTrace(stackTraceString);
        }

        [Test]
        public void StackTrace_FromSystemStackTrace_HasFrames()
        {
            var systemStackTrace = new StackTrace(true);
            var stackTrace = Analytics.ExceptionData.StackTrace.FromSystemStackTrace(systemStackTrace);
            Assert.IsTrue(stackTrace.frames.Length > 0);
        }

        [Test]
        public void StackTrace_FromMonoStackTrace_HasFrames()
        {
            string stackTraceString;

            try
            {
                throw new Exception(exceptionMessage);
            }
            catch (Exception e)
            {
                stackTraceString = e.StackTrace;
            }

            var stackTrace = Analytics.ExceptionData.StackTrace.FromMonoStackTrace(stackTraceString);
            Assert.IsTrue(stackTrace.frames.Length > 0);
        }
    }
}
