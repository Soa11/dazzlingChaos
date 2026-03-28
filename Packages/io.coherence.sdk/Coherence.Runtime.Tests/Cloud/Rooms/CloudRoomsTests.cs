// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests.Cloud.Rooms
{
    using System;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Common;
    using Moq;
    using NUnit.Framework;

    public class CloudRoomsTests : CoherenceTest
    {
        private Mock<IRequestFactoryInternal> mockRequestFactory;
        private Mock<IAuthClientInternal> mockAuthClient;
        private Mock<IRuntimeSettings> mockRuntimeSettings;
        private Mock<IPlayerAccountProvider> mockPlayerAccountProvider;
        private IRequestFactoryInternal requestFactory;
        private IAuthClientInternal authClient;
        private IRuntimeSettings runtimeSettings;
        private IPlayerAccountProvider playerAccountProvider;
        private CloudCredentialsPair credentialsPair;

        public override void OneTimeSetUp()
        {
            mockRequestFactory = new Mock<IRequestFactoryInternal>();
            requestFactory = mockRequestFactory.Object;

            mockAuthClient = new Mock<IAuthClientInternal>();
            authClient = mockAuthClient.Object;

            mockRuntimeSettings = new Mock<IRuntimeSettings>();
            runtimeSettings = mockRuntimeSettings.Object;

            mockPlayerAccountProvider = new Mock<IPlayerAccountProvider>();
            playerAccountProvider = mockPlayerAccountProvider.Object;

            credentialsPair = new CloudCredentialsPair(authClient, requestFactory);
        }

        [Test]
        [Description("CloudRooms.IsLoggedIn returns false when not authenticated or request factory not ready.")]
        public void CloudRooms_IsLoggedIn_When_Auth_And_Request_Ready()
        {
            var lobbyService = new LobbiesService(credentialsPair, runtimeSettings);
            mockAuthClient.SetupGet(m => m.LoggedIn).Returns(() => true);
            mockRequestFactory.SetupGet(m => m.IsReady).Returns(() => true);

            var cloudRooms = new CloudRooms(credentialsPair, runtimeSettings, playerAccountProvider, lobbyService);
            Assert.That(cloudRooms.IsLoggedIn, Is.True);
        }

        [Test]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        [Description("CloudRooms.IsLoggedIn returns false when not authenticated or request factory not ready.")]
        public void CloudRooms_NotLoggedIn_When_Auth_Or_Request_Not_Ready(bool authLoggedIn, bool requestReady)
        {
            var lobbyService = new LobbiesService(credentialsPair, runtimeSettings);
            mockAuthClient.SetupGet(m => m.LoggedIn).Returns(() => authLoggedIn);
            mockRequestFactory.SetupGet(m => m.IsReady).Returns(() => requestReady);

            var cloudRooms = new CloudRooms(credentialsPair, runtimeSettings, playerAccountProvider, lobbyService);
            Assert.That(cloudRooms.IsLoggedIn, Is.False);
        }

        [Test]
        [Description("CloudRooms.IsConnectedToCloud returns true when request factory is ready.")]
        public void Connected_To_Cloud_If_Request_Factory_Is_Ready()
        {
            var lobbyService = new LobbiesService(credentialsPair, runtimeSettings);
            mockRequestFactory.SetupGet(m => m.IsReady).Returns(() => true);

            var cloudRooms = new CloudRooms(credentialsPair, runtimeSettings, playerAccountProvider, lobbyService);
            Assert.That(cloudRooms.IsConnectedToCloud, Is.True);
        }

        [Test]
        [Description("CloudRooms.IsConnectedToCloud returns false when request factory is not ready.")]
        public void Not_Connected_To_Cloud_If_Request_Factory_Is_Not_Ready()
        {
            var lobbyService = new LobbiesService(credentialsPair, runtimeSettings);
            mockRequestFactory.SetupGet(m => m.IsReady).Returns(() => false);

            var cloudRooms = new CloudRooms(credentialsPair, runtimeSettings, playerAccountProvider, lobbyService);
            Assert.That(cloudRooms.IsConnectedToCloud, Is.False);
        }

        [Test]
        [Description("CloudRooms.RoomServices is empty initially.")]
        public void No_Room_Services_Initially()
        {
            var lobbyService = new LobbiesService(credentialsPair, runtimeSettings);
            var cloudRooms = new CloudRooms(credentialsPair, runtimeSettings, playerAccountProvider, lobbyService);

            Assert.That(cloudRooms.RoomServices, Is.Empty);
        }

        [Test]
        [Description("CloudRooms.GetRoomServiceForRegion creates a Room Service for a given region.")]
        public void Room_Service_Created_For_Region()
        {
            var lobbyService = new LobbiesService(credentialsPair, runtimeSettings);
            var cloudRooms = new CloudRooms(credentialsPair, runtimeSettings, playerAccountProvider, lobbyService);

            var region = "us-west-1";
            var roomService = cloudRooms.GetRoomServiceForRegion(region);

            Assert.That(roomService, Is.Not.Null);
            Assert.That(cloudRooms.RoomServices, Is.Not.Empty);
        }

        [Test]
        [Description("CloudRooms.Dispose cleans up resources.")]
        public void Dispose_Resources_Are_Cleaned_Up()
        {
            var removedOnLogin = false;
            var removedOnLogout = false;
            var lobbyService = new LobbiesService(credentialsPair, runtimeSettings);
            var cloudRooms = new CloudRooms(credentialsPair, runtimeSettings, playerAccountProvider, lobbyService);
            mockAuthClient.SetupRemove(m => m.OnLogin -= It.IsAny<Action<LoginResponse>>())
                .Callback(() => removedOnLogin = true);
            mockAuthClient.SetupRemove(m => m.OnLogout -= It.IsAny<Action>()).Callback(() => removedOnLogout = true);

            cloudRooms.Dispose();

            Assert.That(removedOnLogin, Is.True);
            Assert.That(removedOnLogout, Is.True);
        }

        [Test]
        [Description("CloudRooms.Dispose cleans up resources.")]
        public void Async_Dispose_Resources_Are_Cleaned_Up()
        {
            var removedOnLogin = false;
            var removedOnLogout = false;
            var lobbyService = new LobbiesService(credentialsPair, runtimeSettings);
            var cloudRooms = new CloudRooms(credentialsPair, runtimeSettings, playerAccountProvider, lobbyService);
            mockAuthClient.SetupRemove(m => m.OnLogin -= It.IsAny<Action<LoginResponse>>())
                .Callback(() => removedOnLogin = true);
            mockAuthClient.SetupRemove(m => m.OnLogout -= It.IsAny<Action>()).Callback(() => removedOnLogout = true);

            cloudRooms.DisposeAsync().AsTask().Then(() =>
                {
                    Assert.That(removedOnLogin, Is.True);
                    Assert.That(removedOnLogout, Is.True);
                }
            );
        }
    }
}
