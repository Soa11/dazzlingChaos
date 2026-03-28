// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Diagnostics;
    using System.IO;
    using UnityEditor;

    internal static class ZipUtils
    {
        public const string ExecutableName =
#if UNITY_EDITOR_WIN
                "7z.exe"
#else
                "7za"
#endif
            ;

        public static readonly string ExecutablePath;

        static ZipUtils()
        {
            var toolsPath = Path.Combine(EditorApplication.applicationContentsPath, "Tools", ExecutableName);
            if (File.Exists(toolsPath))
            {
                ExecutablePath = toolsPath;
                return;
            }

            // On macOS, starting with Unity 6000.3.0f1, the 7za executable has been moved to Helpers.
            // Instead of version-specific paths, we do a fallback approach in case Unity changes this in the future.
            var helpersPath = Path.Combine(EditorApplication.applicationContentsPath, "Helpers", ExecutableName);
            if (File.Exists(helpersPath))
            {
                ExecutablePath = helpersPath;
            }

            // If not found, leave ExecutablePath as null. The caller should handle this case.
        }

        /// <param name="zipPath">Path of the zip file to decompress</param>
        /// <param name="destPath">Path where to decompress the zip file contents</param>
        /// <exception cref="IOException">If the native 7z application exit code is nonzero.</exception>
        public static void Unzip(string zipPath, string destPath)
        {
            Debug.Assert(File.Exists(ExecutablePath));

            var args = $"x -y -o\"{destPath}\" \"{zipPath}\"";

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = ExecutablePath,
                Arguments = args,
                RedirectStandardError = true,
            };

            var process = Process.Start(startInfo);
            Debug.Assert(process != null, "Can't start unzip process");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new IOException($"Failed to unzip:\n{ExecutablePath} {args}\n{process.StandardError.ReadToEnd()}");
            }
        }

        /// <param name="srcPath">The file or folder to compress.</param>
        /// <param name="zipPath">Path for</param>
        /// <exception cref="IOException">If the native 7z application exit code is nonzero.</exception>
        public static void Zip(string srcPath, string zipPath)
        {
            Debug.Assert(File.Exists(ExecutablePath));

            var args = $"a -tzip \"{zipPath}\" -y \"{srcPath}\"";

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = ExecutablePath,
                Arguments = args,
                RedirectStandardError = true,
            };

            var process = Process.Start(startInfo);
            Debug.Assert(process != null, "Can't start zip process");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new IOException($"Failed to zip:\n{ExecutablePath} {args}\n{process.StandardError.ReadToEnd()}");
            }
        }
    }
}
