// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using Portal;
    using Toolkit;
    using UnityEditor;
    using UnityEngine;

    [HubModule(Priority = 70)]
    public class CloudModule : HubModule
    {
        public static class ModuleGUIContents
        {
            public static readonly GUIContent Portal = EditorGUIUtility.TrTextContent("Online Dashboard", ExternalLinks.OnlineDashboardUrl);
            public static readonly GUIContent LogOut = EditorGUIUtility.TrTextContent("Logout", "Logout");
            public static readonly GUIContent Upload = EditorGUIUtility.TrTextContent("Upload Schema to Cloud", "Upload active schemas to coherence Cloud");
            public static readonly GUIContent OnlineBenefits = new("Make sure to log in to the Online Dashboard to unlock the full potential of coherence. " +
                                                                   "\nCreating an online project will allow you to run Simulators in the cloud, upload builds and share your work with the rest of the world");
            public static readonly GUIContent RefreshSubscription = new("Please refresh organizations above to see subscription info");
            public static readonly GUIContent FetchingSubscriptionData = new("Fetching subscription data...");

            public static readonly GUIContent Account = new("Account");
            public static readonly GUIContent Schemas = new("Schemas");
            public static readonly GUIContent ShareBuild = new("Share build");
            public static readonly GUIContent SelectOrgAndProject = new("You are logged in but need to select an organization and a project.");
            public static readonly GUIContent OnlineResources = new("Online Resources");
        }

        public override string ModuleName => "Cloud";

        public override HelpSection Help => new()
        {
            title = new GUIContent("Getting the most out of coherence"),
            content = ModuleGUIContents.OnlineBenefits
        };

        public override void OnModuleEnable()
        {
            base.OnModuleEnable();

            if (!string.IsNullOrEmpty(ProjectSettings.instance.OrganizationId))
            {
                PortalLoginDrawer.RefreshSubscriptionInfo();
            }

            GameBuildWindow.RestoreSavedBuildSettings();
        }

        public override void OnGUI()
        {
            base.OnGUI();
            CoherenceHubLayout.DrawSection(ModuleGUIContents.Account, DrawAccount, DrawDashboardLink);
            var enabled = GUI.enabled;
            if (!PortalUtil.OrgAndAtLeastOneProjectIsSet)
            {
                EditorGUILayout.HelpBox("You must be logged in and have an organization and project selected before you can upload schemas and share builds.", MessageType.Warning);
                GUI.enabled = false;
            }
            EditorGUILayout.Space();
            CoherenceHubLayout.DrawSection(ModuleGUIContents.Schemas, DrawSchemasInPortal);
            CoherenceHubLayout.DrawSection(ModuleGUIContents.ShareBuild, DrawShareBuildGUI);
            GUI.enabled = enabled;
            CoherenceHubLayout.DrawSection(ModuleGUIContents.OnlineResources, DrawLinks);

            void DrawSchemasInPortal() => SharedModuleSections.DrawSchemasInPortal(Host);
            void DrawShareBuildGUI() => GameBuildWindow.DrawShareBuildGUI(Host);
        }

        public void DrawDashboardLink()
        {
            var url = SharedModuleSections.GetDashboardUrl(PortalLoginDrawer.GetSelectedOrganization()?.slug);
            var content = ModuleGUIContents.Portal;
            content.tooltip = url;
            CoherenceHubLayout.DrawLink(content, url);
        }

        public void DrawAccount()
        {
            bool loginToken = !string.IsNullOrEmpty(ProjectSettings.instance.LoginToken);

            if (!loginToken)
            {
                DrawLoggedOutOptions();
            }
            else
            {
                if (PortalLoginDrawer.GetSelectedOrganization() == null)
                {
                    CoherenceHubLayout.DrawMessageArea(ModuleGUIContents.SelectOrgAndProject);
                }

                EditorGUILayout.Separator();
                using (new EditorGUILayout.HorizontalScope())
                {
                    CoherenceHubLayout.DrawBoldLabel(new GUIContent(ProjectSettings.instance.Email), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (CoherenceHubLayout.DrawButton(ModuleGUIContents.LogOut))
                    {
                        PortalLogin.Logout();
                    }
                }
            }

            var hasOrgID = !string.IsNullOrEmpty(ProjectSettings.instance.OrganizationId);

            if (loginToken)
            {
                var labelWidth = PortalLoginDrawer.DrawOrganizationOptions();

                if (PortalUtil.OrgIsSet)
                {
                    PortalLoginDrawer.DrawProjectOptions(labelWidth);
                    EditorGUILayout.Separator();
                }
            }

            if (loginToken && hasOrgID)
            {
                var r = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, .3f));

                EditorGUILayout.Separator();

                DrawSubscription();
            }
        }

        private void DrawLegacySubscription(OrganizationSubscription subscription)
        {
                CoherenceHubLayout.DrawLabel(new GUIContent($"Plan: {subscription.product_name}"));

                var creditsLeft = subscription.credits_included - subscription.credits_consumed;
                var percentageUsed = 1f - creditsLeft / (float)subscription.credits_included;
                var billingUrl = SharedModuleSections.GetOrganizationBillingUrl(PortalLoginDrawer.GetSelectedOrganization()?.slug);

                CoherenceHubLayout.DrawLabel(new GUIContent($"Credits: {Mathf.Max(creditsLeft, 0)} / {subscription.credits_included}"));
                if (subscription.credits_consumed == 0)
                {
                }
                else if (creditsLeft > 0)
                {
                    var iconName = percentageUsed >= 0.75f ? "Warning" : "Info";
                    CoherenceHubLayout.DrawBoldLabel(EditorGUIUtility.TrTextContentWithIcon($"You've used {Mathf.CeilToInt(percentageUsed * 100)}% of your monthly credits.", iconName));

                    EditorGUILayout.Space();

                    if (CoherenceHubLayout.DrawButtonBlue(EditorGUIUtility.TrTextContent("Manage Cloud plan")))
                    {
                        Application.OpenURL(billingUrl);
                    }
                }
                else if (creditsLeft <= 0)
                {
                    CoherenceHubLayout.DrawBoldLabel(EditorGUIUtility.TrTextContentWithIcon("You've used all your monthly credits.", "Error"));

                    if (creditsLeft < 0)
                    {
                        if (subscription.product_name != "Free")
                        {
                            CoherenceHubLayout.DrawBoldLabel(new GUIContent($"Credit Overage: {Mathf.Abs(creditsLeft)}"));
                            CoherenceHubLayout.DrawBoldLabel(EditorGUIUtility.TrTextContent("Overage will be charged on the monthly bill."));
                        }
                    }

                    EditorGUILayout.Space();

                    if (CoherenceHubLayout.DrawButtonBlue(EditorGUIUtility.TrTextContent("Upgrade Cloud plan")))
                    {
                        Application.OpenURL(billingUrl);
                    }
                }

        }

        public void DrawSubscription()
        {
            var orgSubscription = PortalLogin.OrgSubscription;
            if (PortalLogin.GetSubscriptionDataRequest != null)
            {
                CoherenceHubLayout.DrawLabel(ModuleGUIContents.FetchingSubscriptionData);
            }
            else if (orgSubscription != null)
            {
                if (orgSubscription.version <= 2)
                {
                    DrawLegacySubscription(orgSubscription);
                }
                else
                {
                    EditorGUILayout.LabelField("Plan", orgSubscription.subscription.name);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Credits", orgSubscription.credits.ToString(), ContentUtils.GUIStyles.labelNoExpand);
                    if (GUILayout.Button("Top Up", ContentUtils.GUIStyles.fitButton))
                    {
                        Application.OpenURL(orgSubscription.buy_credits_url);
                    }
                    EditorGUILayout.EndHorizontal();
                    if (orgSubscription.credits < 0)
                    {
                        EditorGUILayout.HelpBox("You've run out of credits. To continue using coherence Cloud, top up or upgrade your plan.", MessageType.Error);
                    }
                    if (GUILayout.Button("Manage Subscription", ContentUtils.GUIStyles.bigButton))
                    {
                        Application.OpenURL(orgSubscription.manage_subscription_url);
                    }
                }
            }
            else
            {
                CoherenceHubLayout.DrawLabel(ModuleGUIContents.RefreshSubscription);
            }
        }

        private void DrawLoggedOutOptions()
        {
            CoherenceHubLayout.DrawInfoAreaWithBulletPoints(
                "You are not logged in. Please log in, select an organization and project to:",
                "Upload schemas or builds.", "Access your project tokens.", "Access the Online Dashboard");

            EditorGUILayout.Separator();

            if (CoherenceHubLayout.DrawButtonBlue("Signup / Login"))
            {
                Repaint();
                PortalLogin.Login(Repaint);
            }
        }

        private static void DrawLinks()
        {
            CoherenceHubLayout.DrawLink("coherence Cloud", DocumentationKeys.CloudService);
            CoherenceHubLayout.DrawLink("CoherenceCloudLogin Unity Component", DocumentationKeys.CoherenceCloudLogin);
            CoherenceHubLayout.DrawLink("Cloud API", DocumentationKeys.CloudApi);
            CoherenceHubLayout.DrawLink("UploadBuildToCoherence API", DocumentationKeys.UploadBuildToCoherence);
        }
    }
}
