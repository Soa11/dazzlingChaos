// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Coherence.Toolkit.Archetypes;
    using MessageType = UnityEditor.MessageType;

    /// <summary>
    /// Responsible for validating the state of a single component bound to a networked object.
    /// </summary>
    internal static class ArchetypeComponentValidator
    {
        internal const int MaxSyncedVariablesPerComponent = 32;

        private static readonly List<Issue> FoundIssues = new();

        public static bool Validate(ArchetypeComponent boundComponent, List<Issue> foundIssues)
        {
            var variableCount = boundComponent.Bindings.Count(b => b is { IsMethod: false });
            if (variableCount > MaxSyncedVariablesPerComponent)
            {
                foundIssues.Add(Issue.TooManySyncedVariables(variableCount));
            }

            return foundIssues.Count is 0;
        }

        public static void DrawIssueHelpBoxes(ArchetypeComponent boundComponent, Predicate<Issue> filter)
        {
            if (Validate(boundComponent, FoundIssues))
            {
                return;
            }

            try
            {
                foreach (var issue in FoundIssues)
                {
                    if (filter(issue))
                    {
                        DrawIssueHelpBox(issue);
                    }
                }
            }
            finally
            {
                FoundIssues.Clear();
            }
        }

        private static void DrawIssueHelpBox(Issue issue) => CoherenceSyncEditor.DrawHelpBox(issue.ToString(), MessageType.Error);

        internal readonly struct Issue
        {
            public readonly IssueType Type;
            private readonly object[] args;

            public int? SyncedVariableCount => Type is IssueType.TooManySyncedVariables ? (int)args[0] : null;

            private Issue(IssueType type, params object[] args)
            {
                Type = type;
                this.args = args;
            }

            public static Issue TooManySyncedVariables(int syncedVariableCount) => new(IssueType.TooManySyncedVariables, syncedVariableCount);

#pragma warning disable CS8524
            public override string ToString() => Type switch
#pragma warning restore CS8524
            {
                IssueType.TooManySyncedVariables => $"This component has {SyncedVariableCount} synced variables. This exceeds the maximum of {MaxSyncedVariablesPerComponent} per component.",
                default(IssueType) => "None"
            };
        }

        internal enum IssueType
        {
            TooManySyncedVariables = 1
        }
    }
}
