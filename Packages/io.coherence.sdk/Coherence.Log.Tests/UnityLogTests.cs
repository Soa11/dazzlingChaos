// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Coherence.Tests;
    using NUnit.Framework;
    using NUnit.Framework.Constraints;
    using UnityEngine;
    using UnityEngine.TestTools;
    using Utils;
    using Logger = Logger;

    public class LogClassA { }
    public class LogClassB { }

    public class ExampleBehaviour : MonoBehaviour { }

    public class UnityLogTests : CoherenceTest
    {
        private string previousLogSettingsJson;

        [Test]
        public void UnityContextArgs()
        {
            var exampleGO = new GameObject();
            exampleGO.name = "example";
            var behaviour = exampleGO.AddComponent<ExampleBehaviour>();

            var parentLogger = Log.GetLogger<LogClassA>().WithArgs(("context", behaviour), ("project_id", 1234));
            var childLogger = parentLogger.With<LogClassB>().WithArgs(("child_info", "ABCD"));

            parentLogger.Debug("PARENT", ("parent_arg", "HIJK"));
            childLogger.Debug("CHILD", ("child_arg", 9876));
            Debug.Log("SAMPLE", behaviour);
        }

        [Test]
        public void LogPassesArgsWhenFiltered()
        {
            var parentLogger = Log.GetLogger<LogClassA>().WithArgs(("a", 1), ("b", 2));

            foreach (var target in parentLogger.GetLogTargets())
            {
                target.Level = LogLevel.Error;
            }

            var childLogger = parentLogger.With<LogClassB>().WithArgs(("c", 3), ("d", 4));

            Logger.LogDelegate onLog = (level, filtered, log, source, args) =>
            {
                Assert.That(filtered, "Filtered");
                Assert.That(args.Length, level == LogLevel.Debug
                    ? Is.EqualTo(3)
                    : Is.EqualTo(5), "Arg count");
            };

            Logger.OnLog += onLog;
            try
            {
                parentLogger.Debug("x", ("x", 10));
                childLogger.Trace("y", ("y", 20));
            }
            finally
            {
                Logger.OnLog -= onLog;
            }
        }

        [TestCase(LogLevel.Trace, "")]
        [TestCase(LogLevel.Trace, "[coherence]")]
        [TestCase(LogLevel.Debug, "")]
        [TestCase(LogLevel.Debug, "[coherence]")]
        [TestCase(LogLevel.Info, "")]
        [TestCase(LogLevel.Info, "[coherence]")]
        [TestCase(LogLevel.Warning, "")]
        [TestCase(LogLevel.Warning, "[coherence]")]
        [TestCase(LogLevel.Error, "")]
        [TestCase(LogLevel.Error, "[coherence]")]
        public void Respects_Watermark_Settings(LogLevel logLevel, string watermark)
        {
            using var logger = GetLogger<LogClassA>(x => x.Watermark = watermark);
            const string message = "Test message.";
            var actualOutput = "";
            Application.logMessageReceived += (condition, _, _) => actualOutput = condition;
            if (logLevel is LogLevel.Warning)
            {
                LogAssert.Expect(LogType.Warning, new Regex(".*"));
            }
            else if (logLevel is LogLevel.Error)
            {
                LogAssert.Expect(LogType.Error, new Regex(".*"));
            }

            LogMessage(logger, message, logLevel);

            var expectedOutput = string.IsNullOrEmpty(watermark) ? message : $"{watermark} {message}";
            IResolveConstraint expression = logLevel is LogLevel.Warning or LogLevel.Error ? Does.StartWith(expectedOutput) : Is.EqualTo(expectedOutput);
            Assert.That(actualOutput, expression);
        }

        [TestCase(LogLevel.Trace, false)]
        [TestCase(LogLevel.Trace, true)]
        [TestCase(LogLevel.Debug, false)]
        [TestCase(LogLevel.Debug, true)]
        [TestCase(LogLevel.Info, false)]
        [TestCase(LogLevel.Info, true)]
        [TestCase(LogLevel.Warning, false)]
        [TestCase(LogLevel.Warning, true)]
        [TestCase(LogLevel.Error, false)]
        [TestCase(LogLevel.Error, true)]
        public void Respects_AddTimestamp_Settings(LogLevel logLevel, bool addTimestamp)
        {
            using var logger = GetLogger<LogClassA>(x => x.AddTimestamp = addTimestamp);
            const string message = "Test message.";
            var actualOutput = "";
            Application.logMessageReceived += (condition, _, _) => actualOutput = condition;
            if (logLevel is LogLevel.Warning)
            {
                LogAssert.Expect(LogType.Warning, new Regex(".*"));
            }
            else if (logLevel is LogLevel.Error)
            {
                LogAssert.Expect(LogType.Error, new Regex(".*"));
            }

            LogMessage(logger, message, logLevel);

            var expectedOutput = logLevel switch
            {
                LogLevel.Warning or LogLevel.Error => !addTimestamp ? $"{message}.*" : $@"..:..:..\.... {message}.*",
                _ => !addTimestamp ? message : $@"..:..:..\.... {message}"
            };
            Assert.That(actualOutput, Does.Match(expectedOutput));
        }

        [TestCase(LogLevel.Trace, false)]
        [TestCase(LogLevel.Trace, true)]
        [TestCase(LogLevel.Debug, false)]
        [TestCase(LogLevel.Debug, true)]
        [TestCase(LogLevel.Info, false)]
        [TestCase(LogLevel.Info, true)]
        [TestCase(LogLevel.Warning, false)]
        [TestCase(LogLevel.Warning, true)]
        [TestCase(LogLevel.Error, false)]
        [TestCase(LogLevel.Error, true)]
        public void Respects_AddSourceType_Settings(LogLevel logLevel, bool addSourceType)
        {
            using var logger = GetLogger<LogClassA>(x => x.AddSourceType = addSourceType);
            const string message = "Test message.";
            var actualOutput = "";
            Application.logMessageReceived += (condition, _, _) => actualOutput = condition;
            if (logLevel is LogLevel.Warning)
            {
                LogAssert.Expect(LogType.Warning, new Regex(".*"));
            }
            else if (logLevel is LogLevel.Error)
            {
                LogAssert.Expect(LogType.Error, new Regex(".*"));
            }

            LogMessage(logger, message, logLevel);

            var expectedOutput = !addSourceType ? message : $"{nameof(LogClassA)}: {message}";
            IResolveConstraint expression = logLevel is LogLevel.Warning or LogLevel.Error ? Does.StartWith(expectedOutput) : Is.EqualTo(expectedOutput);
            Assert.That(actualOutput, expression);
        }

        [Test]
        public void UnityValueArgs()
        {
            var testArgs = new (string, object)[]
            {
                ("arg0", 123),
                ("arg1", "abc"),
                ("arg2", 0.1f),
                ("arg3", 6969),
                ("arg4", "hello"),
                ("arg5", new GameObject()),
                ("arg6", null),
            };

            var parentLogger = Log.GetLogger<LogClassA>().WithArgs(testArgs[0], testArgs[1]);
            var childLogger = parentLogger.With<LogClassB>().WithArgs(testArgs[2], testArgs[3]);

            void checkLogs(LogLevel level, bool filtered, string log, Type source, ICollection<(string, object)> args)
            {
                if (source == typeof(LogClassA))
                {
                    Assert.That(args.Count, Is.EqualTo(3));
                }
                else
                {
                    Assert.That(args.Count, Is.EqualTo(6));
                }
            };

            Logger.OnLog += checkLogs;
            try
            {
                parentLogger.Debug("PARENT", testArgs[4]);
                childLogger.Debug("CHILD", testArgs[5], testArgs[6]);
            }
            finally
            {
                Logger.OnLog -= checkLogs;
            }
        }

        public override void SetUp()
        {
            base.SetUp();
            var runtimeSettings = RuntimeSettings.Instance;
            var settings = runtimeSettings.LogSettings;
            previousLogSettingsJson = CoherenceJson.SerializeObject(settings);

            settings.LogLevel = LogLevel.Trace;
            settings.Watermark = "";
            settings.AddSourceType = false;
            settings.AddTimestamp = false;
            settings.SourceFilters = "";
            Log.SetSettings(settings);
        }

        public override void TearDown()
        {
            var settings = CoherenceJson.DeserializeObject<Settings>(previousLogSettingsJson);
            RuntimeSettings.Instance.logSettings = settings;
            Log.SetSettings(settings);
            base.TearDown();
        }

        private Logger GetLogger<T>(Action<Settings> configure = null)
        {
            var settings = RuntimeSettings.Instance.logSettings;
            configure?.Invoke(settings);
            settings.Apply();
            return Log.GetLogger<T>();
        }

        private static void LogMessage(Logger logger, string message, LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
#if COHERENCE_LOG_TRACE
                    logger.Trace(message);
#else
                    Assert.Ignore("Skipping test because COHERENCE_LOG_TRACE preprocessor directive is not defined.");
#endif
                    break;
                case LogLevel.Debug:
#if COHERENCE_LOG_DEBUG
                    logger.Debug(message);
#else
                    Assert.Ignore("Skipping test because COHERENCE_LOG_DEBUG preprocessor directive is not defined.");
#endif
                    break;
                case LogLevel.Info:
#if !COHERENCE_DISABLE_LOG_INFO
                    logger.Info(message);
#else
                    Assert.Ignore("Skipping test because COHERENCE_DISABLE_LOG_INFO preprocessor directive is defined.");
#endif
                    break;
                case LogLevel.Warning:
#if !COHERENCE_DISABLE_LOG_WARNING
                    logger.Warning(default, message);
#else
                    Assert.Ignore("Skipping test because COHERENCE_DISABLE_LOG_WARNING preprocessor directive is defined.");
#endif
                    break;
                case LogLevel.Error:
#if !COHERENCE_DISABLE_LOG_ERROR
                    logger.Error(default, message);
#else
                    Assert.Ignore("Skipping test because COHERENCE_DISABLE_LOG_ERROR preprocessor directive is defined.");
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }
    }
}
