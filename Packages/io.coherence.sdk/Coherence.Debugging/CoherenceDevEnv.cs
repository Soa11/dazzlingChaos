// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER && UNITY_EDITOR

namespace Coherence.Debugging
{
    using System.IO;
    using UnityEngine;
    using UnityEditor;

    /// <summary>
    /// Detects whether we are in a "coherence dev environment", that is, are we running on a coherence team member machine.
    /// </summary>
    internal static class CoherenceDevEnv
    {
        private const string DevEnvDetectedPrefsKey = "Coherence.Debugging.CoherenceDevEnv.DevEnvDetected";
        private const string DevEnvSystemVariable = "COHERENCE_DEV";

        public static bool IsDevEnvSystemVariableSet { get; private set; }

        // Have we at least once detected the dev env automatically?
        public static bool WasDevEnvDetected
        {
            get => EditorPrefs.GetBool(DevEnvDetectedPrefsKey, false);
            set => EditorPrefs.SetBool(DevEnvDetectedPrefsKey, value);
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            DetectDevEnvSystemVariable();

            if (WasDevEnvDetected)
            {
                return;
            }

            if (IsCoherenceDevEnvironment())
            {
                Debug.Log("[coherence] Developer environment detected.");
                WasDevEnvDetected = true;
                DbgAssert.EnableAtCompileTime();
            }
        }

        private static bool IsCoherenceDevEnvironment()
        {
            return IsRepositoryWithSdkDir() || IsDevEnvSystemVariableSet;
        }

        private static void DetectDevEnvSystemVariable()
        {
            var currentValue = System.Environment.GetEnvironmentVariable(DevEnvSystemVariable, System.EnvironmentVariableTarget.User);
            IsDevEnvSystemVariableSet = !string.IsNullOrEmpty(currentValue);
        }

        private static bool IsRepositoryWithSdkDir()
        {
            try
            {
                var scriptPath = GetScriptDirectory();
                var current = new DirectoryInfo(scriptPath);

                while (current != null)
                {
                    var gitPath = Path.Combine(current.FullName, ".git");

                    if (Directory.Exists(gitPath))
                    {
                        var isCoherenceRepo = Directory.Exists(Path.Combine(current.FullName, "sdk"));
                        return isCoherenceRepo;
                    }

                    current = current.Parent;
                }
            }
            catch
            {
                // This. Is. Fine.
            }

            return false;
        }

        private static string GetScriptDirectory([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        {
            return Path.GetDirectoryName(sourceFilePath);
        }

        public static void SetDevEnvSystemEnvironmentVariable(string value)
        {
            try
            {
                System.Environment.SetEnvironmentVariable(DevEnvSystemVariable, value, System.EnvironmentVariableTarget.User);
                Debug.Log($"[coherence] {DevEnvSystemVariable} environment variable set to '{value}'. Unity Editor needs to restart for changes to take effect.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[coherence] Failed to set {DevEnvSystemVariable} environment variable to '{value}': {e.Message}");
            }
        }
    }
}

#endif
