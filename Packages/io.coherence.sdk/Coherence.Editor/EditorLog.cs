// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Text;
    using System.IO;
    using UnityEngine;
    using UnityEditor;
    using Log;
    using Logger = Log.Logger;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    [InitializeOnLoad]
    internal static class FileLog
    {
        private const string activeKey = "Coherence.FileLog.Active";
        private static readonly object threadlock = new();

        static FileLog()
        {
#if !COHERENCE_DISABLE_FILE_LOG
            try
            {
                Logger.OnLog += OnLog;

                if (!SessionState.GetBool(activeKey, false))
                {
                    SessionState.SetBool(activeKey, true);

                    if (File.Exists(Paths.CurrentCoherenceLogFilePath))
                    {
                        if (File.Exists(Paths.PreviousCoherenceLogFilePath))
                        {
                            File.Delete(Paths.PreviousCoherenceLogFilePath);
                        }

                        File.Move(Paths.CurrentCoherenceLogFilePath, Paths.PreviousCoherenceLogFilePath);
                    }
                }

                _ = Directory.CreateDirectory(Paths.CoherenceLogFilesPath);
            }
            catch (Exception e)
            {
                Logger.OnLog -= OnLog;
                Debug.LogException(e);
            }
#endif
        }

        private static void OnLog(LogLevel logLevel, bool filtered, string message, Type source,
            ICollection<(string key, object value)> args)
        {
            if (filtered)
            {
                return;
            }

            var editorLogLevel = Log.GetSettings().EditorLogLevel;

            if (logLevel < editorLogLevel)
            {
                return;
            }

            try
            {
                var log =
                    $"{DateTime.UtcNow:u} - {source?.Name ?? "NoSource"} - {logLevel.ToString().ToUpperInvariant()} - {message ?? "No message"}";
                if (args != null && args.Count > 0)
                {
                    var argsDict = new Dictionary<string, string>();
                    foreach (var (key, value) in args)
                    {
                        argsDict[key] = value?.ToString();
                    }

                    var json = Utils.CoherenceJson.SerializeObject(
                        argsDict,
                        Formatting.None);

                    log += $" - {json}";
                }

                log += "\n";

                lock (threadlock)
                {
                    File.AppendAllText(Paths.CurrentCoherenceLogFilePath, log, Encoding.UTF8);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
