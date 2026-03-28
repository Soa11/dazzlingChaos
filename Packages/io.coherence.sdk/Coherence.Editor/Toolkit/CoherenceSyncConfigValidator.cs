// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;
#if HAS_ADDRESSABLES
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;
#endif

    /// <summary>
    /// Responsible for validating the state of a <see cref="CoherenceSyncConfig"/>.
    /// </summary>
    internal static class CoherenceSyncConfigValidator
    {
        public static Issue? Validate(CoherenceSyncConfig config)
        {
#if HAS_ADDRESSABLES
            if (config && config.Provider is AddressablesProvider addressablesProvider
                       && AssetDatabase.GetAssetPath(config) is { } configAssetPath
                       && AssetDatabase.AssetPathToGUID(configAssetPath) is { } configGuid
                       && AddressableAssetSettingsDefaultObject.Settings is { } addressableAssetSettings
                       && addressableAssetSettings
                       && addressableAssetSettings.FindAssetEntry(configGuid) is null)
            {
                var group = addressablesProvider.AssetReference?.AssetGUID is { } prefabAssetGuid && addressableAssetSettings.FindAssetEntry(prefabAssetGuid) is { } prefabAssetEntry
                    ? prefabAssetEntry.parentGroup
                    : addressableAssetSettings.DefaultGroup;
                return Issue.SyncIsAddressableButConfigNot(config.Sync?.gameObject.name ?? config.name, group.Name, config.name);
            }
#endif

            return null;
        }

        public static void DrawIssueHelpBoxes(SerializedObject configsSerializedObject)
        {
            foreach (var target in configsSerializedObject.targetObjects)
            {
                var config = target as CoherenceSyncConfig;
                if (!config)
                {
                    continue;
                }

                if (Validate(config) is { } foundIssue)
                {
#if HAS_ADDRESSABLES
                    if (foundIssue.Type is IssueType.SyncIsAddressableButConfigNot)
                    {
                        DrawFixConfigNotAddressableHelpBox(foundIssue.ToString(), configsSerializedObject);
                        return;
                    }
#endif

                    CoherenceSyncEditor.DrawHelpBox(foundIssue.ToString());
                    return;
                }
            }
        }

#if HAS_ADDRESSABLES
        /// <param name="serializedObject">
        /// SerializedObject targeting one or more <see cref="CoherenceSyncConfig"/> or <see cref="CoherenceSync"/> instances.
        /// </param>
        internal static void DrawFixConfigNotAddressableHelpBox(string message, SerializedObject serializedObject)
        {
            CoherenceSyncEditor.DrawFixHelpBox(serializedObject, status: default, message, GUIContents.FixConfigNotAddressableHelpBoxButtonLabel, OnButtonPressed, messageType: MessageType.Warning, canTargetNonAssets: true);

            static void OnButtonPressed(SerializedObject serializedObject)
            {
                foreach (var target in serializedObject.targetObjects)
                {
                    CoherenceSyncConfig config;
                    var sync = target as CoherenceSync;
                    if (sync)
                    {
                        config = sync.CoherenceSyncConfig;
                    }
                    else
                    {
                        config = target as CoherenceSyncConfig;
                        sync = config ? config.Sync : null;
                    }

                    if (sync && config
                        && config.Provider is AddressablesProvider addressablesProvider
                        && AssetDatabase.GetAssetPath(config) is { } configAssetPath
                        && AssetDatabase.AssetPathToGUID(configAssetPath) is { } configGuid
                        && AddressableAssetSettingsDefaultObject.Settings is { } addressableAssetSettings
                        && addressableAssetSettings
                        && addressableAssetSettings.FindAssetEntry(configGuid) is null)
                    {
                        using var logger = Log.Log.GetLogger<INetworkObjectDrawer>(config);
                        var prefabAssetReference = addressablesProvider.AssetReference;
                        var group = prefabAssetReference?.AssetGUID is { } prefabAssetGuid && addressableAssetSettings.FindAssetEntry(prefabAssetGuid) is { } prefabAssetEntry
                            ? prefabAssetEntry.parentGroup
                            : addressableAssetSettings.DefaultGroup;

                        addressableAssetSettings.CreateOrMoveEntry(configGuid, group);
                        addressableAssetSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryCreated, null, true);
                        AssetDatabase.SaveAssets();
                        logger.Info($"Added CoherenceSyncConfig '{config.name}' to addressables group '{group.Name}'.");
                    }
                }
            }
        }
#endif

        internal readonly struct Issue
        {
            public readonly IssueType Type;
            private readonly object[] args;

            public string PrefabName => Type is IssueType.SyncIsAddressableButConfigNot ? (string)args[0] : null;
            public string AddressableGroupName => Type is IssueType.SyncIsAddressableButConfigNot ? (string)args[1] : null;
            public string ConfigName => Type is IssueType.SyncIsAddressableButConfigNot ? (string)args[2] : null;

            private Issue(IssueType type, params object[] args)
            {
                Type = type;
                this.args = args;
            }

            public static Issue SyncIsAddressableButConfigNot(string prefabName, string syncAssetGroup, string configName) => new(IssueType.SyncIsAddressableButConfigNot, prefabName, syncAssetGroup, configName);

#pragma warning disable CS8524
            public override string ToString() => Type switch
#pragma warning restore CS8524
            {
                IssueType.SyncIsAddressableButConfigNot => $"The prefab '{PrefabName}' belongs to the addressable group '{AddressableGroupName}' but the CoherenceSyncConfig '{ConfigName}' does not belong to any group. The CoherenceSyncConfig should be added to a group to prevent duplicate instances of getting loaded if multiple assets belonging to different addressable groups reference it.",
                default(IssueType) => "None"
            };
        }

        internal enum IssueType
        {
            SyncIsAddressableButConfigNot = 1
        }

        private static class GUIContents
        {
            public static readonly GUIContent FixConfigNotAddressableHelpBoxButtonLabel = new("Add Config To Addressable Group");
        }
    }
}
