// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Collections.Generic;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PrefabSyncGroup))]
    internal class PrefabSyncGroupEditor : BaseEditor
    {
        protected override void OnGUI()
        {
            CoherenceHubLayout.DrawMessageArea(
                $"{nameof(PrefabSyncGroup)} will take care of rebuilding the Prefab hierarchy of networked children in remote Clients." +
                "\n\nThis Component is needed to instantiate the Prefab in runtime, for Prefab instances saved in a Scene, you can disable this Component and sync the hierarchy normally using Uniqueness.");

            EditorGUILayout.Separator();

            var childSyncs = ((PrefabSyncGroup)target).ChildCoherenceSyncs();

            using (new EditorGUILayout.HorizontalScope())
            {
                CoherenceHubLayout.DrawBoldLabel(new GUIContent("Tracked Child CoherenceSyncs"));
                GUILayout.FlexibleSpace();
                CoherenceHubLayout.DrawBoldLabel(new GUIContent($"{childSyncs.Count}"));
            }

            EditorGUILayout.Separator();

            using var disabled = new EditorGUI.DisabledScope(true);
            foreach (var sync in childSyncs)
            {
                _ = EditorGUILayout.ObjectField(sync, typeof(CoherenceSync), false);
            }
        }
    }
}
