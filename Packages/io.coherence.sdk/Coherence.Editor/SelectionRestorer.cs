namespace Coherence.Editor
{
#if UNITY_6000_3_OR_NEWER
    using System.Linq;
#endif
    using UnityEditor;
    using UnityEngine;

    // This class exists to circumvent an issue with Unity where the selection gets lost.
    // See https://issuetracker.unity3d.com/product/unity/issues/guid/UUM-61690

    [InitializeOnLoad]
    internal static class SelectionRestorer
    {
        private static
#if UNITY_6000_3_OR_NEWER
            EntityId[]
#else
            int[]
#endif
            cachedSelection;

        private const string key = "Coherence.SelectionRestorer.Cache";

        static SelectionRestorer()
        {
            cachedSelection = SessionState.GetIntArray(key, null)
#if UNITY_6000_3_OR_NEWER
                ?.Select(x => (EntityId)x).ToArray()
#endif
                ;

            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// Restore to the last known selection on the next Editor frame
        /// (via <see cref="UnityEditor.EditorApplication.delayCall"/>)
        /// </summary>
        public static void RequestRestore()
        {
            EditorApplication.delayCall -= RestoreSelection;
            EditorApplication.delayCall += RestoreSelection;
        }

        private static void OnSelectionChanged()
        {
            var selection = Selection.
#if UNITY_6000_3_OR_NEWER
                entityIds
#else
                instanceIDs
#endif
                ;

            if (selection.Length <= 0)
            {
                return;
            }

            cachedSelection = selection;
            SessionState.SetIntArray(key, cachedSelection
#if UNITY_6000_3_OR_NEWER
                .Select(x => (int)x).ToArray()
#endif
            );
        }

        private static void RestoreSelection()
        {
            if (cachedSelection is not null)
            {
#if UNITY_6000_3_OR_NEWER
                Selection.entityIds = cachedSelection;
#else
                Selection.instanceIDs = cachedSelection;
#endif
            }
        }
    }
}
