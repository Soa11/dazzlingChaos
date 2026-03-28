#if UNITY_6000_3_OR_NEWER
namespace Coherence.Editor
{
    using Portal;
    using UnityEditor.Toolbars;
    using UnityEngine;

    internal static class MainToolbarUI
    {
        private const string BakeButtonElementName = "coherence/Bake";
        private static MainToolbarButton bakeButton;

        static MainToolbarUI()
        {
            CloneMode.OnChanged += Refresh;
            BakeUtil.OnSchemasDirtyChanged += Refresh;
            Schemas.OnSchemaStateUpdate += Refresh;
        }

        public static void Refresh()
        {
            UpdateBakeState();
            MainToolbar.Refresh(BakeButtonElementName);
        }

        private static void UpdateBakeState()
        {
            if (bakeButton == null)
            {
                return;
            }

            var content = ProjectSyncButton.GetContent();
            var toolbarContent = bakeButton.content;
            toolbarContent.image = content.image as Texture2D;
            toolbarContent.text = content.text;
            toolbarContent.tooltip = content.tooltip;
            bakeButton.content = toolbarContent;
        }

        [MainToolbarElement(BakeButtonElementName, defaultDockPosition = MainToolbarDockPosition.Middle)]
        internal static MainToolbarElement AddBakeToolbarElement()
        {
            if (bakeButton == null)
            {
                var content = new MainToolbarContent();
                bakeButton = new MainToolbarButton(content, ProjectSyncButton.Invoke);
            }

            UpdateBakeState();

            return bakeButton;
        }
    }
}
#endif
