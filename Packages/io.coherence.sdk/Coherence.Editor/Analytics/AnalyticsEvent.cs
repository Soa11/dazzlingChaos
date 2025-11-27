// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text.RegularExpressions;
    using UnityEngine;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    internal static partial class Analytics
    {
        public static class Events
        {
            public const string Bake = "bake";
            public const string Build = "build";
            public const string CoherenceSyncConfigure = "coherencesync_configure";
            public const string ComponentAdded = "component_added";
            public const string EditorStarted = "editor_started";
            public const string HubSectionClicked = "hub_section_clicked";
            public const string RunLocalReplicatorRooms = "run_local_repl_rooms";
            public const string RunLocalReplicatorWorlds = "run_local_repl_worlds";
            public const string SdkInstalled = "sdk_installed";
            public const string SdkLinkedWithPortal = "sdk_linked_with_portal";
            public const string SdkUpdated = "sdk_updated";
            public const string UploadStart = "upload_start";
            public const string UploadEnd = "upload_end";
            public const string UploadSchema = "upload_schema";
            public const string UploadSimStart = "upload_sim_start";
            public const string UploadSimEnd = "upload_sim_end";
            public const string WelcomeScreenButtonClicked = "welcome_screen_button_clicked";
            public const string CoherenceSyncEditor = "coherence_sync_editor";
            public const string Login = "sdk_login";
            public const string LodLevelAdded = "lod_level_added";
            public const string MenuItem = "menu_item";
            public const string VsaReport = "vsa_report";
            public const string Sample = "sample";
        }

        public enum Severity
        {
            [EnumMember(Value = "debug")]
            Debug,
            [EnumMember(Value = "info")]
            Info,
            [EnumMember(Value = "log")]
            Log,
            [EnumMember(Value = "warning")]
            Warning,
            [EnumMember(Value = "error")]
            Error,
            [EnumMember(Value = "fatal")]
            Fatal,
        }

        [Serializable]
        public class ExceptionData
        {
            [Serializable]
            public class StackTrace
            {
                [Serializable]
                public class Frame
                {
                    [JsonProperty("raw_id")]
                    public string Id => Hash128.Compute(source + methodName + line + column).ToString();

                    [JsonProperty("column")]
                    public int column;

                    [JsonProperty("line")]
                    public int line;

                    [JsonProperty("lang")]
                    public string lang;

                    [JsonProperty("source")]
                    public string source;

                    [JsonProperty("mangled_name")]
                    public string methodName;

                    [JsonProperty("in_app")]
                    public bool inApp = true;

                    [JsonProperty("resolved")]
                    public bool resolved = true;
                }

                [JsonProperty("frames")]
                public Frame[] frames;

                [JsonProperty("type")]
                public string type = "resolved";

                private static readonly Regex UnityConsoleStackTraceRegex =
                    new(@"^(?<method>[^\s]+).*\(at (?<file>.+):(?<line>\d+)\)$", RegexOptions.Multiline);

                private static readonly Regex MonoStackTrace =
                    new(@"^\s*at\s+(?<method>[^(]+)\s*\([^)]*\)\s*(?:\[\s*0x[0-9A-Fa-f]+\s*\]\s*)?in\s+(?<file>.+):(?<line>\d+)\s*$", RegexOptions.Multiline);

                private StackTrace()
                {
                }

                public static StackTrace FromSystemStackTrace(System.Diagnostics.StackTrace stackTrace)
                {
                    var systemFrames = stackTrace.GetFrames() ?? Array.Empty<System.Diagnostics.StackFrame>();
                    if (systemFrames.Length == 0)
                    {
                        return null;
                    }

                    // Skip frames up to (and including) UnityLogger.cs
                    var index = Array.FindIndex(systemFrames, frame =>
                    {
                        var fileName = frame.GetFileName();
                        return fileName != null && fileName.EndsWith("UnityLogger.cs");
                    });

                    if (index != -1 && index + 1 < systemFrames.Length)
                    {
                        systemFrames = systemFrames.Skip(index + 1).ToArray();
                    }

                    var frames = new List<Frame>(systemFrames.Length);
                    foreach (var systemFrame in systemFrames)
                    {
                        var fileName = systemFrame.GetFileName();

                        // When we find a frame without a file name, we stop processing further frames (we're at native code or no names available).
                        // This avoids sending frames with no useful information.
                        if (fileName == null)
                        {
                            break;
                        }

                        frames.Add(new Frame
                        {
                            source = TryResolveStackTracePath(fileName, out var resolvedPath) ? resolvedPath : fileName,
                            methodName = systemFrame.GetMethod()?.Name,
                            line = systemFrame.GetFileLineNumber(),
                            column = systemFrame.GetFileColumnNumber(),
                            lang = "custom",
                        });
                    }

                    return new StackTrace
                    {
                        frames = frames.ToArray(),
                    };
                }

                private static StackTrace FromStackTraceString(string stackTraceString, Regex regex)
                {
                    var matches = regex.Matches(stackTraceString);
                    var frames = new Frame[matches.Count];
                    for (var i = 0; i < matches.Count; i++)
                    {
                        var match = matches[i];
                        var line = match.Groups["line"].Value;
                        var method = match.Groups["method"].Value;
                        var file = match.Groups["file"].Value;

                        frames[i] = new Frame
                        {
                            line = int.TryParse(line, out var lineNumber) ? lineNumber : 0,
                            source = TryResolveStackTracePath(file, out var resolvedPath) ? resolvedPath : file,
                            methodName = method,
                            lang = "custom",
                        };
                    }
                    return new StackTrace
                    {
                        frames = frames,
                    };
                }

                public static StackTrace FromMonoStackTrace(string stackTraceString) => FromStackTraceString(stackTraceString, MonoStackTrace);
                public static StackTrace FromUnityConsoleStackTrace(string stackTraceString) => FromStackTraceString(stackTraceString, UnityConsoleStackTraceRegex);

                private static bool TryResolveStackTracePath(string path, out string resolvedPath)
                {
                    if (!File.Exists(path))
                    {
                        resolvedPath = path;
                        return false;
                    }

                    try
                    {
                        resolvedPath = Path.GetRelativePath(Paths.ResolvedPackageRootPath, path).Replace('\\', '/');
                        return true;
                    }
#if COHERENCE_DEBUG_EDITOR
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        resolvedPath = path;
                        return false;
                    }
#else
                    catch
                    {
                        resolvedPath = path;
                        return false;
                    }
#endif
                }
            }

            [JsonProperty("id")]
            public string Id => Hash128.Compute(type + value).ToString();

            [JsonProperty("type")]
            public string type;

            [JsonProperty("value")]
            public string value;

#pragma warning disable CS0414
            [JsonProperty("stacktrace")]
            public StackTrace stacktrace;
#pragma warning restore CS0414

            private ExceptionData()
            {
            }

            public static ExceptionData FromException(Exception exception)
            {
                return new ExceptionData
                {
                    type = exception.GetType().FullName,
                    value = exception.Message,
                    stacktrace = StackTrace.FromMonoStackTrace(exception.StackTrace),
                };
            }
            public static ExceptionData FromCurrentStackTrace(string type, string value)
            {
                return new ExceptionData
                {
                    type = type,
                    value = value,
                    // 4 is the number of frames to skip to get a clean stack trace (excl. analytics, logger...)
                    stacktrace = StackTrace.FromSystemStackTrace(new System.Diagnostics.StackTrace(true)),
                };
            }

            public static ExceptionData FromUnityConsole(string type, string value, string unityConsoleStackTrace)
            {
                return new ExceptionData
                {
                    type = type,
                    value = value,
                    // 4 is the number of frames to skip to get a clean stack trace (excl. analytics, logger...)
                    stacktrace = StackTrace.FromUnityConsoleStackTrace(unityConsoleStackTrace),
                };
            }
        }

        [Serializable]
        public class ExceptionProperties : BaseProperties
        {
#pragma warning disable CS0414
            [JsonProperty("$exception_list")]
            public List<ExceptionData> exceptions;

            [JsonProperty("$exception_level"), JsonConverter(typeof(StringEnumConverter))]
            public Severity severity;

            [JsonProperty("$exception_fingerprint")]
            public string Fingerprint
            {
                get
                {
                    var hash = new Hash128();
                    foreach (var exception in exceptions)
                    {
                        hash.Append(exception.Id);
                    }
                    return hash.ToString();
                }
            }
#pragma warning restore CS0414

            public ExceptionProperties(Exception exception)
            {
                this.severity = Severity.Fatal;
                this.exceptions = new List<ExceptionData>();
                var currentException = exception;
                const int maxDepth = 4;
                for (var i = 0; i < maxDepth && currentException != null; i++)
                {
                    this.exceptions.Add(ExceptionData.FromException(currentException));
                    currentException = currentException.InnerException;
                }
            }

            public ExceptionProperties(Severity severity, string type, string message, string unityConsoleStackTrace)
            {
                this.severity = severity;
                var exception = unityConsoleStackTrace != null
                    ? ExceptionData.FromUnityConsole(type, message, unityConsoleStackTrace)
                    : ExceptionData.FromCurrentStackTrace(type, message);
                this.exceptions = new List<ExceptionData>
                {
                    exception,
                };
            }

            public ExceptionProperties(Severity severity, string type, string message)
            {
                this.severity = severity;
                this.exceptions = new List<ExceptionData>
                {
                    ExceptionData.FromCurrentStackTrace(type, message),
                };
            }
        }

        [Serializable]
        public class Event<T> where T : BaseProperties
        {
#pragma warning disable CS0414
            [JsonProperty("api_key")]
            public string apiKey;

            [JsonProperty("event")]
            public string name;
            [JsonProperty("properties")]
            public T properties;
#pragma warning restore CS0414

            public Event(string name, T properties)
            {
                this.apiKey = projectAPIKey;
                this.name = name;
                this.properties = properties;
            }
        }

        [Serializable]
        public class ExceptionEvent : Event<ExceptionProperties>
        {
            public ExceptionEvent(Exception exception) : base("$exception", new ExceptionProperties(exception))
            {
            }

            public ExceptionEvent(ExceptionProperties exceptionProperties) : base("$exception", exceptionProperties)
            {
            }
        }

        private class GenericProperties : BaseProperties
        {
            [JsonExtensionData(WriteData = true, ReadData = false)]
            public readonly Dictionary<string, JToken> Properties = new();
        }

        [Serializable]
        public class BaseProperties
        {
            [SerializeField, JsonProperty] protected string distinct_id;

#pragma warning disable CS0414
            [SerializeField, JsonProperty("$device_id")] private string device_id;
            [SerializeField, JsonProperty("$insert_id")] private string insert_id;
            [SerializeField, JsonProperty("$os")] private string os;
            [SerializeField, JsonProperty("$os_version")] private string os_version;
            [SerializeField, JsonProperty("$lib")] private string lib;
            [SerializeField, JsonProperty("$lib_version")] private string lib_version;
            [SerializeField, JsonProperty("$session_id")] private string session_id;
            [SerializeField, JsonProperty("$time")] private long time;
            [SerializeField, JsonProperty("$user_id")] private string user_id;
            [SerializeField, JsonProperty("$groups")] private GroupProperties groups;

            [SerializeField, JsonProperty] private string coherence_engine_version;
            [SerializeField, JsonProperty] private string coherence_sdk_version;
            [SerializeField, JsonProperty] private string game_engine_version;
            [SerializeField, JsonProperty] private string project_id;
            [SerializeField, JsonProperty] private string org_id;
#pragma warning restore CS0414

            public BaseProperties()
            {
                distinct_id = DistinctID;
                device_id = DeviceID;
                insert_id = Guid.NewGuid().ToString();
                os = OS;
                os_version = SystemInfo.operatingSystem;
                lib = EventSource;
                lib_version = Application.unityVersion;
                session_id = SessionID;
                time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

                var userId = UserID;
                if (!string.IsNullOrEmpty(userId) && distinct_id == userId)
                {
                    user_id = userId;
                }

                var runtimeSettings = RuntimeSettings;
                coherence_engine_version = runtimeSettings ? runtimeSettings.RsVersion : string.Empty;
                coherence_sdk_version = runtimeSettings ? runtimeSettings.SdkVersion : string.Empty;
                game_engine_version = Application.unityVersion;
                if (runtimeSettings && !string.IsNullOrEmpty(runtimeSettings.ProjectID))
                {
                    project_id = runtimeSettings.ProjectID;
                }

                var projectSettings = ProjectSettings.instance;
                if (projectSettings && !string.IsNullOrEmpty(projectSettings.OrganizationId))
                {
                    org_id = projectSettings.OrganizationId; // DEPRECATED: remove this property once we fully migrate to groups
                    groups.OrgID = projectSettings.OrganizationId;
                }
            }
        }

        [Serializable]
        private struct UserProperties
        {
            [JsonProperty("internal_user")]
            public bool InternalUser;
        }

        [Serializable]
        private struct GroupProperties
        {
            [JsonProperty("organization")]
            public string OrgID;
        }

        [Serializable]
        private struct OrgProperties
        {
            [JsonProperty("slug")]
            public string Slug;
            [JsonProperty("name")]
            public string Name;
        }

        private class IdentityProperties : BaseProperties
        {
            [JsonProperty("$anon_distinct_id")]
            public string AnonDistinctID;
            [JsonProperty("$set")]
            public UserProperties UserProperties;
        }

        private class OrgIdentityProperties : BaseProperties
        {
            [JsonProperty("$group_type")]
            public string GroupType;
            [JsonProperty("$group_key")]
            public string GroupKey;
            [JsonProperty("$group_set")]
            public OrgProperties GroupSet;

            public OrgIdentityProperties(string orgId, string slug, string name)
            {
                distinct_id = orgId;
                GroupType = "organization";
                GroupKey = orgId;
                GroupSet.Name = name;
                GroupSet.Slug = slug;
            }
        }
    }
}
