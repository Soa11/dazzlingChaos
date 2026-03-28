// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using System.Text.RegularExpressions;
    using Toolkit;

    [Serializable]
    internal class VersionData
    {
        private static class GUIContents
        {
            public static readonly GUIContent Fetch = new GUIContent("Fetch", "Hold 'Alt' to fetch development releases.");
        }

        public SerializedProperty SerializedProperty
        {
            get => prop;
            set => prop = value;
        }

        public string SelectedVersion => selected == 0 ? null : versions[selected].text;

        private bool populated;
        private GUIContent[] versions;
        private GUIContent label;
        private int selected;
        private SerializedProperty prop;

        private string productID;
        private Func<VersionData, string> resolveDownloadPath;
        private Portal.ReleaseList releases;

        private bool currentPlatformOnly;

        private string DefaultResolveDownloadPath(VersionData versionData) => Paths.GetToolsPath(Application.platform);

        public VersionData(GUIContent label, string productID, bool currentPlatformOnly = false, Func<VersionData, string> resolveDownloadPath = null, SerializedProperty prop = null)
        {
            this.productID = productID;

            this.label = label;
            this.prop = prop;
            this.resolveDownloadPath = resolveDownloadPath ?? DefaultResolveDownloadPath;
            this.currentPlatformOnly = currentPlatformOnly;

            var currentVersion = prop != null ? prop.stringValue : "Select...";
            if (string.IsNullOrEmpty(currentVersion))
            {
                currentVersion = "None";
            }

            versions = new[]
            {
                new GUIContent(currentVersion),
            };
        }

        public void OnGUI()
        {
            _ = EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!populated);
            EditorGUI.BeginChangeCheck();
            var v = EditorGUILayout.Popup(label, selected, versions);
            if (EditorGUI.EndChangeCheck())
            {
                if (DownloadAssets(v))
                {
                    if (prop != null)
                    {
                        prop.serializedObject.Update();
                        prop.stringValue = selected == 0 ? string.Empty : versions[v].text;
                        if (prop.serializedObject.ApplyModifiedProperties())
                        {
                            foreach (var target in prop.serializedObject.targetObjects)
                            {
                                AssetDatabase.SaveAssetIfDirty(target);
                            }
                        }
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button(GUIContents.Fetch, ContentUtils.GUIStyles.fitButton))
            {
                var devReleases = Event.current.alt;
                _ = FetchReleases(devReleases);
            }
            EditorGUILayout.EndHorizontal();
        }

        public bool FetchReleases(bool devReleases = false)
        {
            try
            {
                releases = Portal.ReleaseList.Get(productID, devReleases);
                LoadReleases(releases);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        private void LoadReleases(Portal.ReleaseList releases)
        {
            versions = new GUIContent[1 + releases.releases.Length];
            versions[0] = prop != null ? new GUIContent("None") : new GUIContent("Select...");
            selected = 0;

            for (var i = 0; i < releases.releases.Length; i++)
            {
                var r = releases.releases[i];
                versions[1 + i] = EditorGUIUtility.TrTextContent(r.version, r.published_at);
                if (prop != null && r.version == prop.stringValue)
                {
                    selected = 1 + i;
                }
            }

            populated = true;
        }

        /// <summary>
        /// Gets the platform label used in the filenames of downloadable binaries that is compatible with the current Operating System.
        /// </summary>
        private string GetCurrentPlatformString()
        {
            var platform = Application.platform;
            return platform switch
            {
                RuntimePlatform.LinuxEditor => "linux",
                RuntimePlatform.OSXEditor => "darwin",
                RuntimePlatform.WindowsEditor => "windows",
                _ => null,
            };
        }

        /// <summary>
        /// Gets the architecture label used in the filenames of downloadable binaries that is compatible with the current Operating System.
        /// </summary>
        /// <remarks>
        /// Note that this is not necessarily the architecture of the CPU, but the string that matches the downloadable binaries.
        /// For example, on macOS it returns "universal" which is compatible with both Intel and Apple Silicon CPUs.
        /// </remarks>
        private string GetCurrentArchString()
        {
            var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
            var platform = Application.platform;
            return platform switch
            {
                RuntimePlatform.WindowsEditor => "amd64",
                RuntimePlatform.OSXEditor => "universal",
                RuntimePlatform.LinuxEditor => arch switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => "amd64",
                    System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                    _ => null,
                },
                _ => null,
            };
        }

        private bool DownloadAssets(int index)
        {
            // TODO download assets to temp folder, then if every download is OK, move them to proper folders

            var currentPlatform = GetCurrentPlatformString();
            var currentArch = GetCurrentArchString();
            if (currentPlatformOnly)
            {
                Debug.Assert(currentPlatform != null, "Unable to determine current platform.");
                Debug.Assert(currentArch != null, "Unable to determine current architecture");
            }

            if (index == 0)
            {
                return false;
            }

            var downloadPath = resolveDownloadPath(this);
            var version = releases.releases[index - 1].version;
            if (!EditorUtility.DisplayDialog("Download Binaries", $"Download '{version}' in:\n\n'{downloadPath}'\n\nPrevious binaries will be overwritten. Are you sure?", "Download", "Cancel"))
            {
                return false;
            }

            selected = index;

            var selectedRelease = Portal.Release.Get(productID, version);
            var hasErrors = false;
            for (var i = 0; i < selectedRelease.assets.Length; i++)
            {
                var asset = selectedRelease.assets[i];

                try
                {
                    var rx = new Regex(@"(?<name>[^_]+)_(?<platform>[^_]+)_(?<arch>[^_.]+)\w*\.?(?<ext>\w*).zip");
                    var matches = rx.Matches(asset.name);

                    if (matches.Count == 0 ||
                        (matches[0].Groups["ext"].Value != "exe" && matches[0].Groups["ext"].Value != ""))
                    {
                        // not one of the executables.

                        if (asset.Download(out var nonExeData))
                        {
                            _ = Directory.CreateDirectory($"{downloadPath}/");
                            var dataFile = Path.GetFullPath($"{downloadPath}/{asset.name}");
                            File.WriteAllBytes(dataFile, nonExeData);

                            if (Path.GetExtension(dataFile).ToLower() == ".zip")
                            {
                                // extract it.
                                var decompressPath = Path.GetFullPath($"{downloadPath}/");
                                ZipUtils.Unzip(dataFile, decompressPath);
                                File.Delete(dataFile);
                            }
                        }

                        continue;
                    }

                    var groups = matches[0].Groups;
                    var name = groups["name"].Value;
                    var platform = groups["platform"].Value;
                    var arch = groups["arch"].Value;
                    var ext = groups["ext"].Value;
                    var fileName = ext == "exe" ? name + ".exe" : name;

                    var outputFolderName = platform;

                    if (platform.ToLower() == "linux" && arch.ToLower() == "arm64")
                    {
                        outputFolderName = "linux-arm64";
                    }

                    if (currentPlatformOnly)
                    {
                        outputFolderName = "";

                        if (currentPlatform != platform || currentArch != arch)
                        {
                            continue; // Skip assets that do not match the current platform and architecture
                        }
                    }

                    if (asset.Download(out var data))
                    {
                        _ = Directory.CreateDirectory($"{downloadPath}/{outputFolderName}");
                        var zipFile = Path.GetFullPath($"{downloadPath}/{outputFolderName}/{fileName}.zip");
                        var finalPath = Path.GetFullPath($"{downloadPath}/{outputFolderName}/{fileName}");
                        File.WriteAllBytes(zipFile, data);

                        var decompressPath = Path.GetFullPath($"{downloadPath}/{outputFolderName}");
                        ZipUtils.Unzip(zipFile, decompressPath);
                        File.Delete(zipFile);

                        var originalName = name + "_" + platform + "_" + arch;
                        originalName = ext == "exe" ? originalName + ".exe" : originalName;
                        var originalPath = Path.GetFullPath($"{downloadPath}/{outputFolderName}/{originalName}");
                        File.Delete(finalPath);
                        File.Move(originalPath, finalPath);

                        ProcessUtil.FixUnixPermissions(finalPath);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    hasErrors = true;
                }
            }
            return !hasErrors;
        }
    }
}
