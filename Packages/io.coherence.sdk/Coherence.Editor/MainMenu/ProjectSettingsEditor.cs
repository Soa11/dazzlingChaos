// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Cloud;
    using Coherence.Toolkit;
    using Log;
    using Portal;
    using ReplicationServer;
    using Toolkit;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [CustomEditor(typeof(ProjectSettings))]
    internal class ProjectSettingsEditor : Editor
    {
        private const int AllBitsSet = ~0;
        private SerializedProperty worldUDPPort;
        private SerializedProperty worldWebPort;
        private SerializedProperty roomsUDPPort;
        private SerializedProperty roomsWebPort;
        private SerializedProperty sendFrequency;
        private SerializedProperty recvFrequency;
        private SerializedProperty localRoomsCleanupTimeSeconds;
        private SerializedProperty localWorldHostAuthority;
        private SerializedProperty rsConsoleLogLevel;
        private SerializedProperty rsLogToFile;
        private SerializedProperty rsLogFilePath;
        private SerializedProperty rsFileLogLevel;
        private SerializedProperty keepConnectionAlive;
        private SerializedProperty reportAnalytics;

        private RuntimeSettings runtimeSettings;
        private SerializedObject runtimeSettingsSerializedObject;
        private SerializedProperty defaultAuthorityTransferType;
        private SerializedProperty rsVersionOverride;

        private SerializedProperty editorLogLevel;
        private SerializedProperty consoleLogLevel;
        private SerializedProperty sourceFilters;
        private SerializedProperty filterMode;
        private SerializedProperty logStackTrace;
        private SerializedProperty watermark;
        private SerializedProperty addTimestamp;
        private SerializedProperty addSourceType;
        private SerializedProperty logToFile;
        private SerializedProperty logFilePath;
        private SerializedProperty fileLogLevel;

        private SerializedProperty showHubModuleQuickHelp;
        private SerializedProperty bundleRs;

        private string bakeFolder;

        private EditorWindow projectSettingsWindow;

        private bool skipLongUnitTests;

        private SerializedObject versionInfoSerializedObject;
        private SerializedProperty versionInfoSdkProperty;
        private SerializedProperty versionInfoEngineProperty;
        private SerializedProperty versionInfoDocsSlugProperty;

        private VersionData releaseRsVersionData;
        private VersionData customRsVersionData;

        private string devModeKey = "internal";
        private int devModeIndex;

        private enum Mode
        {
            Rooms,
            Worlds,
        }

        private Mode mode;
        private const string modeSessionKey = "Coherence.Settings.Mode";
        private bool alt;
        [NonSerialized] private GUIContent[] onBakingCompletePopupOptions = { new("None", "Don't upload local schema to the any projects after baking has completed.") };

        private class GUIContents
        {
            public static readonly GUIContent port = EditorGUIUtility.TrTextContent("Port");
            public static readonly GUIContent webPort = EditorGUIUtility.TrTextContent("Web Port", "Port used by default on WebGL builds.");
            public static readonly GUIContent replicationServerTitle = EditorGUIUtility.TrTextContent("Local Replication Server");
            public static readonly GUIContent keepConnectionAliveLabel = EditorGUIUtility.TrTextContent("Disable Connection Timeouts (Editor)", "Sets the '--disconnect-timeout' Replication Server flag to its max value.\n\nEven if the Editor is not actively updating, the connection won't time out.");
            public static readonly GUIContent reportAnalytics = EditorGUIUtility.TrTextContent("Share Anonymous Analytics");
            public static readonly GUIContent developmentSettings = EditorGUIUtility.TrTextContent("Development Settings");
            public static readonly GUIContent useCustomTools = EditorGUIUtility.TrTextContent("Use Custom Executable", "Whether to use a replication-server that exists outside of the SDK.");
            public static readonly GUIContent useCustomEndpoints = EditorGUIUtility.TrTextContent("Use Custom Endpoints");
            public static readonly GUIContent customToolsPath = EditorGUIUtility.TrTextContent("Path", "Path to a folder containing the replication-server executable.");
            public static readonly GUIContent customAPIDomain = EditorGUIUtility.TrTextContent("Custom API Domain");
            public static readonly GUIContent defaultAuthorityTransferType = new("Default Authority Transfer Type", "The default Authority Transfer Type for CoherenceSyncs. Useful to change from Steal when using Host Authority restrictions.");
            public static readonly GUIContent consoleLogLevel = EditorGUIUtility.TrTextContent("Console Level");
            public static readonly GUIContent rsConsoleLogLevel = EditorGUIUtility.TrTextContent("Console Level (Editor)");
            public static readonly GUIContent consoleLogFilter = EditorGUIUtility.TrTextContent("Filter", "Comma-separated list of terms.\n\nEach logger is associated with a source. Logger sources (type names) that contain (include) or don't contain (exclude) the terms specified in this filter, will be logged.");
            public static readonly GUIContent editorLogLevel = EditorGUIUtility.TrTextContent("Editor.log Level");
            public static readonly GUIContent logStackTrace = EditorGUIUtility.TrTextContent("Include Stack Trace");
            public static readonly GUIContent addTimestamp = EditorGUIUtility.TrTextContent("Add Timestamp", "Should a timestamp be added to each logged message?\n\nExample: 13:30:30.416 My message.");
            public static readonly GUIContent watermark = EditorGUIUtility.TrTextContent("Watermark", "Optional prefix to add to all logged messages.\n\nExample: [coherence] My message.");
            public static readonly GUIContent addSourceType = EditorGUIUtility.TrTextContent("Add Source Type", "Should the name of the type from which the log originated be added to each logged message?\n\nExample: MyClass: My message.");

            public static readonly GUIContent logFilePath = EditorGUIUtility.TrTextContent("Path");
            public static readonly GUIContent logToFile = EditorGUIUtility.TrTextContent("Write to File");
            public static readonly GUIContent rsLogToFile = EditorGUIUtility.TrTextContent("Write to File");
            public static readonly GUIContent logLevel = EditorGUIUtility.TrTextContent("Level");
            public static readonly GUIContent showHubQuickHelp = EditorGUIUtility.TrTextContent("Hub Quick Help", "Show help boxes with information about each tab on the Hub.");
            public static readonly GUIContent onBakingComplete = EditorGUIUtility.TrTextContent("On Baking Complete", "Specify which projects local schemas should be uploaded to after baking has completed.");

            public static readonly GUIContent projectSelector = EditorGUIUtility.TrTextContent("Cloud Project Mode",
                "Single: use one Cloud project.\n" +
                "Development, Release: two Cloud projects are used; one for Development builds and the other for Release builds and the Editor.\n\n" +
                "Set the projects via coherence Hub > Cloud.");
            public static readonly GUIContent[] projectSelectorOptions =
            {
                EditorGUIUtility.TrTextContent("Single"),
                EditorGUIUtility.TrTextContent("Development, Release"),
            };
            public static readonly GUIContent openHub = EditorGUIUtility.TrTextContent("Open Hub");

            public const int SingleProjectSelectorIndex = 0;
            public const int MultiProjectSelectorIndex = 1;
            public static readonly GUIContent logsTitle = EditorGUIUtility.TrTextContent("Unity Logs");
            public static readonly GUIContent bundleReplicationServer = EditorGUIUtility.TrTextContent("Bundle In Build");
            public static readonly GUIContent localWorldHostAuthority = new("Host Authority Restrictions", "When starting a local World, apply restrictions so that only Simulators and Hosts are allowed to perform the specified actions.");
            public static readonly GUIContent localRoomHostAuthorityHelp = new($"If you want to set host authority restrictions, you need to pass them at room creation time via {nameof(SelfHostedRoomCreationOptions)}.{nameof(SelfHostedRoomCreationOptions.HostAuthority)}.");
            public static readonly GUIContent automationsTitle = EditorGUIUtility.TrTextContent("Automations");
            public static readonly GUIContent uploadSchemasTitle = EditorGUIUtility.TrTextContent("Upload Schemas to Cloud");
            public static readonly GUIContent bakeAutomationsTitle = EditorGUIUtility.TrTextContent("Bake");
            public static readonly GUIContent featureFlagsTitle = EditorGUIUtility.TrTextContent("Feature Flags");
            public static readonly GUIContent scriptingDefineHelp = EditorGUIUtility.TrTextContent("Added only for the active build target");
            public static readonly GUIContent rsLogsTitle = EditorGUIUtility.TrTextContent("Local Replication Server Logs");

            public static readonly GUIContent customRsVersionData = EditorGUIUtility.TrTextContent("Download");
            public static readonly GUIContent releaseRsVersionData = EditorGUIUtility.TrTextContent("Replication Server");
            public static readonly GUIContent rsVersionOverride = EditorGUIUtility.TrTextContent("Version Override", "At runtime, report 'Version Override' instead of the version acknowledged by the SDK.\n\nNo matter what RS is used, this value is what's sent as 'the version' through the protocol and to coherence Cloud.\n\nLeave blank to use the Replication Server version used by the SDK (defined in the VersionInfo asset).");
            public static readonly GUIContent editVersionInfo = EditorGUIUtility.TrTextContent("Edit in Manifest");
            public static readonly GUIContent resolvedSdkVersion = EditorGUIUtility.TrTextContent("Resolved SDK Version", "Hash available when the SDK is used directly from a git repo copy. When available, instead of the actual sdk version (vX.Y.Z), the hash is sent as the SDK version.");
            public static readonly GUIContent sdkVersion = EditorGUIUtility.TrTextContent("SDK Version");
        }

        private void RepaintWindow()
        {
            if (!projectSettingsWindow)
            {
                return;
            }

            projectSettingsWindow.Repaint();
        }

        private void OnEnable()
        {
            // make sure ProjectSettings are editable
            ProjectSettings.instance.hideFlags &= ~HideFlags.NotEditable;

            var t = Type.GetType("UnityEditor.SettingsWindow,UnityEditor.dll");
            if (t != null)
            {
                var windows = Resources.FindObjectsOfTypeAll(t);
                if (windows.Length > 0)
                {
                    projectSettingsWindow = Resources.FindObjectsOfTypeAll(t)[0] as EditorWindow;
                }
            }

            EditorApplication.modifierKeysChanged += RepaintWindow;
            EditorApplication.projectChanged += OnProjectChanged;

            worldUDPPort = serializedObject.FindProperty("worldUDPPort");
            worldWebPort = serializedObject.FindProperty("worldWebPort");
            roomsUDPPort = serializedObject.FindProperty("roomsUDPPort");
            roomsWebPort = serializedObject.FindProperty("roomsWebPort");
            sendFrequency = serializedObject.FindProperty("sendFrequency");
            recvFrequency = serializedObject.FindProperty("recvFrequency");
            localRoomsCleanupTimeSeconds = serializedObject.FindProperty("localRoomsCleanupTimeSeconds");
            localWorldHostAuthority = serializedObject.FindProperty(nameof(ProjectSettings.localWorldHostAuthority));
            rsConsoleLogLevel = serializedObject.FindProperty(nameof(ProjectSettings.rsConsoleLogLevel));
            rsLogToFile = serializedObject.FindProperty(nameof(ProjectSettings.rsLogToFile));
            rsLogFilePath = serializedObject.FindProperty(nameof(ProjectSettings.rsLogFilePath));
            rsFileLogLevel = serializedObject.FindProperty(nameof(ProjectSettings.rsFileLogLevel));
            keepConnectionAlive = serializedObject.FindProperty("keepConnectionAlive");
            reportAnalytics = serializedObject.FindProperty("reportAnalytics");
            showHubModuleQuickHelp = serializedObject.FindProperty(nameof(ProjectSettings.showHubModuleQuickHelp));
            bundleRs = serializedObject.FindProperty(nameof(ProjectSettings.RSBundlingEnabled));

            Refresh();

            mode = (Mode)SessionState.GetInt(modeSessionKey, 0);
            skipLongUnitTests = DefinesManager.IsSkipLongTestsDefineEnabled();

            customRsVersionData ??= new VersionData(GUIContents.customRsVersionData,
                "engine",
                true,
                _ => ProjectSettings.instance.CustomToolsPath);

            if (releaseRsVersionData == null)
            {
                releaseRsVersionData = new VersionData(GUIContents.releaseRsVersionData,
                    "engine",
                    false,
                    _ => Paths.toolsPath,
                    versionInfoEngineProperty);
            }
            else
            {
                releaseRsVersionData.SerializedProperty = versionInfoEngineProperty;
            }
        }

        private void OnDisable()
        {
            EditorApplication.modifierKeysChanged -= RepaintWindow;
            EditorApplication.projectChanged -= OnProjectChanged;
            PortalLogin.StopPolling();
        }

        private void OnProjectChanged()
        {
            Refresh();
            Repaint();
        }

        private void Refresh()
        {
            ProjectSettings.instance.PruneSchemas();

            runtimeSettings = RuntimeSettings.Instance;

            if (runtimeSettingsSerializedObject != null)
            {
                runtimeSettingsSerializedObject.Dispose();
                runtimeSettingsSerializedObject = null;
            }

            if (versionInfoSerializedObject != null)
            {
                versionInfoSerializedObject.Dispose();
                versionInfoSerializedObject = null;
            }

            if (runtimeSettings)
            {
                runtimeSettingsSerializedObject = new SerializedObject(runtimeSettings);
                defaultAuthorityTransferType = runtimeSettingsSerializedObject.FindProperty(nameof(RuntimeSettings.defaultAuthorityTransferType));

                editorLogLevel = runtimeSettingsSerializedObject.FindProperty("logSettings.EditorLogLevel");
                consoleLogLevel = runtimeSettingsSerializedObject.FindProperty("logSettings.LogLevel");
                sourceFilters = runtimeSettingsSerializedObject.FindProperty("logSettings.SourceFilters");
                filterMode = runtimeSettingsSerializedObject.FindProperty("logSettings.FilterMode");
                logStackTrace = runtimeSettingsSerializedObject.FindProperty("logSettings.LogStackTrace");
                addTimestamp = runtimeSettingsSerializedObject.FindProperty($"{nameof(RuntimeSettings.logSettings)}.{nameof(Settings.AddTimestamp)}");
                watermark = runtimeSettingsSerializedObject.FindProperty($"{nameof(RuntimeSettings.logSettings)}.{nameof(Settings.Watermark)}");
                addSourceType = runtimeSettingsSerializedObject.FindProperty($"{nameof(RuntimeSettings.logSettings)}.{nameof(Settings.AddSourceType)}");
                logToFile = runtimeSettingsSerializedObject.FindProperty("logSettings.LogToFile");
                logFilePath = runtimeSettingsSerializedObject.FindProperty("logSettings.LogFilePath");
                fileLogLevel = runtimeSettingsSerializedObject.FindProperty("logSettings.FileLogLevel");

                rsVersionOverride = runtimeSettingsSerializedObject.FindProperty("rsVersionOverride");

                if (!string.IsNullOrEmpty(runtimeSettings.ProjectID))
                {
                    Schemas.UpdateSyncState();
                }

                var versionInfo = runtimeSettings.VersionInfo;
                if (versionInfo)
                {
                    versionInfoSerializedObject = new SerializedObject(versionInfo);

                    versionInfoSdkProperty = versionInfoSerializedObject.FindProperty("sdk");
                    versionInfoEngineProperty = versionInfoSerializedObject.FindProperty("engine");
                    versionInfoDocsSlugProperty = versionInfoSerializedObject.FindProperty("docsSlug");
                }
            }

            if (!string.IsNullOrEmpty(ProjectSettings.instance.LoginToken))
            {
                PortalLogin.FetchOrgs();
            }
        }

        public override void OnInspectorGUI()
        {
            ContentUtils.DrawCloneModeMessage();

            if (Event.current.modifiers == EventModifiers.Alt && Event.current.clickCount == 2)
            {
                GUIUtility.keyboardControl = 0;
                ProjectSettings.instance.ShowDevelopmentSettings = !ProjectSettings.instance.ShowDevelopmentSettings;
                GUIUtility.ExitGUI();
            }

            serializedObject.Update();
            runtimeSettingsSerializedObject?.Update();
            versionInfoSerializedObject?.Update();

            EditorGUI.BeginDisabledGroup(CloneMode.Enabled && !CloneMode.AllowEdits);

            DrawMiscSettings();

            EditorGUILayout.Space();

            DrawAutomations();

            EditorGUILayout.Space();

            DrawLocalReplicationServer();

            DrawMissingRuntimeSettings();

            EditorGUI.EndDisabledGroup();

            DrawLogs();
            DrawRsLogs();

            EditorGUI.BeginDisabledGroup(CloneMode.Enabled && !CloneMode.AllowEdits);

            EditorGUI.EndDisabledGroup();

            if (ProjectSettings.instance.ShowDevelopmentSettings)
            {
                DrawDevelopmentSettingsSettings();
            }

            runtimeSettingsSerializedObject?.ApplyModifiedProperties();
            versionInfoSerializedObject?.ApplyModifiedProperties();
            if (serializedObject.ApplyModifiedProperties())
            {
                ProjectSettings.instance.Save();
            }

            CheckDevMode();
        }

        private void CheckDevMode()
        {
            var e = Event.current;
            if (!e.isKey || e.type != EventType.KeyDown || e.character == '\0')
            {
                return;
            }

            if (e.character == devModeKey[devModeIndex])
            {
                devModeIndex++;
            }
            else
            {
                devModeIndex = 0;
            }

            if (devModeIndex >= devModeKey.Length)
            {
                ProjectSettings.instance.ShowDevelopmentSettings = !ProjectSettings.instance.ShowDevelopmentSettings;
                devModeIndex = 0;
                GUIUtility.ExitGUI();
            }
        }

        private void DrawAutomations()
        {
            EditorGUILayout.LabelField(GUIContents.automationsTitle, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField(GUIContents.bakeAutomationsTitle);
            EditorGUI.indentLevel++;
            BakeUtil.BakeOnEnterPlayMode = EditorGUILayout.Toggle("On Enter Play Mode", BakeUtil.BakeOnEnterPlayMode);
            BakeUtil.BakeOnBuild = EditorGUILayout.Toggle("On Unity Player Build", BakeUtil.BakeOnBuild);
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField(GUIContents.uploadSchemasTitle);
            EditorGUI.indentLevel++;
            PortalUtil.UploadOnEnterPlayMode = EditorGUILayout.Toggle("On Enter Play Mode", PortalUtil.UploadOnEnterPlayMode);
            PortalUtil.UploadOnBuild = EditorGUILayout.Toggle("On Unity Player Build", PortalUtil.UploadOnBuild);
            DrawOnBakingComplete();

            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
        }

        private void DrawOnBakingComplete()
        {
            if (!ProjectSettings.instance.UsingMultipleProjects)
            {
                PortalUtil.UploadAfterBake = EditorGUILayout.Toggle("On Baking Complete", PortalUtil.UploadAfterBake);
                return;
            }

            var projects = ProjectSettings.instance.MultipleProjects;
            var projectCount = projects.Count;
            var optionCount = projectCount + 2;
            var lastOptionIndex = optionCount - 1;
            if (onBakingCompletePopupOptions.Length != optionCount)
            {
                Array.Resize(ref onBakingCompletePopupOptions, optionCount);
                for (var i = 0; i < projectCount; i++)
                {
                    var project = projects[i];
                    onBakingCompletePopupOptions[i + 1] = new(project.Label.text, $"Upload local schema to the '{project.Project.name}' project after baking has completed.");
                }

                onBakingCompletePopupOptions[lastOptionIndex] = new(string.Join(", ", projects.Select(p => p.Label.text)),
                    $"Upload local schema to {(projectCount is 2 ? "both" : "all")} projects after baking has completed:" +
                    $"\n{string.Join("\n", projects.Select(p => $"- {p.Project.name}"))}");
            }

            var indexWas = (int)PortalUtil.UploadAfterBakeFlags;
            if (indexWas is AllBitsSet)
            {
                indexWas = lastOptionIndex;
            }

            var setIndex = EditorGUILayout.Popup(GUIContents.onBakingComplete, indexWas, onBakingCompletePopupOptions);
            if (setIndex != indexWas)
            {
                PortalUtil.UploadAfterBakeFlags = setIndex == lastOptionIndex ? (PortalUtil.UploadAfterBakeOptions)AllBitsSet : (PortalUtil.UploadAfterBakeOptions)setIndex;
            }
        }

        private void DrawBakeWizard()
        {
            if (GUILayout.Button("Bake Wizard", ContentUtils.GUIStyles.bigButton))
            {
                AdvancedBakeWizard.Open();
            }
        }

        private void DrawCustomRsSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Development Replication Server", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            var uct = EditorGUILayout.Toggle(GUIContents.useCustomTools, ProjectSettings.instance.UseCustomTools);
            if (EditorGUI.EndChangeCheck())
            {
                ProjectSettings.instance.UseCustomTools = uct;
                if (uct && string.IsNullOrEmpty(ProjectSettings.instance.CustomToolsPath))
                {
                    var p = Environment.GetEnvironmentVariable("GOPATH");
                    ProjectSettings.instance.CustomToolsPath = p != null ? Path.Combine(p, "bin") : Paths.nativeToolsPath;
                }
            }

            EditorGUI.BeginDisabledGroup(!ProjectSettings.instance.UseCustomTools);
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var path = EditorGUILayout.TextField(GUIContents.customToolsPath, ProjectSettings.instance.CustomToolsPath);
            if (EditorGUI.EndChangeCheck())
            {
                ProjectSettings.instance.CustomToolsPath = path;
            }

            if (GUILayout.Button("Browse", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                var folder = EditorUtility.OpenFolderPanel("Select path to replication-server", ProjectSettings.instance.CustomToolsPath, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    ProjectSettings.instance.CustomToolsPath = folder;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(ProjectSettings.instance.CustomToolsPath))
            {
                if (!File.Exists(Path.Combine(ProjectSettings.instance.CustomToolsPath, Paths.replicationServerName)))
                {
                    EditorGUILayout.HelpBox(
                        $"'{ProjectSettings.instance.CustomToolsPath}' does not contain a binary called '{Paths.replicationServerName}'.",
                        MessageType.Warning);
                }
            }

            EditorGUI.BeginChangeCheck();
            customRsVersionData.OnGUI();
            if (EditorGUI.EndChangeCheck())
            {
                var version = customRsVersionData.SelectedVersion;
                if (!string.IsNullOrEmpty(version))
                {
                    rsVersionOverride.stringValue = version;
                    _ = rsVersionOverride.serializedObject.ApplyModifiedProperties();
                }
                GUIUtility.ExitGUI();
            }
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();

            rsVersionOverride.stringValue = EditorGUILayout.TextField(GUIContents.rsVersionOverride, rsVersionOverride.stringValue);
            if (!string.IsNullOrEmpty(rsVersionOverride.stringValue))
            {
                var rsVersionOverrideRect = GUILayoutUtility.GetLastRect();
                var overrideIconRect = rsVersionOverrideRect;
                overrideIconRect.y += 1;
                overrideIconRect.width = 3;
                overrideIconRect.height = 16;
                EditorGUI.DrawRect(overrideIconRect, Color.cyan);
            }

            EditorLauncher.StartInTerminal = EditorGUILayout.Toggle("Launch from a Terminal", EditorLauncher.StartInTerminal);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawBackendSection()
        {
            _ = EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Backend", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            // custom endpoint toggle
            EditorGUI.BeginChangeCheck();
            var use = EditorGUILayout.Toggle(GUIContents.useCustomEndpoints,
                ProjectSettings.instance.UseCustomEndpoints);
            if (EditorGUI.EndChangeCheck())
            {
                ProjectSettings.instance.UseCustomEndpoints = use;
                if (use && string.IsNullOrEmpty(ProjectSettings.instance.CustomAPIDomain))
                {
                    ProjectSettings.instance.CustomAPIDomain = Endpoints.apiDomain;
                }
            }

            // custom endpoints
            EditorGUI.BeginDisabledGroup(!ProjectSettings.instance.UseCustomEndpoints);
            _ = EditorGUILayout.BeginHorizontal();
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            var apiDomain = EditorGUILayout.TextField(GUIContents.customAPIDomain, ProjectSettings.instance.CustomAPIDomain);
            if (EditorGUI.EndChangeCheck())
            {
                if (apiDomain.StartsWith("https://"))
                {
                    apiDomain = apiDomain[8..];
                    if (apiDomain.IndexOf("/", StringComparison.Ordinal) > -1)
                    {
                        apiDomain = apiDomain[..apiDomain.IndexOf("/", StringComparison.Ordinal)];
                    }
                }

                ProjectSettings.instance.CustomAPIDomain = apiDomain;
            }

            if (GUILayout.Button("local", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                ProjectSettings.instance.CustomAPIDomain = "localhost";
            }

            if (GUILayout.Button("stage", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                ProjectSettings.instance.CustomAPIDomain = "api.stage.coherence.io";
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawTestSection()
        {
            _ = EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Test Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            // skipping long tests
            EditorGUI.BeginChangeCheck();
            skipLongUnitTests = EditorGUILayout.Toggle("Skip Long Unit Tests", skipLongUnitTests);
            if (EditorGUI.EndChangeCheck())
            {
                DefinesManager.ApplySkipLongUnitTestsDefine(skipLongUnitTests);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawDocGenSection()
        {
            if (Directory.Exists(Paths.docFxPath))
            {
                versionInfoDocsSlugProperty.isExpanded = EditorGUILayout.Foldout(versionInfoDocsSlugProperty.isExpanded, "DocFX API Reference");
                if (!versionInfoDocsSlugProperty.isExpanded)
                {
                    return;
                }

                EditorGUILayout.HelpBox("DocFX v2.78.3+ required.", MessageType.Info);
                EditorGUILayout.HelpBox("Make sure your .csprojs are generated and up-to-date. Go to Preferences > External Tools, disable \"Player projects\" and hit \"Regenerate project files\". Then, execute the following steps in order.", MessageType.Info);
                if (File.Exists("Assets/csc.rsp"))
                {
                    EditorGUILayout.HelpBox(
                        "csc.rsp can interfere with docs generation.",
                        MessageType.Warning);
                }

                if (ContentUtils.DrawIndentedButton($"Create {Paths.directoryBuildTargetsFile}"))
                {
                    if (!DocGenUtil.HasDirectoryBuildTargets ||
                        (DocGenUtil.HasDirectoryBuildTargets &&
                        EditorUtility.DisplayDialog(Paths.directoryBuildTargetsFile,
                            $"{Paths.directoryBuildTargetsFile} file exists. Override?",
                            "OK", "Cancel")))
                    {
                        DocGenUtil.GenerateDirectoryBuildTargets();
                        ShowNotification("BuildTargets created");
                    }
                }

                if (ContentUtils.DrawIndentedButton("Build XMLs"))
                {
                    DocGenUtil.RunBuildSolution();
                    ShowNotification("Running on terminal");
                }

                if (ContentUtils.DrawIndentedButton("Build Metadata"))
                {
                    DocGenUtil.FetchBuildArtifacts();
                    ShowNotification("Metadata built");
                }

                if (ContentUtils.DrawIndentedButton("Build & Serve Site"))
                {
                    DocGenUtil.RunDocFx();
                    ShowNotification("Running on terminal");
                }

                if (ContentUtils.DrawIndentedButton("Build & Serve Site (DEBUG)"))
                {
                    DocGenUtil.RunDocFx(true);
                    ShowNotification("Running on terminal");
                }

                EditorGUILayout.Space();

                if (ContentUtils.DrawIndentedButton("Open DocFX Folder"))
                {
                    EditorUtility.RevealInFinder(Paths.docFxConfigPath);
                }

                if (ContentUtils.DrawIndentedButton("Clear All"))
                {
                    if (!EditorUtility.DisplayDialog("Clear All", "Are you sure you want to clear _site and _site.zip?",
                            "Clear All", "Cancel"))
                    {
                        GUIUtility.ExitGUI();
                        return;
                    }

                    var zipPath = Path.GetFullPath(Paths.docFxSiteZipPath);
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }

                    var sitePath = Path.GetFullPath(Paths.docFxSitePath);
                    if (Directory.Exists(sitePath))
                    {
                        Directory.Delete(sitePath, recursive: true);
                    }

                    ShowNotification("Cleared");
                }

                if (ContentUtils.DrawIndentedButton("Zip compiled site"))
                {
                    var sitePath = Path.GetFullPath(Paths.docFxSitePath);
                    if (!File.Exists(sitePath))
                    {
                        Debug.LogError("No site found. Did you build it?");
                        GUIUtility.ExitGUI();
                        return;
                    }

                    EditorUtility.DisplayProgressBar("Compressing documentation site", "Compressing...", 1f);
                    try
                    {
                        var zipPath = Path.GetFullPath(Paths.docFxSiteZipPath);
                        if (File.Exists(zipPath))
                        {
                            File.Delete(zipPath);
                        }
                        ZipUtils.Zip(Path.GetFullPath(Paths.docFxSitePath), Path.GetFullPath(Paths.docFxSiteZipPath), uploadAll: false);

                        if (File.Exists(zipPath))
                        {
                            EditorUtility.RevealInFinder(zipPath);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
        }

        private void DrawReleaseSection()
        {
            _ = EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Release Preparation", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            DrawVersionInfoProperties();
            DrawDocGenSection();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawSdkVersion()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // sdk version
            _ = EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GUIContents.sdkVersion, EditorGUIUtility.TrTempContent(versionInfoSdkProperty.stringValue));

            if (GUILayout.Button(GUIContents.editVersionInfo, ContentUtils.GUIStyles.fitButton))
            {
                var packageManifest = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Paths.packageManifestPath);
                EditorUtility.OpenPropertyEditor(packageManifest);
            }
            EditorGUILayout.EndHorizontal();

            if (!runtimeSettings)
            {
                return;
            }

            // sdk revision hash
            EditorGUI.indentLevel++;
            if (!string.IsNullOrEmpty(runtimeSettings.SdkVersionBuildMetadata))
            {
                EditorGUILayout.LabelField(GUIContents.resolvedSdkVersion, EditorGUIUtility.TrTempContent(runtimeSettings.SdkVersion));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("The release workflow overrides the SDK version listed above.\nThe commit hash is only available for setups where the git repo is used.", ContentUtils.GUIStyles.miniLabelGreyWrap);
            EditorGUILayout.EndVertical();
        }

        private void RefreshPackageLinks()
        {
            var file = Path.GetFullPath("Packages/io.coherence.sdk/package.json");
            var content = File.ReadAllText(file);
            var version = versionInfoDocsSlugProperty.stringValue;
            const string pattern = @"(https://docs\.coherence\.io/)(\d+\.\d+)(.*)";
            var updatedContent = Regex.Replace(content, pattern,
                match => match.Groups[1].Value + version + match.Groups[3].Value);
            var updated = !content.Equals(updatedContent);
            if (updated)
            {
                File.WriteAllText(file, updatedContent);
                AssetDatabase.Refresh();
                Debug.Log("package.json updated");
            }
            else
            {
                Debug.Log("package.json was already up-to-date");
            }
        }

        private static void RefreshHelpUrls()
        {
            var types = TypeCache.GetTypesWithAttribute<HelpURLAttribute>();
            var helpUrlKeys = TypeCache.GetTypesWithAttribute<CoherenceDocumentationAttribute>()
                .ToDictionary(t => t, t => t.GetCustomAttribute<CoherenceDocumentationAttribute>().DocumentationKey);

            var updatedAny = false;
            foreach (var type in types)
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    continue;
                }

                if (type.Namespace == null || !type.Namespace.StartsWith("Coherence"))
                {
                    continue;
                }

                if (!helpUrlKeys.TryGetValue(type, out var docKey))
                {
                    Debug.LogError($"Type '{type.Name}' doesn't have a [{nameof(CoherenceDocumentationAttribute)}]. Please, add it.");
                    continue;
                }

                var go = ObjectFactory.CreateGameObject(SceneManager.GetActiveScene(), HideFlags.HideAndDontSave, "RefreshHelpUrls");
                var monoBehaviour = go.AddComponent(type) as MonoBehaviour;
                var monoScript = MonoScript.FromMonoBehaviour(monoBehaviour);

                if (!monoScript)
                {
                    Debug.LogError($"No MonoScript found for type '{type.Name}'");
                    DestroyImmediate(go);
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(monoScript);
                var text = monoScript.text;

                var docsUrl = DocumentationLinks.GetDocsUrl(docKey);

                var attribute = type.GetCustomAttribute<HelpURLAttribute>();
                if (docsUrl.Equals(attribute.URL))
                {
                    Debug.Log($"Skipped {type.Name}. Already up-to-date.");
                    DestroyImmediate(go);
                    continue;
                }

                text = text.Replace($"[HelpURL(\"{attribute.URL}\")]", $"[HelpURL(\"{docsUrl}\")]");
                File.WriteAllText(path, text);
                updatedAny = true;
                Debug.Log($"Updated {type.Name} from {attribute.URL} to {docsUrl}");
                DestroyImmediate(go);
            }

            if (updatedAny)
            {
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.Log("All HelpURL attributes are up to date!");
            }
        }

        private void DrawVersionInfoProperties()
        {
            // rs version + download
            releaseRsVersionData.OnGUI();

            // docs slug
            EditorGUILayout.PropertyField(versionInfoDocsSlugProperty);
            EditorGUI.indentLevel++;
            if (ContentUtils.DrawIndentedButton("Update [HelpURL] attributes"))
            {
                RefreshHelpUrls();
            }

            if (ContentUtils.DrawIndentedButton("Update links in package.json"))
            {
                RefreshPackageLinks();
            }
            EditorGUI.indentLevel--;
        }

        private void DrawDevelopmentSettingsSettings()
        {
            EditorGUILayout.LabelField(GUIContents.developmentSettings, EditorStyles.boldLabel);

            DrawSdkVersion();
            DrawCustomRsSection();
            DrawReleaseSection();
            DrawBackendSection();
            DrawTestSection();
            DrawBakeWizard();
            DrawUserSettings();
        }

        private void DrawUserSettings()
        {
            EditorGUI.BeginDisabledGroup(!PortalLogin.LoggedInOnce);
            if (GUILayout.Button("Logout + Clear LoggedInOnce"))
            {
                PortalLogin.Logout();
                UserSettings.RemoveKey(PortalLogin.LoggedInOnceKey);
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawMiscSettings()
        {
            _ = EditorGUILayout.PropertyField(reportAnalytics, GUIContents.reportAnalytics);
            _ = EditorGUILayout.PropertyField(showHubModuleQuickHelp, GUIContents.showHubQuickHelp);

            // defaultAuthorityTransferType is unfortunately an int because the Coherence assembly cannot reference the Toolkit assembly
            // so we need to cast it to the enum and render it manually with EnumPopup.
            var defaultAuthorityTransferTypeValue = (CoherenceSync.AuthorityTransferType)defaultAuthorityTransferType.intValue;
            defaultAuthorityTransferTypeValue = (CoherenceSync.AuthorityTransferType)EditorGUILayout.EnumPopup(GUIContents.defaultAuthorityTransferType, defaultAuthorityTransferTypeValue);
            defaultAuthorityTransferType.intValue = (int)defaultAuthorityTransferTypeValue;

            var projectSettings = ProjectSettings.instance;
            var projectSelectorType = projectSettings.UsingMultipleProjects ? GUIContents.MultiProjectSelectorIndex : GUIContents.SingleProjectSelectorIndex;
            EditorGUILayout.BeginHorizontal();
            var setProjectSelectorType = EditorGUILayout.Popup(GUIContents.projectSelector, projectSelectorType, GUIContents.projectSelectorOptions);
            if (projectSelectorType != setProjectSelectorType)
            {
                serializedObject.ApplyModifiedProperties();
                projectSettings.UsingMultipleProjects = setProjectSelectorType is GUIContents.MultiProjectSelectorIndex;
                projectSettings.Save();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button(GUIContents.openHub, ContentUtils.GUIStyles.fitButton))
            {
                _ = CoherenceHub.Open<CloudModule>();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLogs()
        {
            if (!runtimeSettings)
            {
                return;
            }

            EditorGUILayout.LabelField(GUIContents.logsTitle, EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.PropertyField(editorLogLevel, GUIContents.editorLogLevel);
                EditorGUILayout.PropertyField(consoleLogLevel, GUIContents.consoleLogLevel);

                EditorGUI.indentLevel++;
                _ = EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.PropertyField(sourceFilters, GUIContents.consoleLogFilter);
                    var indent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 0;
                    // enum props are always rendered honoring indent, but here we want to have an "inline" element in horizontal space
                    filterMode.enumValueIndex = (int)(Log.FilterMode)EditorGUILayout.EnumPopup((Log.FilterMode)filterMode.enumValueIndex, GUILayout.MaxWidth(68));
                    EditorGUI.indentLevel = indent;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;

                EditorGUILayout.PropertyField(logStackTrace, GUIContents.logStackTrace);
                EditorGUILayout.PropertyField(addTimestamp, GUIContents.addTimestamp);
                EditorGUILayout.PropertyField(watermark, GUIContents.watermark);
                EditorGUILayout.PropertyField(addSourceType, GUIContents.addSourceType);

                EditorGUILayout.PropertyField(logToFile, GUIContents.logToFile);

                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(!logToFile.boolValue);
                {
                    EditorGUILayout.PropertyField(logFilePath, GUIContents.logFilePath);
                    EditorGUILayout.PropertyField(fileLogLevel, GUIContents.logLevel);
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            if (EditorGUI.EndChangeCheck())
            {
                runtimeSettingsSerializedObject.ApplyModifiedProperties();
                runtimeSettings.LogSettings.Apply();
            }

            var lowestLogLevel = runtimeSettings.LogSettings.GetLowestLogLevel();
            DefinesManager.ApplyCorrectLogLevelDefines(lowestLogLevel);

            EditorGUILayout.LabelField("Scripting Defines", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            ContentUtils.DrawScriptingDefine(LogConditionals.DisableInfo);
            ContentUtils.DrawScriptingDefine(LogConditionals.DisableWarning);
            ContentUtils.DrawScriptingDefine(LogConditionals.DisableError);
            EditorGUILayout.LabelField(GUIContents.scriptingDefineHelp, ContentUtils.GUIStyles.miniLabelGreyWrap);
            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;
        }

        private void DrawRsLogs()
        {
            EditorGUILayout.LabelField(GUIContents.rsLogsTitle, EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(rsConsoleLogLevel, GUIContents.rsConsoleLogLevel);
            EditorGUILayout.PropertyField(rsLogToFile, GUIContents.rsLogToFile);
            EditorGUI.BeginDisabledGroup(!rsLogToFile.boolValue);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(rsLogFilePath, GUIContents.logFilePath);
            EditorGUILayout.PropertyField(rsFileLogLevel, GUIContents.logLevel);
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }


        private void DrawLocalReplicationServer()
        {
            EditorGUILayout.LabelField(GUIContents.replicationServerTitle, EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(CloneMode.Enabled && !CloneMode.AllowEdits);
            _ = EditorGUILayout.PropertyField(bundleRs, GUIContents.bundleReplicationServer);
            _ = EditorGUILayout.PropertyField(keepConnectionAlive, GUIContents.keepConnectionAliveLabel);
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;

            _ = EditorGUILayout.BeginHorizontal();
            var r = EditorGUILayout.BeginVertical(ContentUtils.GUIStyles.frameBox);

            var modeCount = 2;
            for (int i = 0; i < modeCount; i++)
            {
                var tabRect = ContentUtils.GetTabRect(r, i, modeCount, out var tabStyle);
                EditorGUI.BeginChangeCheck();
                var m = GUI.Toggle(tabRect, (int)mode == i, ((Mode)i).ToString(), tabStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    GUI.FocusControl(null);
                    if (m)
                    {
                        mode = (Mode)i;
                        SessionState.SetInt(modeSessionKey, i);
                    }
                }
            }

            _ = GUILayoutUtility.GetRect(10, 22);

            // inner frame contents

            if (mode == Mode.Rooms)
            {
                EditorGUI.BeginChangeCheck();
                _ = EditorGUILayout.PropertyField(roomsUDPPort, GUIContents.port);
                _ = EditorGUILayout.PropertyField(roomsWebPort, GUIContents.webPort);
                _ = EditorGUILayout.PropertyField(sendFrequency);
                _ = EditorGUILayout.PropertyField(recvFrequency);
                _ = EditorGUILayout.PropertyField(localRoomsCleanupTimeSeconds);
                if (EditorGUI.EndChangeCheck())
                {
                    roomsUDPPort.intValue = roomsUDPPort.intValue < 0 ? 0 : roomsUDPPort.intValue;
                    if (runtimeSettings)
                    {
                        roomsWebPort.intValue = roomsWebPort.intValue < 0 ? 0 : roomsWebPort.intValue;
                    }

                    sendFrequency.intValue = sendFrequency.intValue < 1 ? 1 : sendFrequency.intValue;
                    recvFrequency.intValue = recvFrequency.intValue < 1 ? 1 : recvFrequency.intValue;
                }

                GUILayout.Label(GUIContents.localRoomHostAuthorityHelp, ContentUtils.GUIStyles.miniLabelGreyWrap);
            }
            else if (mode == Mode.Worlds)
            {
                EditorGUI.BeginChangeCheck();
                _ = EditorGUILayout.PropertyField(worldUDPPort, GUIContents.port);
                _ = EditorGUILayout.PropertyField(worldWebPort, GUIContents.webPort);
                _ = EditorGUILayout.PropertyField(sendFrequency);
                _ = EditorGUILayout.PropertyField(recvFrequency);
                if (EditorGUI.EndChangeCheck())
                {
                    worldUDPPort.intValue = worldUDPPort.intValue < 0 ? 0 : worldUDPPort.intValue;
                    worldWebPort.intValue = worldWebPort.intValue < 0 ? 0 : worldWebPort.intValue;
                    sendFrequency.intValue = sendFrequency.intValue < 1 ? 1 : sendFrequency.intValue;
                    recvFrequency.intValue = recvFrequency.intValue < 1 ? 1 : recvFrequency.intValue;
                }

                _ = EditorGUILayout.PropertyField(localWorldHostAuthority, GUIContents.localWorldHostAuthority);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private void DrawMissingRuntimeSettings()
        {
            if (!runtimeSettings)
            {
                if (GUILayout.Button("Initialize Runtime Settings", EditorStyles.miniButton))
                {
                    Postprocessor.UpdateRuntimeSettings();
                    Refresh();
                }
            }
        }

        private static void ShowNotification(string notification)
        {
            if (!EditorWindow.focusedWindow)
            {
                Debug.Log(notification);
                return;
            }

            EditorWindow.focusedWindow.ShowNotification(new GUIContent(notification), 1f);
        }
    }
}
