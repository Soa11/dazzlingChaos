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
                    DrawIssueHelpBox(issue, serializedObject, status);
                }
            }
            finally
            {
                FoundIssues.Clear();
            }
        }

        private static void DrawIssueHelpBox(Issue issue, SerializedObject serializedObject, GameObjectStatus status) => CoherenceSyncEditor.DrawHelpBox(issue.ToString(), MessageType.Error);

        internal readonly struct Issue
        {
            public readonly IssueType Type;
            private readonly object[] args;

            public Component Component => Type is IssueType.TooManySyncedVariables ? (Component)args[0] : null;
            public int? SyncedVariableCount => Type is IssueType.TooManySyncedVariables ? (int)args[1] : null;

            private Issue(IssueType type, params object[] args)
            {
                Type = type;
                this.args = args;
            }

            public static Issue TooManySyncedVariables(Component component, int syncedVariableCount)
                => new(IssueType.TooManySyncedVariables, component, syncedVariableCount);

            public static Issue NotConnectedToPrefab => new(IssueType.NotConnectedToPrefab);

#pragma warning disable CS8524
            public override string ToString() => Type switch
#pragma warning restore CS8524
            {
                IssueType.TooManySyncedVariables => $"The {Component?.GetType().Name} component has {SyncedVariableCount} synced variables. This exceeds the maximum of {ArchetypeComponentValidator.MaxSyncedVariablesPerComponent} per component.",
                IssueType.NotConnectedToPrefab => "CoherenceSync can only be used to network prefabs.",
                default(IssueType) => "None"
            };
        }

        internal enum IssueType
        {
            TooManySyncedVariables = 1,
            NotConnectedToPrefab = 2
        }
    }
}
