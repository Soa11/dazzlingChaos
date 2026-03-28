// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Editor.Portal
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using System.Threading.Tasks;
    using Common;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;
    using Log = Coherence.Log.Log;
    using Error = Log.Error;

    /// <summary>
    /// Represents an operation for uploading <see cref="Schemas"/> to the Online Dashboard.
    /// <note>
    /// Communication with the Online Dashboard happens through HTTP synchronously.
    /// </note>
    /// </summary>
    internal sealed class SchemasUploadOperation
    {
        private const int UpdateSyncStateTimeoutMilliseconds = 5000;
        private const float UpdateSyncStateTimeoutSeconds = UpdateSyncStateTimeoutMilliseconds / 1000f;

        /// <summary>
        /// Specifies the different reasons for which an operation to upload schemas to the cloud can fail.
        /// </summary>
        internal enum FailReason
        {
            None,
            AbortedByUser,

            MissingSchemaID,
            MissingRuntimeSettings,
            MissingPortalAndLoginTokens,
            MissingOrganizationID,
            InvalidOrganizationID,
            MissingProjectID,

            ProtocolError,
            ConnectionError,
            DataProcessingError,
            RequestCreationFailed
        }

        /// <summary>
        /// Represents the result of an operation to upload schemas to the cloud.
        /// </summary>
        internal sealed class Result
        {
            public static readonly Result Success = new(FailReason.None, null);
            public static readonly Result AbortedByUser = new(FailReason.AbortedByUser, null);

            private Result(FailReason failReason, string webRequestError)
            {
                FailReason = failReason;
                WebRequestError = webRequestError;
            }

            public FailReason FailReason { get; }

            [MaybeNull]
            public string WebRequestError { get; }

            public static Result Failed(FailReason failReason) => new(failReason, null);
            public static Result ConnectionError([DisallowNull] string error) => new(FailReason.ConnectionError, error);
            public static Result ProtocolError([DisallowNull] string error) => new(FailReason.ProtocolError, error);
            public static Result DataProcessingError([DisallowNull] string error) => new(FailReason.DataProcessingError, error);
            public static Result RequestCreationFailed([DisallowNull] string error) => new(FailReason.RequestCreationFailed, error);
        }

        public SchemasUploadOperation(Schemas schemas, string portalToken, string loginToken, string organizationID, ProjectInfo project)
        {
            Schemas = schemas;
            PortalToken = portalToken;
            LoginToken = loginToken;
            OrganizationID = organizationID;
            Project = project;
        }

        public Schemas Schemas { get; }
        public string PortalToken { get; }
        public string LoginToken { get; }
        public string OrganizationID { get; }
        public ProjectInfo Project { get; }
        public string ProjectID => Project.id;
        public string ProjectName => Project.name;

        /// <summary>
        /// Attempts to upload the schemas to the cloud.
        /// </summary>
        /// <param name="interactionMode">Indicates whether the user should be prompted for confirmation or performed automatically.</param>
        /// <param name="waitUntilSchemaSyncStateUpdated">Should thread be blocked until schema sync state is updated?</param>
        /// <returns> Object describing the result of the operation. </returns>
        internal Result Upload(InteractionMode interactionMode = InteractionMode.AutomatedAction, bool updateSyncState = true, ShouldWait waitForUpdateSyncState = ShouldWait.Never)
        {
            if (string.IsNullOrEmpty(PortalToken) && string.IsNullOrEmpty(LoginToken))
            {
                return Result.Failed(FailReason.MissingPortalAndLoginTokens);
            }

            if (string.IsNullOrEmpty(OrganizationID))
            {
                return Result.Failed(FailReason.MissingOrganizationID);
            }

            if (PortalLogin.TryValidateOrganizationID(OrganizationID, out var idIsValid) && !idIsValid)
            {
                return Result.Failed(FailReason.InvalidOrganizationID);
            }

            if (string.IsNullOrEmpty(ProjectID))
            {
                return Result.Failed(FailReason.MissingProjectID);
            }

            if (string.IsNullOrEmpty(Schemas.id))
            {
                return Result.Failed(FailReason.MissingSchemaID);
            }

            if (!PortalRequest.TryCreate(Endpoints.schemasPath, OrganizationID, Project, "POST", autoDispose: false, logResults: true, out var portalRequest, out var error))
            {
                return Result.RequestCreationFailed(error);
            }

            try
            {
                if (!Application.isBatchMode && interactionMode == InteractionMode.UserAction
                                             && !EditorUtility.DisplayDialog("Upload Schemas Now?",
                                                 $"Local Schema ID: {Schemas.id}\nProject Name: {ProjectName}\nProject ID: {ProjectID}", "Upload",
                                                 "Cancel"))
                {
                    return Result.AbortedByUser;
                }

                var body = JsonUtility.ToJson(Schemas);
                var bodyRaw = Encoding.UTF8.GetBytes(body);
                portalRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                portalRequest.downloadHandler = new DownloadHandlerBuffer();
                portalRequest.disposeUploadHandlerOnDispose = true;
                portalRequest.disposeDownloadHandlerOnDispose = true;

                _ = portalRequest.SendWebRequest();

                while (!portalRequest.isDone)
                {
                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Portal", "Uploading schemas...", portalRequest.uploadProgress))
                    {
                        EditorUtility.ClearProgressBar();
                        portalRequest.Abort();
                        return Result.AbortedByUser;
                    }
                }

                EditorUtility.ClearProgressBar();

                switch (portalRequest.result)
                {
                    case UnityWebRequest.Result.ProtocolError:
                        return Result.ProtocolError(portalRequest.error);
                    case UnityWebRequest.Result.ConnectionError:
                        return Result.ConnectionError(portalRequest.error);
                    case UnityWebRequest.Result.DataProcessingError:
                        return Result.DataProcessingError(portalRequest.error);
                }

                _ = ProjectSettings.instance.RehashActiveSchemas();
                Analytics.Capture(Analytics.Events.UploadSchema);

                if (updateSyncState)
                {
                    var updateSyncStateTask = Schemas.UpdateSyncStateAsync();
                    if (waitForUpdateSyncState.ShouldWait())
                    {
                        if (!updateSyncStateTask.Wait(millisecondsTimeout: UpdateSyncStateTimeoutMilliseconds))
                        {
                            using var logger = Log.GetLogger(typeof(SchemasUploadOperation));
                            logger.Error(Error.EditorUpdateSyncStateTimeout, $"Updating sync state failed to complete within {UpdateSyncStateTimeoutSeconds} seconds.");
                        }
                    }
                    else
                    {
                        updateSyncStateTask.Then(task =>
                        {
                            using var logger = Log.GetLogger(typeof(SchemasUploadOperation));
                            logger.Error(Error.EditorUpdateSyncStateFailed, "Updating sync state failed: " + task.Exception?.Message);
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }

                EditorUtility.ClearProgressBar();
                return Result.Success;
            }
            finally
            {
                portalRequest.Dispose();
            }
        }


        /// <summary>
        /// Attempts to upload the schemas to the cloud.
        /// </summary>
        /// <param name="interactionMode">Indicates whether the user should be prompted for confirmation or performed automatically.</param>
        /// <returns> Object describing the result of the operation. </returns>
        internal async Task<Result> UploadAsync(InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            if (string.IsNullOrEmpty(PortalToken) && string.IsNullOrEmpty(LoginToken))
            {
                return Result.Failed(FailReason.MissingPortalAndLoginTokens);
            }

            if (string.IsNullOrEmpty(OrganizationID))
            {
                return Result.Failed(FailReason.MissingOrganizationID);
            }

            if (PortalLogin.TryValidateOrganizationID(OrganizationID, out var idIsValid) && !idIsValid)
            {
                return Result.Failed(FailReason.InvalidOrganizationID);
            }

            if (string.IsNullOrEmpty(ProjectID))
            {
                return Result.Failed(FailReason.MissingProjectID);
            }

            if (string.IsNullOrEmpty(Schemas.id))
            {
                return Result.Failed(FailReason.MissingSchemaID);
            }

            if (!PortalRequest.TryCreate(Endpoints.schemasPath, OrganizationID, Project, "POST", autoDispose: false, logResults: true, out var portalRequest, out var error))
            {
                return Result.RequestCreationFailed(error);
            }

            try
            {
                if (!Application.isBatchMode && interactionMode == InteractionMode.UserAction
                                             && !EditorUtility.DisplayDialog("Upload Schemas Now?",
                                                 $"Local Schema ID: {Schemas.id}\nProject Name: {ProjectName}\nProject ID: {ProjectID}", "Upload",
                                                 "Cancel"))
                {
                    return Result.AbortedByUser;
                }

                var body = JsonUtility.ToJson(Schemas);
                var bodyRaw = Encoding.UTF8.GetBytes(body);
                portalRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                portalRequest.downloadHandler = new DownloadHandlerBuffer();
                portalRequest.disposeUploadHandlerOnDispose = true;
                portalRequest.disposeDownloadHandlerOnDispose = true;

                _ = portalRequest.SendWebRequest();

                while (!portalRequest.isDone)
                {
                    if (!Application.isBatchMode)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Portal", "Uploading schemas...", portalRequest.uploadProgress))
                        {
                            EditorUtility.ClearProgressBar();
                            portalRequest.Abort();
                            return Result.AbortedByUser;
                        }

                        await Task.Yield();
                    }
                }

                EditorUtility.ClearProgressBar();

                switch (portalRequest.result)
                {
                    case UnityWebRequest.Result.ProtocolError:
                        return Result.ProtocolError(portalRequest.error);
                    case UnityWebRequest.Result.ConnectionError:
                        return Result.ConnectionError(portalRequest.error);
                    case UnityWebRequest.Result.DataProcessingError:
                        return Result.DataProcessingError(portalRequest.error);
                }

                Analytics.Capture(Analytics.Events.UploadSchema);
                var updateSyncStateTask = Schemas.UpdateSyncStateAsync();

                while (!updateSyncStateTask.IsCompleted)
                {
                    if (!Application.isBatchMode)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Portal", "Updating sync state...", 0f))
                        {
                            EditorUtility.ClearProgressBar();
                            return Result.AbortedByUser;
                        }

                        await Task.Yield();
                    }
                }

                EditorUtility.ClearProgressBar();
                return Result.Success;
            }
            finally
            {
                portalRequest.Dispose();
            }
        }
    }
}
