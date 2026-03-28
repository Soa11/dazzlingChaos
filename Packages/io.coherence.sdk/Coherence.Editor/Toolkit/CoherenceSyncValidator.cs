// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Collections.Generic;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Responsible for validating the state of a <see cref="CoherenceSync"/>.
    /// </summary>
    internal static class CoherenceSyncValidator
    {
        private static readonly List<Issue> FoundIssues = new();
        private static readonly List<ArchetypeComponentValidator.Issue> ComponentIssues = new();

        public static bool HasIssue(CoherenceSync sync, Predicate<Issue> filter)
        {
            if (Validate(sync, new(sync.gameObject), FoundIssues))
            {
                return false;
            }

            try
            {
                foreach (var issue in FoundIssues)
                {
                    if (filter(issue))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                FoundIssues.Clear();
            }
        }

        public static bool Validate(CoherenceSync sync, GameObjectStatus status, List<Issue> foundIssues)
        {
            if (status is { IsAsset: false, IsInPrefabStage: false, IsInstanceInScene: false } && !Application.isPlaying)
            {
                foundIssues.Add(Issue.NotConnectedToPrefab);
            }

            // Without this ToolkitArchetype.BoundComponents might not be populated correctly
            sync.ValidateArchetype();
            foreach (var boundComponent in sync.Archetype.BoundComponents)
            {
                if (ArchetypeComponentValidator.Validate(boundComponent, ComponentIssues))
                {
                    continue;
                }

                foreach (var componentIssue in ComponentIssues)
                {
                    var component = boundComponent.Component;
#pragma warning disable CS8524
                    foundIssues.Add(componentIssue.Type switch
#pragma warning restore CS8524
                    {
                        ArchetypeComponentValidator.IssueType.TooManySyncedVariables
                            => Issue.TooManySyncedVariables(component, (int)componentIssue.SyncedVariableCount),
                    });
                }

                ComponentIssues.Clear();
            }

            if (CoherenceSyncConfigValidator.Validate(sync.CoherenceSyncConfig) is { } configIssue)
            {
#pragma warning disable CS8524
                foundIssues.Add(configIssue.Type switch
#pragma warning restore CS8524
                {
                    CoherenceSyncConfigValidator.IssueType.SyncIsAddressableButConfigNot
                        => Issue.SyncIsAddressableButConfigNot(configIssue.PrefabName, configIssue.AddressableGroupName, configIssue.ConfigName),
                });
            }

            return foundIssues.Count is 0;
        }

        public static void DrawIssueHelpBoxes(CoherenceSync sync, SerializedObject serializedObject, GameObjectStatus status)
        {
            if (Validate(sync, status, FoundIssues))
            {
                return;
            }

            try
            {
                foreach (var issue in FoundIssues)
                {
#if HAS_ADDRESSABLES
                    if (issue.Type is IssueType.ConfigNotAddressable)
                    {
                        CoherenceSyncConfigValidator.DrawFixConfigNotAddressableHelpBox(issue.ToString(), serializedObject);
                        continue;
                    }
#endif

                    DrawIssueHelpBox(issue, serializedObject, status);
                }
            }
            finally
            {
                FoundIssues.Clear();
            }
        }

        private static void DrawIssueHelpBox(Issue issue, SerializedObject serializedObject, GameObjectStatus status) => CoherenceSyncEditor.DrawHelpBox(issue.ToString(), issue.MessageType);

        internal readonly struct Issue
        {
            public readonly IssueType Type;
            public readonly MessageType MessageType;
            private readonly object[] args;

            public Component Component => Type is IssueType.TooManySyncedVariables ? (Component)args[0] : null;
            public int? SyncedVariableCount => Type is IssueType.TooManySyncedVariables ? (int)args[1] : null;
            public string PrefabName => Type is IssueType.ConfigNotAddressable ? (string)args[0] : null;
            public string AddressableGroupName => Type is IssueType.ConfigNotAddressable ? (string)args[1] : null;
            public string ConfigName => Type is IssueType.ConfigNotAddressable ? (string)args[2] : null;

            private Issue(IssueType type, MessageType messageType = MessageType.Error, params object[] args)
            {
                Type = type;
                MessageType = messageType;
                this.args = args;
            }

            public static Issue TooManySyncedVariables(Component component, int syncedVariableCount)
                => new(IssueType.TooManySyncedVariables, MessageType.Error, component, syncedVariableCount);

            public static Issue NotConnectedToPrefab => new(IssueType.NotConnectedToPrefab);

            public static Issue SyncIsAddressableButConfigNot(string prefabName, string syncAssetGroup, string configName) => new(IssueType.ConfigNotAddressable, MessageType.Warning, prefabName, syncAssetGroup, configName);

#pragma warning disable CS8524
            public override string ToString() => Type switch
#pragma warning restore CS8524
            {
                IssueType.TooManySyncedVariables => $"The {Component?.GetType().Name} component has {SyncedVariableCount} synced variables. This exceeds the maximum of {ArchetypeComponentValidator.MaxSyncedVariablesPerComponent} per component.",
                IssueType.NotConnectedToPrefab => "CoherenceSync can only be used to network prefabs.",
                IssueType.ConfigNotAddressable => $"The prefab '{PrefabName}' belongs to the addressable group '{AddressableGroupName}' but the CoherenceSyncConfig '{ConfigName}' does not belong to any group. The CoherenceSyncConfig should be added to a group to prevent duplicate instances of getting loaded if multiple assets belonging to different addressable groups reference it.",
                default(IssueType) => "None"
            };
        }

        internal enum IssueType
        {
            TooManySyncedVariables = 1,
            NotConnectedToPrefab = 2,
            ConfigNotAddressable = 3
        }
    }
}
