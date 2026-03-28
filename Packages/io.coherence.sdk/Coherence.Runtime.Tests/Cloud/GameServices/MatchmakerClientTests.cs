// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests.Cloud.GameServices
{
    using System;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Common;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class MatchmakerClientTests : CoherenceTest
    {
        private const string ProjectId = nameof(ProjectId);
        private const string UniqueId = nameof(UniqueId);

        private readonly IRuntimeSettings runtimeSettings = new Mock<IRuntimeSettings>().Object;
        private readonly Mock<IPlayerAccountProvider> playerAccountProviderMock = new();
        private readonly Mock<IRequestFactoryInternal> requestFactoryMock = new();
        private MatchmakerClient matchmakerClient;
        private IPlayerAccountProvider playerAccountProvider;
        private CloudCredentialsPair cloudCredentialsPair = null;
        private AuthClient invalidClient;

        [Test]
        [TestCase(Result.InvalidCredentials, "Invalid credentials provided.")]
        [TestCase(Result.ServerError, "")]
        [Description("Ensures that MatchMakerException contains correct error code and message.")]
        public void Exception_Contains_Correct_ErrorCode_And_Message(Result result, string message)
        {
            var exception = new MatchMakerException(result, message);
            Assert.That(result, Is.EqualTo(exception.ErrorCode));
            Assert.That(message, Is.EqualTo(exception.Message));
        }

        [Test]
        [Description("Ensures that Match method throws MatchMakerException if AuthClient is not logged in.")]
        public void Match_Throws_If_AuthClient_Not_Logged_In()
        {
            var playerAccount = CreatePlayerAccount();
            playerAccountProviderMock.Setup(p => p.GetPlayerAccount(It.IsAny<LoginInfo>())).Returns(playerAccount);
            playerAccountProvider = playerAccountProviderMock.Object;
            var requestFactory = requestFactoryMock.Object;
            cloudCredentialsPair = CloudCredentialsFactory.ForClient(runtimeSettings, playerAccountProvider);
            invalidClient = AuthClient.ForPlayer(requestFactory, playerAccountProvider);

            matchmakerClient = new MatchmakerClient(cloudCredentialsPair.RequestFactory, invalidClient);
            Assert.That(
                () => matchmakerClient.Match("region", "team", "payload", Array.Empty<string>(), Array.Empty<string>()),
                Throws.TypeOf<MatchMakerException>());

            PlayerAccount.Unregister(playerAccount);
        }

        [Test]
        [Description("Ensures that Match method throws MatchMakerException if a server error occurs.")]
        public async Task Match_Throws_If_Server_Error_Happens()
        {
            // Login end point
            requestFactoryMock.Setup(r => r.SendRequestAsync("/login/guest", It.IsAny<string>(), It.IsAny<string>(),
                    null, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(CreateValidLoginResponse());

            // Match end point
            requestFactoryMock.Setup(r => r.SendRequestAsync("/match", It.IsAny<string>(), It.IsAny<string>(),
                    null, It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new RequestException(ErrorCode.TooManyRequests, 500, "Server error"));

            requestFactoryMock.SetupGet(r => r.IsReady).Returns(true);

            playerAccountProvider = playerAccountProviderMock.Object;
            var requestFactory = requestFactoryMock.Object;

            var newAuthClient = AuthClient.ForPlayer(requestFactory, playerAccountProvider);
            cloudCredentialsPair = new CloudCredentialsPair(newAuthClient, requestFactory);

            var cloudService = new CloudService(cloudCredentialsPair, null, null, null, null, null, null, null);
            var playerAccount = CreatePlayerAccount(cloudService);
            playerAccountProviderMock.Setup(p => p.GetPlayerAccount(It.IsAny<LoginInfo>())).Returns(playerAccount);
            playerAccountProviderMock.SetupGet(p => p.IsReady).Returns(true);

            var result = await cloudCredentialsPair.AuthClient.LoginAsGuest();
            Assert.That(result.LoggedIn, Is.True);

            matchmakerClient = new MatchmakerClient(cloudCredentialsPair.RequestFactory,
                (AuthClient)cloudCredentialsPair.AuthClient);
            Assert.That(
                () => matchmakerClient.Match("region", "team", "payload", Array.Empty<string>(), Array.Empty<string>()),
                Throws.TypeOf<MatchMakerException>());

            PlayerAccount.Unregister(playerAccount);
        }

        [Test]
        [Description("Ensures that Match method returns valid response if user is logged in.")]
        public async Task Match_Returns_Valid_Response_If_User_Logged_In()
        {
            // Login end point
            requestFactoryMock.Setup(r => r.SendRequestAsync("/login/guest", It.IsAny<string>(), It.IsAny<string>(),
                    null, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(CreateValidLoginResponse());

            // Match end point
            requestFactoryMock.Setup(r => r.SendRequestAsync("/match", It.IsAny<string>(), It.IsAny<string>(),
                    null, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(CreateValidMatchResponse());

            requestFactoryMock.SetupGet(r => r.IsReady).Returns(true);
            var requestFactory = requestFactoryMock.Object;

            playerAccountProvider = playerAccountProviderMock.Object;
            var newAuthClient = AuthClient.ForPlayer(requestFactory, playerAccountProvider);
            cloudCredentialsPair = new CloudCredentialsPair(newAuthClient, requestFactory);

            var cloudService = new CloudService(cloudCredentialsPair, null, null, null, null, null, null, null);
            var playerAccount = CreatePlayerAccount(cloudService);
            playerAccountProviderMock.Setup(p => p.GetPlayerAccount(It.IsAny<LoginInfo>())).Returns(playerAccount);
            playerAccountProviderMock.SetupGet(p => p.IsReady).Returns(true);

            var result = await cloudCredentialsPair.AuthClient.LoginAsGuest();
            Assert.That(result.LoggedIn, Is.True);

            matchmakerClient = new MatchmakerClient(cloudCredentialsPair.RequestFactory,
                (AuthClient)cloudCredentialsPair.AuthClient);
            var response =
                await matchmakerClient.Match("region", "team", "payload", Array.Empty<string>(), Array.Empty<string>());
            Assert.That(response, Is.Not.Null);
            Assert.That(response.MatchId, Is.EqualTo(nameof(MatchResponse.MatchId)));

            PlayerAccount.Unregister(playerAccount);
        }

        private PlayerAccount CreatePlayerAccount(CloudService cloudService = null)
        {
            var loginInfo = LoginInfo.ForGuest(ProjectId, UniqueId, false);
            return new PlayerAccount(loginInfo, UniqueId, ProjectId, cloudService);
        }

        private string CreateValidLoginResponse()
        {
            var response = new LoginResponse()
            {
                sessionToken = nameof(LoginResponse.sessionToken),
                Username = nameof(LoginResponse.Username),
            };

            return JsonConvert.SerializeObject(response);
        }

        private string CreateValidMatchResponse()
        {
            var response = new MatchResponse()
            {
                MatchId = nameof(MatchResponse.MatchId),
                Players = Array.Empty<MatchedPlayer>(),
                Error = null,
            };

            return JsonConvert.SerializeObject(response);
        }
    }
}
