// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Collections.Generic;
    using Coherence.Toolkit;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Responsible for validating the state of a <see cref="CoherenceQuery"/>.
    /// </summary>
    internal static class CoherenceQueryValidator
    {
        private static readonly List<Issue> FoundIssues = new();

        public static bool HasAnyIssues(CoherenceQuery query)
        {
            return HasIssue(query, Any);
            static bool Any(Issue issue) => true;
        }

        public static bool HasIssue(CoherenceQuery query, Predicate<Issue> filter) => HasIssue(query, new(query.gameObject), filter);

        public static bool HasIssue(CoherenceQuery query, GameObjectStatus status, Predicate<Issue> filter)
        {
            if (Validate(query, status, FoundIssues))
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

        public static bool Validate(CoherenceQuery query, GameObjectStatus status, List<Issue> foundIssues)
        {
            var bridge = FindBridge(query);

            if (!bridge)
            {
                if (query.gameObject.scene.IsValid() && !status.IsAsset)
                {
                    foundIssues.Add(Issue.BridgeNotFound);
                }
            }
            else
            {
                if (!bridge.isActiveAndEnabled)
                {
                    foundIssues.Add(Issue.BridgeDisabledOrInactive);
                }

                if (!bridge.HasBakedData)
                {
                    foundIssues.Add(Issue.BakedDataNotFound(Application.isPlaying));
                }
            }

            return foundIssues.Count == 0;
        }

        public static void DrawIssueHelpBoxes(CoherenceQuery query, GameObjectStatus status)
        {
            if (Validate(query, status, FoundIssues))
            {
                return;
            }

            try
            {
                foreach (var issue in FoundIssues)
                {
                    DrawIssueHelpBox(issue);
                }
            }
            finally
            {
                FoundIssues.Clear();
            }
        }

        private static void DrawIssueHelpBox(Issue issue) => CoherenceSyncEditor.DrawHelpBox(issue.ToString(), issue.MessageType);

        private static CoherenceBridge FindBridge(CoherenceQuery query) => query.Bridge ? query.Bridge : FindBridge(query.gameObject.scene);

        private static CoherenceBridge FindBridge(Scene scene)
            => scene.IsValid() && (CoherenceBridgeStore.TryGetBridge(scene, out var bridge) || CoherenceBridge.TryGetEditorOnly(scene, out bridge)) ? bridge : null;

        internal readonly struct Issue
        {
            public readonly IssueType Type;
            private readonly object[] args;

#pragma warning disable CS8524
            public UnityEditor.MessageType MessageType => Type switch
#pragma warning restore CS8524
            {
                IssueType.BridgeNotFound => UnityEditor.MessageType.Error,
                IssueType.BakedDataNotFound => UnityEditor.MessageType.Error,
                IssueType.BridgeDisabledOrInactive => UnityEditor.MessageType.Error
            };

            private Issue(IssueType type, params object[] args)
            {
                Type = type;
                this.args = args;
            }

            public static Issue BridgeNotFound => new(IssueType.BridgeNotFound);
            public static Issue BridgeDisabledOrInactive => new(IssueType.BridgeDisabledOrInactive);
            public static Issue BakedDataNotFound(bool isPlaying) => new(IssueType.BakedDataNotFound, isPlaying);

#pragma warning disable CS8524
            public override string ToString() => Type switch
#pragma warning restore CS8524
            {
                IssueType.BridgeNotFound => $"{nameof(CoherenceBridge)} required in the scene.",
                IssueType.BakedDataNotFound when args.Length > 0 && args[0] is bool isPlaying =>
                    isPlaying
                        ? "Network code not found. Exit Play Mode, and bake via coherence > Bake."
                        : "Network code not found. Bake via coherence > Bake.",
                IssueType.BridgeDisabledOrInactive => $"An active {nameof(CoherenceBridge)} is required in the scene.",
            };
        }

        internal enum IssueType
        {
            BridgeNotFound = 1,
            BridgeDisabledOrInactive = 2,
            BakedDataNotFound = 3
        }
    }
}
