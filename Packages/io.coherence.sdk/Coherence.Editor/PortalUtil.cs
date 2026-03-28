// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Portal;
    using UnityEditor;
    using UnityEngine;
    using Log = Log.Log;
    using Error = Log.Error;

    public static class PortalUtil
    {
        static PortalUtil() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange is PlayModeStateChange.ExitingEditMode)
            {
                Postprocessor.UpdateRuntimeSettings();

                if (UploadOnEnterPlayMode
                    // Only auto-upload schemas to the Development project when testing in the Editor.
                    && ProjectSettings.instance.GetActiveProject(true) is { } activeProject)
                {
                    Schemas.Upload(new[] { activeProject}, updateSyncState: false); //, waitForUpdateSyncState: ShouldWait.Never);
                }
            }
            else if (stateChange is PlayModeStateChange.EnteredPlayMode && UploadOnEnterPlayMode)
            {
                Schemas.UpdateSyncStateAsync().Then(task =>
                {
                    using var logger = Log.GetLogger(typeof(SchemasUploadOperation));
                    logger.Error(Error.EditorUpdateSyncStateFailed, "Updating sync state failed: " + task.Exception?.Message);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public const string uploadOnEnterPlayModeKey = "Coherence.UploadOnEnterPlayMode";
        public const string uploadOnBuildKey = "Coherence.UploadOnBuild";
        public const string uploadAfterBakeKey = "Coherence.uploadAfterBake";

        /// <summary>
        /// Logs out the current user from the Online Dashboard.
        /// </summary>
        public static void Logout() => PortalLogin.Logout();

        /// <summary>
        /// Removes the data related to the organization and the project of the current user.
        /// </summary>
        public static void ClearOrganizationData() => PortalLogin.AssociateOrganization(null);

        public static bool UploadOnEnterPlayMode
        {
            get => EditorPrefs.GetBool(uploadOnEnterPlayModeKey, false);
            set => EditorPrefs.SetBool(uploadOnEnterPlayModeKey, value);
        }

        public static bool UploadAfterBake
        {
            get => EditorPrefs.GetBool(uploadAfterBakeKey, false);
            set => EditorPrefs.SetBool(uploadAfterBakeKey, value);
        }

        internal static UploadAfterBakeOptions UploadAfterBakeFlags
        {
            get => (UploadAfterBakeOptions)EditorPrefs.GetInt(uploadAfterBakeKey, 0);
            set => EditorPrefs.SetInt(uploadAfterBakeKey, (int)value);
        }

        public static bool UploadOnBuild
        {
            get => EditorPrefs.GetBool(uploadOnBuildKey, false);
            set => EditorPrefs.SetBool(uploadOnBuildKey, value);
        }
        /// <summary>
        /// Determines the state of synchronicity against the Online Dashboard.
        /// </summary>
        public static Schemas.SyncState SyncState => Schemas.state;

        /// <summary>
        /// List of schema states the portal knows about.
        /// </summary>
        public static Schemas.SchemaState[] RemoteSchemaStates => Schemas.RemoteSchemaStates;

        /// <summary>
        /// List of schema IDs the portal knows about.
        /// </summary>
        public static string[] RemoteSchemaIDs => Schemas.RemoteSchemaIDs;

        /// <summary>
        /// Determines if the schemas found in the project are in sync with the ones found in the Online Dashboard.
        /// </summary>
        public static bool InSync => SyncState == Schemas.SyncState.InSync;

        public static bool LocalSchemaFoundInPortal => BakeUtil.HasSchemaID && Array.IndexOf(RemoteSchemaIDs, BakeUtil.SchemaID) != -1;

        /// <summary>
        /// Uploads schemas to the portal.
        /// </summary>
        /// <note>
        /// This method communicates with the Online Dashboard through HTTP synchronously.
        /// </note>
        /// <returns>
        /// <see langword="true"/> if the operation succeeds, <see langword="false"/> otherwise.
        /// </returns>
        public static bool UploadSchemas() => UploadSchemas(true, ShouldWait.InBatchMode);

        internal static bool UploadSchemas(bool updateSyncState, ShouldWait waitForUpdateSyncState)
            => Schemas.UploadActive(InteractionMode.AutomatedAction, null, updateSyncState: updateSyncState, waitForUpdateSyncState: waitForUpdateSyncState);

        public static bool CanCommunicateWithPortal => !string.IsNullOrEmpty(ProjectSettings.instance.PortalToken) || !string.IsNullOrEmpty(ProjectSettings.instance.LoginToken);

        internal static bool OrgAndAtLeastOneProjectIsSet => OrgIsSet && AtLeastOneProjectIsSet;
        internal static bool OrgIsSet => PortalLogin.organizations.Any(x => x.id == ProjectSettings.instance.OrganizationId);
        internal static bool AtLeastOneProjectIsSet => ProjectSettings.instance.GetValidAndDistinctProjects().Any();

        /// <summary>
        /// Returns <see langword="true"/> if <see cref="RuntimeSettings.OrganizationID"/> has a value that is found among
        /// <see cref="PortalLogin.organizations"/> and <see cref="ProjectSettings.GetActiveProjectId"/> returns a value
        /// that is found among the projects of the active organization; otherwise, <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// NOTE: Always returns <see langword="false"/> if not logged in or if <see cref="PortalLogin.FetchOrgs"/>
        /// has not been called yet or has not completed yet.
        /// </remarks>
        public static bool OrgAndProjectIsSet
        {
            get
            {
                var activeProjectId = ProjectSettings.instance.GetActiveProjectId();
                if (string.IsNullOrEmpty(activeProjectId))
                {
                    return false;
                }

                if (PortalLogin.organizations.FirstOrDefault(x => x.id == ProjectSettings.instance.OrganizationId) is not { } activeOrganization)
                {
                    return false;
                }

                foreach (var project in activeOrganization.projects)
                {
                    if (string.Equals(project.id, activeProjectId))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Flags]
        internal enum UploadAfterBakeOptions
        {
            None = 0,
            Development = 1,
            Release = 2
        }
    }
}
