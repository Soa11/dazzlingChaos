// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Log;
    using UnityEditor;
    using UnityEngine;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Utils;
    using Portal;

    [InitializeOnLoad]
    internal static partial class Analytics
    {
        private const string endpoint = "https://xp.coherence.io/capture/";
        private const string projectAPIKey = "phc_OWjEpeOs7PXoRMndJC5cdA7yNf9flI5bDAecZXM5mlD";
        private const string distinctIDPrefKey = "Coherence.Xp.DistinctID";
        private static readonly char[] newLineMarkers = { '\r', '\n', };
        private static HashSet<string> sentExceptionsByFingerprint = new();

        private static readonly SynchronizationContext unitySyncContext;
        private static LazyLogger logger = Log.GetLazyLogger(typeof(Analytics));

        static Analytics()
        {
            var userID = UserID;
            var distinctID = DistinctID;

            if (!string.IsNullOrEmpty(userID) && userID != distinctID)
            {
                DistinctID = userID;
            }

            unitySyncContext = SynchronizationContext.Current;

            UnityLogger.OnLogWarningEventExt += CaptureWarning;
            UnityLogger.OnLogErrorEventExt += CaptureError;
            Application.logMessageReceivedThreaded += TryCaptureUnityConsoleLog;
        }

        public static string DistinctID
        {
            get
            {
                var uid = EditorPrefs.GetString(distinctIDPrefKey, null);
                if (!string.IsNullOrEmpty(uid))
                {
                    return uid;
                }
                else
                {
                    uid = Guid.NewGuid().ToString();
                    DistinctID = uid;
                    return uid;
                }
            }
            internal set => EditorPrefs.SetString(distinctIDPrefKey, value);
        }

        private static string UserID
        {
            get => ProjectSettings.instance.UserID;
        }

        private static string DeviceID
        {
            get => SystemInfo.deviceUniqueIdentifier.ToLower();
        }

        private static readonly string SessionID = Guid.NewGuid().ToString();

        private static string OS
        {
            get
            {
#if UNITY_EDITOR_OSX
                return "Mac OS X";
#elif UNITY_EDITOR_WIN
                return "Windows";
#else
                return "Linux";
#endif
            }
        }

        private static string EventSource
        {
            get
            {
                return Application.isBatchMode ? "unity-headless" : "unity";
            }
        }

        private static RuntimeSettings RuntimeSettings
        {
            get => ProjectSettings.instance.RuntimeSettings;
        }

        private static bool OptOut
        {
            get => !ProjectSettings.instance.reportAnalytics;
        }

        public static void Identify(string userID, string email)
        {
            var oldDistinctID = DistinctID;
            var newDistinctID = userID;

            if (string.IsNullOrEmpty(newDistinctID) || newDistinctID == oldDistinctID)
            {
                return;
            }

            Guid g;
            var anonymous = Guid.TryParse(oldDistinctID, out g);

            if (anonymous)
            {
                DistinctID = newDistinctID;
                Capture(new Event<IdentityProperties>(
                    "$identify",
                    new IdentityProperties{
                        AnonDistinctID = oldDistinctID,
                        UserProperties = {
                            InternalUser = email.EndsWith("@coherence.io")
                        }
                    }
                ));
            }
            else
            {
                DistinctID = newDistinctID;
            }
        }

        public static void OrgIdentify(Organization org)
        {
            if (org != null)
            {
                Capture(new Event<OrgIdentityProperties>(
                    "$groupidentify",
                    new OrgIdentityProperties(org.id, org.slug, org.name)
                ));
            }
        }

        public static void ResetIdentity()
        {
            DistinctID = Guid.NewGuid().ToString();
        }

        public static void Capture(string eventName)
        {
            if (OptOut)
            {
                return;
            }

            var evt = new Event<BaseProperties>(eventName, new BaseProperties());
            Capture(evt);
        }

        public static void Capture(string eventName, params (string key, JToken value)[] properties)
        {
            if (OptOut)
            {
                return;
            }

            var eventProperties = new GenericProperties();
            foreach (var prop in properties)
            {
                eventProperties.Properties[prop.key] = prop.value;
            }
            var evt = new Event<GenericProperties>(eventName, eventProperties);
            Capture(evt);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RuntimeInitializeOnLoad()
        {
            // Reset static state, in case domain reload is disabled.
            sentExceptionsByFingerprint.Clear();
        }

        private static void CaptureIssue(string type, string message, Severity severity = Severity.Error,
            string unityConsoleStackTrace = null)
        {
            var exceptionProperties = new ExceptionProperties(severity, type, message, unityConsoleStackTrace);
            var exceptionEvent = new ExceptionEvent(exceptionProperties);
            var fingerprint = exceptionEvent.properties.Fingerprint;

            // Ensure we only send one of each unique exception.
            // Entering/exiting playmode and recompiling scripts resets the session.
            if (sentExceptionsByFingerprint.Add(fingerprint))
            {
                Capture(exceptionEvent);
            }
        }

        public static void Capture<T>(Event<T> evt) where T : BaseProperties
        {
            if (OptOut)
            {
                return;
            }

            var payload = CoherenceJson.SerializeObject(
                evt,
                Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                }
            );

            _ = Task.Run(async () =>
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(endpoint);
                    req.Method = "POST";
                    req.ContentType = "application/json";

                    await using (var streamWriter = new StreamWriter(await req.GetRequestStreamAsync()))
                    {
                        await streamWriter.WriteAsync(payload);
                    }

                    var res = await req.GetResponseAsync();
                    res.Close();
                }
#if COHERENCE_DEBUG_EDITOR
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
#else
                catch
                {
                    // do nothing
                }
#endif
            });
        }

        private static void CaptureWarning(object context, Warning warning, string message, params (string key, object value)[] args)
        {
            if (OptOut)
            {
                return;
            }

            if (unitySyncContext == null)
            {
                logger.Debug("Skipping event capture, no sync context", ("warning", warning), ("message", message));
                return;
            }

            if (unitySyncContext == SynchronizationContext.Current)
            {
                CaptureEvent(warning, message, args);
            }
            else
            {
                unitySyncContext.Post((_) => { CaptureEvent(warning, message, args); }, null);
            }

            static void CaptureEvent(Warning warning, string message, params (string key, object value)[] args)
            {
                Capture("coherence_warning",
                    ("warning_id", warning.ToString()),
                    ("message", message),
                    ("isPlaying", Application.isPlaying),
                    ("args", JToken.FromObject(args.ToDictionary(kv => kv.key, kv => kv.value?.ToString())))
                );
            }
        }

        private static void CaptureError(object context, Error error, string message, params (string key, object value)[] args)
        {
            if (OptOut)
            {
                return;
            }

            if (unitySyncContext == null)
            {
                logger.Debug("Skipping event capture, no sync context", ("error", error), ("message", message));
                return;
            }

            if (unitySyncContext == SynchronizationContext.Current)
            {
                CaptureEvent(error, message, args);
            }
            else
            {
                unitySyncContext.Post((_) => { CaptureEvent(error, message, args); }, null);
            }

            static void CaptureEvent(Error error, string message, params (string key, object value)[] args)
            {
                Capture("coherence_error",
                    ("error_id", error.ToString()),
                    ("message", message),
                    ("isPlaying", Application.isPlaying),
                    ("args", JToken.FromObject(args.ToDictionary(kv => kv.key, kv => kv.value?.ToString())))
                );
            }
        }

        private static string TrimUntilNewline(string input)
        {
            var idx = input.IndexOfAny(newLineMarkers);
            return idx >= 0 ? input[..idx] : input;
        }

        private static void TryCaptureUnityConsoleLog(string log, string stackTrace, LogType type)
        {
            // Only capture exceptions and asserts that originate from Coherence code.
            if (type is LogType.Exception or LogType.Assert &&
                (stackTrace.IndexOf("Coherence.", StringComparison.InvariantCulture) != -1))
            {
                CaptureIssue(TrimUntilNewline(log), log, Severity.Error, stackTrace);
            }
        }
    }
}
