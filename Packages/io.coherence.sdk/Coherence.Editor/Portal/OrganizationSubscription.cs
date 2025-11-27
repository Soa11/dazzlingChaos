// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System;
    using UnityEngine;
    using UnityEngine.Networking;

    [Serializable]
    internal class OrganizationSubscription
    {
        [Serializable]
        public class SubscriptionInfo
        {
#pragma warning disable 649
            public string name;
            public bool paid;
            public bool trialing;
            public long cancel_at;
#pragma warning restore 649
        }

#pragma warning disable 649
        public int version;
        public bool enterprise;
        public string product_name;
        public long end_date;
        public int credits_consumed;
        public int credits_included;
        public bool paid_tier;

        // pricing v3 fields
        public int credits;
        public SubscriptionInfo subscription;
        public string buy_credits_url;
        public string manage_subscription_url;
#pragma warning restore 649

        private OrganizationSubscription()
        {
        }

        /// <summary>
        /// Fetch the <see cref="OrganizationSubscription"/> for an organization asynchronously in a <see cref="PortalRequest"/> .
        /// </summary>
        /// <param name="organizationID">The organization ID.</param>
        /// <param name="onFetched">Action to call when the request is done.</param>
        /// <returns><c>null</c> if login token or organization ID is invalid, otherwise the <see cref="PortalRequest"/> created.</returns>
        public static PortalRequest FetchAsync(string organizationID, Action<OrganizationSubscription> onFetched)
        {
            if (string.IsNullOrEmpty(ProjectSettings.instance.LoginToken)
                || string.IsNullOrEmpty(organizationID))
            {
                return null;
            }

            var activeProject = ProjectSettings.instance.GetActiveProject();
            PortalRequest req = new PortalRequest(Endpoints.organizationSubscriptionPath, "GET", activeProject);
            req.downloadHandler = new DownloadHandlerBuffer();
            _ = req.SendWebRequest(_ => onFetched?.Invoke(req.result == UnityWebRequest.Result.Success
                ? JsonUtility.FromJson<OrganizationSubscription>(req.downloadHandler.text)
                : null));
            return req;
        }

    }
}

