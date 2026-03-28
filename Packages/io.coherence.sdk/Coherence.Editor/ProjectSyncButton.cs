namespace Coherence.Editor
{
    using System.Threading.Tasks;
    using Common;
    using Portal;
    using UnityEditor;
    using UnityEngine;
    using Log = Coherence.Log.Log;
    using Error = Log.Error;

    internal static class ProjectSyncButton
    {
        public enum State
        {
            CloneModeEnabled,
            BakeOutdated,
            NotLoggedIn,
            NoOrganizationsFetched,
            NoOrganizationSelected,
            NoProjectSelected,
            CloudOutOfSync,
            CloudInSync,
        }

        private static class GUIContents
        {
            public static readonly GUIContent Clone = Icons.GetContent("Coherence.Clone",
            "This Editor instance is a Clone. Clones don't allow baking or uploading schemas. Asset automations such as updating prefabs are disabled by default. To bypass this, go to any coherence window, like the Hub, and click on 'Allow Editing'.");

            public static readonly GUIContent CloneAllowEdits = Icons.GetContent("Coherence.Clone.Edit",
                "This Editor instance is a Clone. Clones don't allow baking or uploading schemas. Asset automations such as updating prefabs are currently enabled. To disable edits, go to any coherence window, like the Hub, and click on 'Allow Editing'.");

            public static readonly GUIContent BakeOutdated = Icons.GetContent("Coherence.Bake.Warning",
                "Bake required for networking.\n\nClick to bake.");

            public static readonly GUIContent NotLoggedIn = Icons.GetContent("Logo.Icon.Disabled",
                "Bake up-to-date.\nNot logged in to coherence Cloud.");

            public static readonly GUIContent NoOrganizationsFetched = Icons.GetContent("Logo.Icon.Disabled",
                "Organizations not fetched.\nClick to open coherence Cloud window.");

            public static readonly GUIContent CloudOutOfSync = Icons.GetContent("Coherence.Cloud.Warning",
                "Schema not found in Cloud.\n\nClick to upload.");

            public static readonly GUIContent NoOrganizationSelected = Icons.GetContent("Coherence.Cloud.Warning",
                "No organization selected.\n\nClick to open coherence Cloud window.");

            public static readonly GUIContent NoProjectSelected = Icons.GetContent("Coherence.Cloud.Warning",
                "No project selected.\n\nClick to open coherence Cloud window.");

            public static readonly GUIContent CloudInSync = Icons.GetContent("Logo.Icon",
                "Bake up-to-date.\nLogged in to coherence Cloud.");
        }

        /// <remarks>
        /// Can call <see cref="PortalLogin.FetchOrgs"/> if it wasn't called yet.
        /// </remarks>
        public static State GetState()
        {
            if (CloneMode.Enabled)
            {
                return State.CloneModeEnabled;
            }

            if (BakeUtil.Outdated)
            {
                return State.BakeOutdated;
            }

            if (!PortalLogin.IsLoggedIn)
            {
                return State.NotLoggedIn;
            }

            if (!PortalLogin.OrganizationsFetched)
            {
                // TODO consider moving this call outside of the UI logic
                if (!PortalLogin.FetchedOrganizationsOnce)
                {
                    PortalLogin.FetchOrgs();
                }

                return State.NoOrganizationsFetched;
            }

            if (!PortalUtil.OrgIsSet)
            {
                return State.NoOrganizationSelected;
            }

            if (!PortalUtil.OrgAndProjectIsSet)
            {
                return State.NoProjectSelected;
            }

            if (PortalUtil.SyncState != Schemas.SyncState.InSync)
            {
                return State.CloudOutOfSync;
            }

            return State.CloudInSync;
        }

        public static GUIContent GetContent()
        {
            return GetState() switch
            {
                State.CloneModeEnabled => CloneMode.AllowEdits ? GUIContents.CloneAllowEdits : GUIContents.Clone,
                State.BakeOutdated => GUIContents.BakeOutdated,
                State.NotLoggedIn => GUIContents.NotLoggedIn,
                State.NoOrganizationsFetched => GUIContents.NoOrganizationsFetched,
                State.NoOrganizationSelected => GUIContents.NoOrganizationSelected,
                State.NoProjectSelected => GUIContents.NoProjectSelected,
                State.CloudOutOfSync => CustomizeCloudOutOfSync(),
                State.CloudInSync => CustomizeCloudInSync(),
                _ => GUIContent.none,
            };

            GUIContent CustomizeCloudOutOfSync()
            {
                var org = ProjectSettings.instance.OrganizationName;
                var project = RuntimeSettings.Instance.ProjectName;
                var id = string.IsNullOrEmpty(org) ? project : $"{org}/{project}";
                GUIContents.CloudOutOfSync.tooltip = ProjectSettings.instance.UsingMultipleProjects
                    ? "Local shema not uploaded to all projects.\n\nClick to upload."
                    : $"Local schema not uploaded to project.\nProject '{id}'\n\nClick to upload.";
                return GUIContents.CloudOutOfSync;
            }

            GUIContent CustomizeCloudInSync()
            {
                var org = ProjectSettings.instance.OrganizationName;
                var project = RuntimeSettings.Instance.ProjectName;
                var id = string.IsNullOrEmpty(org) ? project : $"{org}/{project}";
                GUIContents.CloudInSync.tooltip = $"Logged in to coherence Cloud.\nProject '{id}'";
                return GUIContents.CloudInSync;
            }
        }

        public static bool GetEnabled() => GetState() != State.CloneModeEnabled;

        public static void Invoke()
        {
            switch (GetState())
            {
                case State.CloneModeEnabled:
                    // No action
                    break;
                case State.BakeOutdated:
                    BakeUtil.BakeAsync(waitForUpdateSyncState: ShouldWait.Never).Then(task =>
                    {
                        using var logger = Log.GetLogger(typeof(ProjectSyncButton));
                        logger.Error(Error.EditorBakingFailed, "Baking failed: " + task.Exception?.Message);
                    }, TaskContinuationOptions.OnlyOnFaulted);
                    break;
                case State.NotLoggedIn:
                case State.NoOrganizationsFetched:
                case State.NoOrganizationSelected:
                case State.NoProjectSelected:
                    CoherenceHub.Open<CloudModule>();
                    break;
                case State.CloudOutOfSync:
                    Schemas.UploadActiveAsync(InteractionMode.UserAction, EditorWindow.focusedWindow).Then(task =>
                    {
                        using var logger = Log.GetLogger(typeof(ProjectSyncButton));
                        logger.Error(Error.EditorUploadingSchemasFailed, "Uploading schema failed: " + task.Exception?.Message);
                    }, TaskContinuationOptions.OnlyOnFaulted);
                    break;
                case State.CloudInSync:
                    CoherenceHub.Open();
                    break;
            }
        }
    }
}
