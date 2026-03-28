// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using Connection;
    using Log;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;
    using PackageInfo = UnityEditor.PackageManager.PackageInfo;
    using System.Linq;

    internal class PortalRequest : UnityWebRequest, IDisposable
    {
        internal class HeaderInfo
        {
            public string SDKVersion;
            public string LoginToken;
            public string ProjectPortalToken;
            public string SettingsPortalToken;
            public string OrgId;
            public string ProjectId;

            public static HeaderInfo GetDefault([MaybeNull] ProjectInfo project)
            {
                var packageInfo = PackageInfo.FindForAssetPath(Paths.packageManifestPath);
                var headerInfo = new HeaderInfo
                {
                    SDKVersion = packageInfo?.version,
                    LoginToken = ProjectSettings.instance.LoginToken,
                    SettingsPortalToken = ProjectSettings.instance.PortalToken,
                    OrgId = ProjectSettings.instance.OrganizationId,
                    ProjectPortalToken = project?.portal_token,
                    ProjectId = project?.id,
                };

                return headerInfo;
            }
        }

        private const string RequestIDHeader = "X-Coherence-Request-ID";
        private const string ProjectDeletedHeader = "X-Coherence-Project-Deleted";
        private const string CreditLimitExceededHeader = "X-Coherence-Credit-Limit-Exceeded";
        private const string MinVersionHeader = "X-Coherence-Client-Minimum-Version";

        private static readonly RequestIdSource IdSource = new RequestIdSource();
        private static readonly LazyLogger Logger = Log.GetLazyLogger<PortalRequest>();

        private UnityWebRequestAsyncOperation op;
        private bool logResults;
        private bool autoDispose;
        private bool disposed;
        private readonly string requestId;
        private readonly string path;
        private readonly Dictionary<string, string> headers = new Dictionary<string, string>();

        /// <param name="logResults"> Should results of the operation be logged to the Console automatically when it completes? </param>
        /// <param name="autoDispose"> Should the request object be disposed automatically when the operation completes? </param>
        internal static bool TryCreate(string path, string organizationID, ProjectInfo project, string method, bool autoDispose, bool logResults, [NotNullWhen(true), MaybeNullWhen(false)] out PortalRequest request, [NotNullWhen(false), MaybeNullWhen(true)] out string error)
        {
            if (Endpoints.TryGet(path, organizationID, project.id, out var pathEndpoint, out error))
            {
                request = new PortalRequest(path, method, logResults: logResults, autoDispose: autoDispose, url: Endpoints.OnlineDashboard + pathEndpoint, headerInfo: HeaderInfo.GetDefault(project));
                error = null;
                return true;
            }

            request = default;
            return false;
        }

        public PortalRequest(string path, string method, bool autoDispose, bool logResults = true) : this(path, method, ProjectSettings.instance.GetActiveProject(), autoDispose: autoDispose, logResults: logResults) { }

        public PortalRequest(string path, string method, [MaybeNull] ProjectInfo project, bool autoDispose, bool logResults = true) : this(path, method, autoDispose: autoDispose, logResults: logResults, Endpoints.OnlineDashboard + Endpoints.Get(path, project?.id ?? ""), HeaderInfo.GetDefault(project)) { }

        internal PortalRequest(string path, string method, bool autoDispose, bool logResults, string url, HeaderInfo headerInfo) : base(url, method)
        {
            this.path = path;
            this.logResults = logResults;
            this.autoDispose = autoDispose;
            requestId = IdSource.Next();

            if (!string.IsNullOrEmpty(headerInfo.LoginToken))
            {
                SetRequestHeader("X-Coherence-Sdk-Token", headerInfo.LoginToken);
            }
            else if (!string.IsNullOrEmpty(headerInfo.ProjectPortalToken))
            {
                SetRequestHeader("X-Coherence-Portal-Token", headerInfo.ProjectPortalToken);
            }
            else if (!string.IsNullOrEmpty(headerInfo.SettingsPortalToken))
            {
                SetRequestHeader("X-Coherence-Portal-Token", headerInfo.SettingsPortalToken);
            }

            if (!string.IsNullOrEmpty(headerInfo.OrgId))
            {
                SetRequestHeader("X-Coherence-Organization-Id", headerInfo.OrgId);
            }

            if (!string.IsNullOrEmpty(headerInfo.ProjectId))
            {
                SetRequestHeader("X-Coherence-Project-Id", headerInfo.ProjectId);
            }

            if (!string.IsNullOrEmpty(headerInfo.SDKVersion))
            {
                SetRequestHeader("X-Coherence-Client", "unity-sdk-v" + headerInfo.SDKVersion);
            }

            SetRequestHeader(RequestIDHeader, requestId);

            if (method is "POST" or "PUT")
            {
                SetRequestHeader("Content-Type", "application/json");
            }
        }

        public new void SetRequestHeader(string name, string value)
        {
            headers.Add(name, value);
            base.SetRequestHeader(name, value);
        }

        public UnityWebRequestAsyncOperation SendWebRequest(Action<AsyncOperation> callback = null)
        {
            Logger.Debug($"Request",
                         ("requestID", requestId),
                         ("url", url),
                         ("method", method),
                         ProjectSettings.instance.UseCustomEndpoints ? ("endpoint", Endpoints.OnlineDashboard) : ("", ""),
                         ("headers", $"[{string.Join(", ", headers?.Select(kv => $"{kv.Key}: {kv.Value}") ?? Array.Empty<string>())}]"));

            op = base.SendWebRequest();

            if (callback is not null || logResults || autoDispose)
            {
                op.completed += x =>
                {
                    try
                    {
                        try
                        {
                            if (logResults)
                            {
                                OnCompleted(x);
                            }
                        }
                        finally
                        {
                            callback?.Invoke(x);
                        }
                    }
                    finally
                    {
                        if (autoDispose)
                        {
                            Dispose();
                        }
                    }
                };
            }

            return op;
        }

        public new void Dispose() => ((IDisposable)this).Dispose();

        void IDisposable.Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            try
            {
                // If the object was disposed before results have been logged, log the results now.
                // This can happen because UnityWebRequestAsyncOperation.isDone can become true before the completed callback of the SendWebRequest is invoked.
                if (logResults && op is not null && op.isDone)
                {
                    OnCompleted(op);
                }
            }
            finally
            {
                base.Dispose();
                disposeUploadHandlerOnDispose = false;
                disposeCertificateHandlerOnDispose = false;
                disposeDownloadHandlerOnDispose = false;
            }
        }

        private void OnCompleted(AsyncOperation op)
        {
            if (!logResults)
            {
                return;
            }

            logResults = false;
            LogResult();

            var projDeleted = GetResponseHeader(ProjectDeletedHeader);
            var creditLimitExceeded = GetResponseHeader(CreditLimitExceededHeader);

            if (!string.IsNullOrEmpty(projDeleted))
            {
                Logger.Warning(Warning.EditorPortalRequestProjectDeleted);

                foreach (var conditionalProject in ProjectSettings.instance.MultipleProjects)
                {
                    if (conditionalProject.Project.id == RuntimeSettings.Instance.ProjectID)
                    {
                        conditionalProject.Project = new();
                    }
                }

                PortalLogin.AssociateProject(null);
                return;
            }

            if (!string.IsNullOrEmpty(creditLimitExceeded))
            {
                Logger.Warning(Warning.EditorPortalRequestCreditsExceeded);
            }

            // First check if there's an issue with the version of the SDK we are using.
            var minVersion = GetResponseHeader(MinVersionHeader);
            if (minVersion != null)
            {
                PackageInfo packageInfo = PackageInfo.FindForAssetPath(Paths.packageManifestPath);
                if (responseCode == 403)
                {
                    var message = $"The coherence SDK version you are using ({packageInfo.version}) is no longer supported. Please upgrade to {minVersion} or newer.";
                    Logger.Error(Error.EditorPortalRequestSDKUnsupported, message);
                    _ = EditorUtility.DisplayDialog("Unsupported coherence version", message, "Ok");
                    return;
                }

                Logger.Warning(Warning.EditorPortalRequestSDKDeprecated,
                    $"The coherence SDK version you are using ({packageInfo.version}) is deprecated. Please upgrade to {minVersion} or newer.");
            }
            else
            {
                // 423 here means that the feature wasn't enabled
                if (responseCode == 423)
                {
                    var message = "Feature not enabled. Visit the Online Dashboard to enable it.";
                    Logger.Error(Error.EditorPortalRequestFeatureNotSupported, message);
                    _ = EditorUtility.DisplayDialog("Feature not enabled", message, "Ok");
                }
                // If you upgrade old project with login info cached it needs to be reset
                else if (responseCode == 401 && downloadHandler.text.Contains("ERR_TOKEN_MISSING"))
                {
                    Logger.Warning(Warning.EditorPortalRequestMissingToken);
                }
            }
        }

        private void LogResult()
        {
            if (result == Result.Success)
            {
                Logger.Debug($"Response",
                    ("requestID", requestId),
                    ("responseID", GetResponseHeader(RequestIDHeader)),
                    ("url", url),
                    ("method", method),
                    ("statusCode", responseCode),
                    ProjectSettings.instance.UseCustomEndpoints ? ("endpoint", Endpoints.OnlineDashboard) : ("", ""),
                    ("body", downloadHandler?.text));
            }
            else
            {
                Logger.Warning(Warning.EditorPortalRequestFailed,
                    $"Request failed",
                    ("requestID", requestId),
                    ("path", path),
                    ("method", method),
                    ("statusCode", responseCode),
                    ("error", error),
                    ("result", result),
                    ProjectSettings.instance.UseCustomEndpoints ? ("endpoint", Endpoints.OnlineDashboard) : ("", ""),
                    ("body", downloadHandler?.text));
            }
        }
    }
}
