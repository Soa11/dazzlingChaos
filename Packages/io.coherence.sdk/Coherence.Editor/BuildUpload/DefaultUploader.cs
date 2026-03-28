// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using Portal;
    using UnityEditor;
    using UnityEngine;

    internal class DefaultUploader : BuildUploader
    {
        internal override bool Upload(AvailablePlatforms platform, string buildPath, [MaybeNull] ProjectInfo project)
        {
            var zipPath = GetZipTempPath();
            var platformAsString = platform.ToString().ToLowerInvariant();

            OnUploadStart(platformAsString);

            try
            {
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayProgressBar("Game", "Compressing game build path...", 1f);
                }

                File.Delete(zipPath);

                // We want to compress the contents of the build folder, not the folder itself
                var srcPath = Path.Combine(buildPath, "*");
                ZipUtils.Zip(srcPath, zipPath);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            try
            {
                var size = new FileInfo(zipPath).Length;

                // request a valid upload endpoint
                var uurl = Portal.UploadURL.GetGame(size, platformAsString, project);
                if (uurl == null)
                {
                    return false;
                }

                // upload the game (zipfile)
                if (!uurl.Upload(zipPath, size))
                {
                    return false;
                }

                _ = Portal.UploadURL.RegisterBuild(platform: platformAsString, filename: "", project);

                OnUploadEnd(platformAsString);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }

            return true;
        }
    }
}
