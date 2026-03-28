// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests.Portal
{
    using Coherence.Tests;
    using Coherence.Editor.Portal;
    using NUnit.Framework;

    public class RequestTests : CoherenceTest
    {
        [Test]
        [TestCase("LoginToken", null, null, "LoginToken", "X-Coherence-Sdk-Token")]
        [TestCase(null, "ProjectPortalToken", null, "ProjectPortalToken", "X-Coherence-Portal-Token")]
        [TestCase(null, null, "SettingsPortalToken", "SettingsPortalToken", "X-Coherence-Portal-Token")]
        [Description("Ensure that the correct token is used in the request headers based on priority.")]
        public void Correct_Login_Token_Used(string loginToken, string projectPortalToken, string settingsPortalToken, string expectedHeaderValue, string headerName)
        {
            var headerInfo = new PortalRequest.HeaderInfo()
            {
                SDKVersion = "",
                LoginToken = loginToken,
                ProjectPortalToken = projectPortalToken,
                SettingsPortalToken = settingsPortalToken,
                OrgId = "",
                ProjectId = "",
            };

            var portalRequest = new PortalRequest("some/path", "GET", false, false, "my/url", headerInfo);
            Assert.That(portalRequest.GetRequestHeader(headerName), Is.EqualTo(expectedHeaderValue));
        }

        [Test]
        [Description("Ensure that the header contains OrgId if provided.")]
        public void Header_Correctly_Set_If_OrgId_Provided()
        {
            var headerInfo = new PortalRequest.HeaderInfo()
            {
                SDKVersion = "",
                LoginToken = "",
                ProjectPortalToken = "",
                SettingsPortalToken = "",
                OrgId = "OrgIdValue",
                ProjectId = "",
            };

            var portalRequest = new PortalRequest("some/path", "GET", false, false, "my/url", headerInfo);
            Assert.That(portalRequest.GetRequestHeader("X-Coherence-Organization-Id"), Is.EqualTo(headerInfo.OrgId));
        }

        [Test]
        [Description("Ensure that the header contains ProjectId if provided.")]
        public void Header_Correctly_Set_If_ProjectId_Provided()
        {
            var headerInfo = new PortalRequest.HeaderInfo()
            {
                SDKVersion = "",
                LoginToken = "",
                ProjectPortalToken = "",
                SettingsPortalToken = "",
                OrgId = "",
                ProjectId = "ProjectIdValue",
            };

            var portalRequest = new PortalRequest("some/path", "GET", false, false, "my/url", headerInfo);
            Assert.That(portalRequest.GetRequestHeader("X-Coherence-Project-Id"), Is.EqualTo(headerInfo.ProjectId));
        }

        [Test]
        [Description("Ensure that the header contains SDK version if provided.")]
        public void Header_Correctly_Set_If_SDKVersion_Provided()
        {
            var headerInfo = new PortalRequest.HeaderInfo()
            {
                SDKVersion = "2.0.0",
                LoginToken = "",
                ProjectPortalToken = "",
                SettingsPortalToken = "",
                OrgId = "",
                ProjectId = "",
            };

            var portalRequest = new PortalRequest("some/path", "GET", false, false, "my/url", headerInfo);
            Assert.That(portalRequest.GetRequestHeader("X-Coherence-Client"), Is.EqualTo($"unity-sdk-v{headerInfo.SDKVersion}"));
        }

        [Test]
        [TestCase("")]
        [TestCase(null)]
        [Description("Ensure that headers are not set if the corresponding value for organization is missing.")]
        public void Header_Not_Set_If_OrgId_Missing(string organizationId)
        {
            var headerInfo = new PortalRequest.HeaderInfo()
            {
                SDKVersion = "",
                LoginToken = "",
                ProjectPortalToken = "",
                SettingsPortalToken = "",
                OrgId = organizationId,
                ProjectId = "",
            };

            var portalRequest = new PortalRequest("some/path", "GET", false, false, "my/url", headerInfo);
            Assert.That(portalRequest.GetRequestHeader("X-Coherence-Organization-Id"), Is.Null.Or.Empty);
        }

        [Test]
        [TestCase("")]
        [TestCase(null)]
        [Description("Ensure that headers are not set if the corresponding value for project is missing.")]
        public void Header_Not_Set_If_ProjectId_Missing(string projectId)
        {
            var headerInfo = new PortalRequest.HeaderInfo()
            {
                SDKVersion = "",
                LoginToken = "",
                ProjectPortalToken = "",
                SettingsPortalToken = "",
                OrgId = "",
                ProjectId = projectId,
            };

            var portalRequest = new PortalRequest("some/path", "GET", false, false, "my/url", headerInfo);
            Assert.That(portalRequest.GetRequestHeader("X-Coherence-Project-Id"), Is.Null.Or.Empty);
        }

        [Test]
        [TestCase("")]
        [TestCase(null)]
        [Description("Ensure that headers are not set if the corresponding value for sdk version is missing.")]
        public void Header_Not_Set_If_SDKVersion_Missing(string sdkVersion)
        {
            var headerInfo = new PortalRequest.HeaderInfo()
            {
                SDKVersion = sdkVersion,
                LoginToken = "",
                ProjectPortalToken = "",
                SettingsPortalToken = "",
                OrgId = "",
                ProjectId = "",
            };

            var portalRequest = new PortalRequest("some/path", "GET", false, false, "my/url", headerInfo);
            Assert.That(portalRequest.GetRequestHeader("X-Coherence-Client"), Is.Null.Or.Empty);
        }
    }
}
