// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEditor.PackageManager;
    using UnityEngine;

    internal static class RevisionInfo
    {
        internal static void TryUpdateSdkVersionOverride()
        {
            if (!RuntimeSettings.TryGet(out var runtimeSettings))
            {
                return;
            }

            if (!TryGetBuildMetadata(out var buildMetadata))
            {
                buildMetadata = null;
            }

            if (runtimeSettings.SdkVersionBuildMetadata != buildMetadata)
            {
                runtimeSettings.SdkVersionBuildMetadata = buildMetadata;
                EditorUtility.SetDirty(runtimeSettings);
                AssetDatabase.SaveAssetIfDirty(runtimeSettings);
            }
        }

        private static bool TryGetBuildMetadata(out string buildMetadata)
        {
            buildMetadata = null;

            if (!IsLocalOrEmbeddedPackage(out var sdkPath))
            {
                return false;
            }

            // Try to tell apart repo from release
            var repoPath = Path.GetFullPath(Path.Combine(sdkPath, ".."));
            var ciDir = Path.Combine(repoPath, ".github");
            if (!Directory.Exists(ciDir))
            {
                return false;
            }

            var result = ProcessUtil.RunProcess("git", $"-C \"{repoPath}\" rev-parse --short HEAD", out var output, out var errors);
            if (result != 0)
            {
                Debug.LogError($"Failed to get HEAD commit hash.\nOutput: {output}\nErrors: {errors}");
                return false;
            }

            var hash = output.TrimEnd('\n');
            result = ProcessUtil.RunProcess("git", $"-C \"{repoPath}\" status --porcelain -- sdk", out output, out errors);
            if (result != 0)
            {
                Debug.LogError($"Failed to get porcelain status.\nOutput: {output}\nErrors: {errors}");
            }

            var dirty = string.IsNullOrEmpty(output.TrimEnd('\n')) ? null : ".dirty";

            buildMetadata = $"+sha.{hash}{dirty}";
            return true;
        }

        private static bool IsLocalOrEmbeddedPackage(out string packagePath)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(Paths.packageRootPath);

            if (packageInfo == null)
            {
                packagePath = null;
                return false;
            }

            packagePath = packageInfo.resolvedPath;

            return packageInfo.source is PackageSource.Local or PackageSource.Embedded;
        }
    }
}
