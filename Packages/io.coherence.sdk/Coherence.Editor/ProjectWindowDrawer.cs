namespace Coherence.Editor
{
    using Portal;
    using Toolkit;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal static class ProjectWindowDrawer
    {
        private static string coherenceFolderGuid;

        static ProjectWindowDrawer()
        {
            EditorApplication.projectWindowItemOnGUI += OnItemGUI;
            EditorApplication.projectChanged += OnProjectChanged;
            Schemas.OnSchemaStateUpdate += OnSchemaStateUpdate;
            UpdateFolderGuid();
        }

        private static void OnSchemaStateUpdate()
        {
            if (!Application.isBatchMode)
            {
                EditorApplication.RepaintProjectWindow();
            }
        }

        private static void OnProjectChanged() => UpdateFolderGuid();
        private static void UpdateFolderGuid() => coherenceFolderGuid = AssetDatabase.AssetPathToGUID(Paths.projectAssetsPath);

        private static void OnItemGUI(string guid, Rect rect)
        {
            if (guid != coherenceFolderGuid)
            {
                return;
            }

            // only render at smallest height
            var smallestHeight = 16f;
            if (!Mathf.Approximately(rect.height, smallestHeight))
            {
                return;
            }

            // precalculated size needed to render a folder with the name "coherence"
            var usedWidth = 80f;
            var iconWidth = 16f;
            if (rect.width <= usedWidth + iconWidth)
            {
                return;
            }

            var iconRect = rect;
            iconRect.xMin = iconRect.xMax - iconWidth;

            var content = ProjectSyncButton.GetContent();
            var enabled = ProjectSyncButton.GetEnabled();

            if (DrawIconButton(iconRect, content) && enabled)
            {
                ProjectSyncButton.Invoke();
            }
        }

        private static bool DrawIconButton(Rect rect, GUIContent content, bool disabled = false)
        {
            EditorGUI.BeginDisabledGroup(disabled);
            var style = disabled ? GUIStyle.none : ContentUtils.GUIStyles.iconButton;
            var clicked = GUI.Button(rect, content, style);
            EditorGUI.EndDisabledGroup();

            return clicked;
        }
    }
}
