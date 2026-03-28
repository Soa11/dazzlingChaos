namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Brisk;
    using Cloud;
    using Coherence.Tests;
    using Common;
    using Connection;
    using Entities;
    using NUnit.Framework;
    using Runtime.Tests;
    using Stats;
    using Toolkit.Relay;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.TestTools;
    using Utils;
    using Object = UnityEngine.Object;
    using Ping = Common.Ping;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceBridge"/>.
    /// </summary>
    public class CoherenceBridgeTests : CoherenceTest
    {
        private TestableBridgeBuilder bridgeBuilder;
        private CoherenceBridge bridge;
        private Task<CoherenceBridge> initBridgeTask;
        private CoherenceBridge Bridge
        {
            get
            {
                if (bridge)
                {
                    return bridge;
                }

                (bridge, initBridgeTask) = bridgeBuilder.BuildAsync();
                return bridge;
            }
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            bridgeBuilder = new();
            bridgeBuilder.SetupClient(x => x.SetIsConnected(false));
        }

        [TearDown]
        public override async void TearDown()
        {
            bridgeBuilder.Dispose();
            Assert.That(CoherenceBridgeStore.bridges.Count, Is.Zero);
            Assert.That((bool)CoherenceBridgeStore.MasterBridge, Is.False);

            if (initBridgeTask is not null)
            {
                try
                {
                    await initBridgeTask;
                }
                catch (Exception exception)
                {
                    if (!exception.WasCanceled())
                    {
                        Assert.Fail($"CoherenceBridge initialization failed with an exception: {exception}");
                    }
                }
            }

            base.TearDown();
        }

        [TestCase(CoherenceBridgePlayerAccount.AutoLoginAsGuest)]
        [TestCase(CoherenceBridgePlayerAccount.None)]
        [Description("Ensures that a bridge CloudService is not null. See: https://github.com/coherence/unity/issues/7796")]
        public void CoherenceBridge_CloudService_Not_Null_When_PlayerAccountAutoConnect_Is_Not_Main(CoherenceBridgePlayerAccount playerAccountAutoConnect)
        {
            bridgeBuilder.SetCloudService(null)
                         .SetPlayerAccountAutoConnect(playerAccountAutoConnect);

            var cloudService = Bridge.CloudService;

            Assert.That(cloudService, Is.Not.Null);
        }

        [Test]
        public void CoherenceBridge_CloudService_Equals_Main_PlayerAccount_CloudService_When_PlayerAccountAutoConnect_Is_Main()
        {
            using var fakeCloudServiceBuilder = new FakeCloudServiceBuilder();
            var mainPlayerAccountCloudService = fakeCloudServiceBuilder.Build();
            using var mainPlayerAccount = new PlayerAccount(LoginInfo.WithPassword("Username", "Password", false), new("CloudUniqueId"), "ProjectId", mainPlayerAccountCloudService);
            mainPlayerAccount.SetAsMain();

            bridgeBuilder.SetCloudService(null)
                         .SetPlayerAccountAutoConnect(CoherenceBridgePlayerAccount.Main);

            var cloudService = Bridge.CloudService;
            Assert.That(cloudService, Is.EqualTo(mainPlayerAccountCloudService));
        }

        [Test]
        public void CoherenceBridge_CloudService_Is_Null_When_PlayerAccountAutoConnect_Is_Main_And_PlayerAccount_Main_Is_Null()
        {
            bridgeBuilder.SetCloudService(null)
                         .SetPlayerAccountAutoConnect(CoherenceBridgePlayerAccount.Main);

            var cloudService = Bridge.CloudService;

            Assert.That(cloudService, Is.Null);
        }

        [TestCase(CoherenceBridgePlayerAccount.AutoLoginAsGuest)]
        [TestCase(CoherenceBridgePlayerAccount.None)]
        public void Dispose_Disposes_CloudService_When_PlayerAccountAutoConnect_Is_Not_Main(CoherenceBridgePlayerAccount playerAccountAutoConnect)
        {
            bridgeBuilder.SetCloudService(null)
                         .SetPlayerAccountAutoConnect(playerAccountAutoConnect);

            var cloudService = Bridge.CloudService;
            ((IDisposable)Bridge).Dispose();
            Assert.That(cloudService.IsDisposed, Is.True);
        }

        [TestCase(CoherenceBridgePlayerAccount.AutoLoginAsGuest, false), TestCase(CoherenceBridgePlayerAccount.AutoLoginAsGuest, true)]
        [TestCase(CoherenceBridgePlayerAccount.None, false), TestCase(CoherenceBridgePlayerAccount.None, true)]
        public async Task DisposeAsync_Disposes_CloudService_When_PlayerAccountAutoConnect_Is_Not_Main(CoherenceBridgePlayerAccount playerAccountAutoConnect, bool waitForOngoingCloudOperationsToFinish)
        {
            bridgeBuilder.SetCloudService(null)
                         .SetPlayerAccountAutoConnect(playerAccountAutoConnect);

            var cloudService = Bridge.CloudService;
            Assert.That(cloudService.IsDisposed, Is.False);
            await Bridge.DisposeAsync(waitForOngoingCloudOperationsToFinish: waitForOngoingCloudOperationsToFinish);
            Assert.That(cloudService.IsDisposed, Is.True);
        }

        [Test]
        public void Dispose_Does_Not_Disposes_CloudService_When_PlayerAccountAutoConnect_Is_Main()
        {
            using var fakeCloudServiceBuilder = new FakeCloudServiceBuilder();
            var mainPlayerAccountCloudService = fakeCloudServiceBuilder.Build();
            using var mainPlayerAccount = new PlayerAccount(LoginInfo.WithPassword("Username", "Password", false), new("CloudUniqueId"), "ProjectId", mainPlayerAccountCloudService);
            mainPlayerAccount.SetAsMain();

            bridgeBuilder.SetCloudService(null)
                         .SetPlayerAccountAutoConnect(CoherenceBridgePlayerAccount.Main);

            var cloudService = Bridge.CloudService;
            ((IDisposable)Bridge).Dispose();
            Assert.That(cloudService.IsDisposed, Is.False);
        }

        [TestCase(false), TestCase(true)]
        public async Task DisposeAsync_Does_Not_Disposes_CloudService_When_PlayerAccountAutoConnect_Is_Main(bool waitForOngoingCloudOperationsToFinish)
        {
            using var fakeCloudServiceBuilder = new FakeCloudServiceBuilder();
            var mainPlayerAccountCloudService = fakeCloudServiceBuilder.Build();
            using var mainPlayerAccount = new PlayerAccount(LoginInfo.WithPassword("Username", "Password", false), new("CloudUniqueId"), "ProjectId", mainPlayerAccountCloudService);
            mainPlayerAccount.SetAsMain();

            bridgeBuilder.SetCloudService(null)
                         .SetPlayerAccountAutoConnect(CoherenceBridgePlayerAccount.Main);

            var cloudService = Bridge.CloudService;
            await Bridge.DisposeAsync(waitForOngoingCloudOperationsToFinish: waitForOngoingCloudOperationsToFinish);
            Assert.That(cloudService.IsDisposed, Is.False);
        }

        [TestCase(CoherenceBridgePlayerAccount.AutoLoginAsGuest)]
        [TestCase(CoherenceBridgePlayerAccount.Main)]
        [TestCase(CoherenceBridgePlayerAccount.None)]
        public void Dispose_Does_Not_Disposes_CloudService_Injected_Via_Property(CoherenceBridgePlayerAccount playerAccountAutoConnect)
        {
            bridgeBuilder.SetCloudService(null)
                .SetPlayerAccountAutoConnect(playerAccountAutoConnect);

            using var fakeCloudServiceBuilder = new FakeCloudServiceBuilder();
            var cloudService = fakeCloudServiceBuilder.Build();
            Bridge.CloudService = cloudService;

            ((IDisposable)Bridge).Dispose();

            Assert.That(cloudService.IsDisposed, Is.False);
        }

        [TestCase(CoherenceBridgePlayerAccount.AutoLoginAsGuest, false), TestCase(CoherenceBridgePlayerAccount.AutoLoginAsGuest, true)]
        [TestCase(CoherenceBridgePlayerAccount.Main, false), TestCase(CoherenceBridgePlayerAccount.Main, true)]
        [TestCase(CoherenceBridgePlayerAccount.None, false), TestCase(CoherenceBridgePlayerAccount.None, true)]
        public async Task DisposeAsync_Does_Not_Disposes_CloudService_Injected_Via_Property(CoherenceBridgePlayerAccount playerAccountAutoConnect, bool waitForOngoingCloudOperationsToFinish)
        {
            bridgeBuilder.SetCloudService(null)
                         .SetPlayerAccountAutoConnect(playerAccountAutoConnect);

            using var fakeCloudServiceBuilder = new FakeCloudServiceBuilder();
            var cloudService = fakeCloudServiceBuilder.Build();
            Bridge.CloudService = cloudService;

            await Bridge.DisposeAsync(waitForOngoingCloudOperationsToFinish: waitForOngoingCloudOperationsToFinish);

            Assert.That(cloudService.IsDisposed, Is.False);
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void Connect_Creates_CoherenceGlobalQuery_When_Expected(bool enableClientConnections, bool createGlobalQuery, bool shouldHaveActiveGlobalQuery)
        {
            bridgeBuilder.SetGlobalQuery(null)
                         .SetEnableClientConnections(enableClientConnections)
                         .SetCreateGlobalQuery(createGlobalQuery)
                         .Build();
            Bridge.HasBakedData = true;
            Bridge.Connect(new());
            Assert.That(Bridge.HasActiveGlobalQuery, Is.EqualTo(shouldHaveActiveGlobalQuery));
        }

        [Test]
        public void Main_Bridge_Is_Destroyed_During_Awake_If_MasterBridge_Already_Exists()
        {
            bridgeBuilder.SetIsMain().Build();
            using var secondBridgeBuilder = new TestableBridgeBuilder().SetIsMain(true);
            var secondBridge = secondBridgeBuilder.Build();
            Assert.That((bool)secondBridge, Is.False);
        }

        [Test]
        public void Non_Main_Bridge_Is_Destroyed_During_Awake_If_MasterBridge_Already_Exists()
        {
            bridgeBuilder.SetIsMain().Build();
            using var secondBridgeBuilder = new TestableBridgeBuilder().SetIsMain(false);
            var secondBridge = secondBridgeBuilder.Build();

            // TODO: change expected results to True once issue 'Main Bridge - destroy only other main bridges (#7682)' has been completed.
            Assert.That((bool)secondBridge, Is.False);
        }

        [Test]
        public void Setting_CloudService_To_Null_Disposes_PreviousCloudService()
        {
            bridgeBuilder.SetPlayerAccountAutoConnect(CoherenceBridgePlayerAccount.None).SetCloudService(null).Build();
            var cloudService = Bridge.CloudService;
            Bridge.CloudService = null;
            Assert.That(cloudService.IsDisposed, Is.True);
        }

        [Test]
        public void Setting_CloudService_To_Value_It_Already_Has_Does_Not_Dispose_It()
        {
            bridgeBuilder.SetPlayerAccountAutoConnect(CoherenceBridgePlayerAccount.None).SetCloudService(null).Build();
            var cloudService = Bridge.CloudService;
            Bridge.CloudService = cloudService;
            Assert.That(cloudService.IsDisposed, Is.False);
        }

        [Test]
        public void OnConnected_Is_Raised_When_Connect_Is_Executed()
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(false));
            var timesRaised = 0;
            Bridge.HasBakedData = true;
            Bridge.onConnected.AddListener(_ => timesRaised++);
            Bridge.Connect(new());
            Assert.That(timesRaised, Is.EqualTo(1));
        }

        [Test]
        public void OnDisconnected_Is_Raised_When_Disconnect_Is_Executed()
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(true));
            var timesRaised = 0;
            Bridge.onDisconnected.AddListener((_, _) => timesRaised++);
            Bridge.Disconnect();
            Assert.That(timesRaised, Is.EqualTo(1));
        }

        [Test]
        public void OnConnected_Handles_Exception_In_Event_Handler()
        {
            Bridge.HasBakedData = true;
            Bridge.onConnected.AddListener(_ => throw new());
            Bridge.Connect(new());
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        [Test]
        public void OnDisconnected_Handles_Exception_In_Event_Handler()
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(true));
            Bridge.onDisconnected.AddListener((_, _) => throw new());
            Bridge.Disconnect();
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        [Test]
        public void OnValidateConnectionRequest_Handles_Exception_In_Event_Handler()
        {
            Bridge.onValidateConnectionRequest.AddListener(_ => throw new());
            bridgeBuilder.ClientBuilder.RaiseOnValidateConnectionRequest(new());
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        [Test]
        public void OnConnectionError_Handles_Exception_In_Event_Handler()
        {
            Bridge.onConnectionError.AddListener((_, _) => throw new());
            bridgeBuilder.ClientBuilder.RaiseOnConnectionError(new(""));
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        [Test]
        public void OnNetworkEntityCreated_Handles_Exception_In_Event_Handler()
        {
            Bridge.onNetworkEntityCreated.AddListener((_, _) => throw new());
            Bridge.OnNetworkEntityCreatedInvoke(new(new(), AuthorityType.Full, false, false, null, " "));
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        [Test]
        public void OnNetworkEntityDestroyed_Handles_Exception_In_Event_Handler()
        {
            Bridge.onNetworkEntityDestroyed.AddListener((_, _, _) => throw new());
            var entity = new Entity(default, 1, true);
            var state = new NetworkEntityState(entity, AuthorityType.Full, false, false, null, " ");
            Bridge.OnNetworkEntityDestroyedInvoke(state, DestroyReason.ClientDestroy);
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        [Test]
        public void OnLiveQuerySynced_Handles_Exception_In_Event_Handler()
        {
            Bridge.HasBakedData = true;
            Bridge.onLiveQuerySynced.AddListener(_ => throw new());
            Bridge.OnQuerySynced((true, true));
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void IsConnected_Is_True_After_Connect_Has_Been_Executed(bool hasBakedData)
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(false));
            Bridge.HasBakedData = hasBakedData;
            Assert.That(Bridge.IsConnected, Is.False);
            Bridge.Connect(new());
            Assert.That(Bridge.IsConnected, Is.EqualTo(hasBakedData));
        }

        [Test]
        public void IsConnected_Is_False_After_Disconnect_Has_Been_Executed()
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(true));
            Assert.That(Bridge.IsConnected, Is.True);
            Bridge.Disconnect();
            Assert.That(Bridge.IsConnected, Is.False);
        }

        [TestCase(ConnectionState.Disconnected, false)]
        [TestCase(ConnectionState.Opening, false)]
        [TestCase(ConnectionState.Connecting, true)]
        [TestCase(ConnectionState.Connected, false)]
        public void IsConnecting_Reflects_Client_ConnectionState(ConnectionState connectionState, bool expected)
        {
            var bridge = Bridge;
            bridgeBuilder.SetupClient(x => x.SetConnectionState(connectionState));
            Assert.That(bridge.IsConnecting, Is.EqualTo(expected));
        }

        [TestCase(ConnectionState.Disconnected, false)]
        [TestCase(ConnectionState.Opening, false)]
        [TestCase(ConnectionState.Connecting, false)]
        [TestCase(ConnectionState.Connected, true)]
        public void IsConnected_Reflects_Client_ConnectionState(ConnectionState connectionState, bool expected)
        {
            var bridge = Bridge;
            bridgeBuilder.SetupClient(x => x.SetConnectionState(connectionState));
            Assert.That(bridge.IsConnected, Is.EqualTo(expected));
        }

        [Test]
        public void SetInitialScene_Updates_Client_InitialScene()
        {
            Bridge.SetInitialScene(42);
            Assert.That(Bridge.Client.InitialScene, Is.EqualTo(42));
        }

        [Test]
        public void GetClientConnectionEntry_Returns_ClientConnectionEntry()
        {
            var bridge = Bridge;
            Assert.That(bridge.GetClientConnectionEntry(), Is.EqualTo(bridge.ClientConnectionEntry).And.Not.Null);
        }

        [Test]
        public void GetSimulatorConnectionEntry_Returns_SimulatorConnectionEntry()
        {
            var bridge = Bridge;
            Assert.That(bridge.GetSimulatorConnectionEntry(), Is.EqualTo(bridge.SimulatorConnectionEntry).And.Not.Null);
        }

        [Test]
        public void Scene_Returns_GameObject_Scene()
        {
            var bridge = Bridge;
            Assert.That(bridge.Scene, Is.EqualTo(bridge.gameObject.scene));
        }

        [Test]
        public void InstantiationScene_Defaults_To_Bridge_Scene()
        {
            var bridge = Bridge;
            Assert.That(bridge.InstantiationScene, Is.EqualTo(bridge.Scene));
        }

        [Test]
        public void Transform_ReturnsGameObjectTransform()
        {
            var bridge = Bridge;
            Assert.That(bridge.Transform, Is.EqualTo(bridge.transform));
        }

        [Test]
        public void EntityCount_ReturnsZeroInitially()
        {
            var bridge = Bridge;
            Assert.That(bridge.EntityCount, Is.EqualTo(0));
        }

        [Test]
        public void Setting_InstantiationScene_Updates_CoherenceBridgeStore()
        {
            var bridge = Bridge;
            var scene = SceneManager.GetActiveScene();
            bridge.InstantiationScene = scene;
            Assert.That(CoherenceBridgeStore.TryGetBridge(scene, out var foundBridge), Is.True);
            Assert.That(foundBridge, Is.EqualTo(bridge));
            bridge.InstantiationScene = null;
            Assert.That(CoherenceBridgeStore.TryGetBridge(scene, out foundBridge), Is.False);
            Assert.That((bool)foundBridge, Is.False);
        }

        [Test]
        public void GetValidatedHostPayload_ReturnsClientValidatedHostPayload()
        {
            var bridge = Bridge;
            var payload = bridge.GetValidatedHostPayload();
            Assert.That(payload, Is.EqualTo(bridge.Client.ValidatedHostPayload));
        }

        [Test]
        public void SetConnectionValidationPayload_UpdatesClientPayload()
        {
            var bridge = Bridge;
            var payload = new CustomPayload(new byte[] { 1, 2, 3 });
            bridge.SetConnectionValidationPayload(payload);
            Assert.That(bridge.Client.ConnectionValidationPayload, Is.EqualTo(payload));
        }

        [TestCase(false), TestCase(true)]
        public void TranslateFloatingOrigin_Vector3_Result_Is_Determined_By_Connection_State(bool isConnected)
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(isConnected)).Build();
            var bridge = Bridge;
            var translation = new Vector3(1f, 2f, 3f);
            var result = bridge.TranslateFloatingOrigin(translation);
            Assert.That(result, Is.EqualTo(isConnected));
        }

        [TestCase(false), TestCase(true)]
        public void TranslateFloatingOrigin_Vector3d_Result_Is_Determined_By_Connection_State(bool isConnected)
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(isConnected)).Build();
            var bridge = Bridge;
            var translation = new Vector3d(1d, 2d, 3d);
            var result = bridge.TranslateFloatingOrigin(translation);
            Assert.That(result, Is.EqualTo(isConnected));
        }

        [TestCase(false), TestCase(true)]
        public void SetFloatingOrigin_Result_Is_Determined_By_Connection_State(bool isConnected)
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(isConnected)).Build();
            var bridge = Bridge;
            var newOrigin = new Vector3d(1d, 2d, 3d);
            var result = bridge.SetFloatingOrigin(newOrigin);
            Assert.That(result, Is.EqualTo(isConnected));
        }

        [TestCase(false), TestCase(true)]
        public void SetFloatingOrigin_Updates_GetFloatingOrigin_Result_When_Connected(bool isConnected)
        {
            bridgeBuilder.SetupClient(x => x.SetIsConnected(isConnected));
            var bridge = Bridge;
            var newOrigin = new Vector3d(1d, 2d, 3d);
            bridge.SetFloatingOrigin(newOrigin);
            if (!isConnected)
            {
                Assert.That(bridge.GetFloatingOrigin(), Is.Not.EqualTo(newOrigin));
            }
            else
            {
                Assert.That(bridge.GetFloatingOrigin(), Is.EqualTo(newOrigin));
            }
        }

        [Test]
        public void ConnectionType_ReturnsClientConnectionType()
        {
            var bridge = Bridge;
            Assert.That(bridge.ConnectionType, Is.EqualTo(bridge.Client.ConnectionType));
        }

        [TestCase(ConnectionType.Client, false, false)]
        [TestCase(ConnectionType.Client, true, true)]
        [TestCase(ConnectionType.Simulator, false, true)]
        [TestCase(ConnectionType.Simulator, true, true)]
        [TestCase(ConnectionType.Replicator, false, false)]
        [TestCase(ConnectionType.Replicator, true, false)]
        public void IsSimulatorOrHost_Is_Based_On_ConnectionType_And_ClientAsHost(ConnectionType connectionType, bool clientAsHost, bool expected)
        {
            bridgeBuilder.SetClientAsHost(clientAsHost);
            var bridge = Bridge;
            bridgeBuilder.SetupClient(x => x.SetConnectionType(connectionType));
            Assert.That(bridge.IsSimulatorOrHost, Is.EqualTo(expected));
        }

        [Test]
        public void ClientID_Returns_ClientID_From_Client()
        {
            var bridge = Bridge;
            var clientID = new ClientID(42u);
            bridgeBuilder.SetupClient(x => x.SetClientID(clientID));
            Assert.That(bridge.ClientID, Is.EqualTo(clientID));
        }

        [Test]
        public void NetStats_Returns_Stats_From_Client()
        {
            var bridge = Bridge;
            var stats = new Stats();
            bridgeBuilder.SetupClient(x => x.SetStats(stats)).Build();
            Assert.That(bridge.NetStats, Is.EqualTo(stats));
        }

        [Test]
        public void Ping_Returns_Ping_From_Client()
        {
            var bridge = Bridge;
            var ping = new Ping();
            bridgeBuilder.SetupClient(x => x.SetPing(ping)).Build();
            Assert.That(bridge.Ping, Is.EqualTo(ping));
        }

        [Test]
        public void NetworkTimeAsDouble_Is_In_Sync_With_Client_NetworkTime()
        {
            var bridge = Bridge;
            bridgeBuilder.SetupClient(x => x.SetupNetworkTime(x => x.SetTimeAsDouble(42d)));
            Assert.That(bridge.NetworkTimeAsDouble, Is.EqualTo(42d));
        }

        [Test]
        public void ClientFixedSimulationFrame_Is_In_Sync_With_Client_NetworkTime()
        {
            var bridge = Bridge;
            var fixedSimulationFrame = 1L;
            bridgeBuilder.SetupClient(x => x.SetupNetworkTime(x => x.SetClientFixedSimulationFrame(fixedSimulationFrame)));
            Assert.That(bridge.ClientFixedSimulationFrame, Is.EqualTo(fixedSimulationFrame));
        }

        [Test]
        public void ClientConnections_IsNotNull()
        {
            var bridge = Bridge;
            Assert.That(bridge.ClientConnections, Is.Not.Null);
        }

        [Test]
        public void InputManager_IsNotNull()
        {
            var bridge = Bridge;
            Assert.That(bridge.InputManager, Is.Not.Null);
        }

        [Test]
        public void AuthorityManager_IsNotNull()
        {
            var bridge = Bridge;
            Assert.That(bridge.AuthorityManager, Is.Not.Null);
        }

        [Test]
        public void EntitiesManager_IsNotNull()
        {
            var bridge = Bridge;
            Assert.That(bridge.EntitiesManager, Is.Not.Null);
        }

        [Test]
        public void UniquenessManager_IsNotNull()
        {
            var bridge = Bridge;
            Assert.That(bridge.UniquenessManager, Is.Not.Null);
        }

        [Test]
        public void SceneManager_IsNotNull()
        {
            var bridge = Bridge;
            Assert.That(bridge.SceneManager, Is.Not.Null);
        }

        [Test]
        public void FloatingOriginManager_IsNotNull()
        {
            var bridge = Bridge;
            Assert.That(bridge.FloatingOriginManager, Is.Not.Null);
        }

        [Test]
        public void CloudService_Returns_Null_After_Bridge_Destroyed_And_Disposed()
        {
            Assert.That(Bridge.CloudService, Is.Not.Null);
            ((IDisposable)Bridge).Dispose();
            Object.DestroyImmediate(Bridge);
            Assert.That(Bridge.CloudService, Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public async Task CloudService_Returns_Null_After_Bridge_Destroyed_And_Disposed_Asynchronously(bool waitForOngoingCloudOperationsToFinish)
        {
            Assert.That(Bridge.CloudService, Is.Not.Null);
            await Bridge.DisposeAsync(waitForOngoingCloudOperationsToFinish: waitForOngoingCloudOperationsToFinish);
            Object.DestroyImmediate(Bridge);
            Assert.That(Bridge.CloudService, Is.Null);
        }

        [Test]
        public void UnityEvents_AreNotNull()
        {
            var bridge = Bridge;
            Assert.That(bridge.onConnected, Is.Not.Null);
            Assert.That(bridge.onDisconnected, Is.Not.Null);
            Assert.That(bridge.onConnectionError, Is.Not.Null);
            Assert.That(bridge.onNetworkEntityCreated, Is.Not.Null);
            Assert.That(bridge.onNetworkEntityDestroyed, Is.Not.Null);
            Assert.That(bridge.onLiveQuerySynced, Is.Not.Null);
            Assert.That(bridge.onValidateConnectionRequest, Is.Not.Null);
        }

        [Test]
        [Description("Ensures that errors thrown from the IRelay interface are routed through the CoherenceBridge OnConnectionError event.")]
        public void CoherenceBridge_IRelay_OnError()
        {
            var bridge = Bridge;
            var relay = new MockRelay();
            bridge.SetRelay(relay);

            ConnectionException receivedException = null;
            bridge.onConnectionError.AddListener((_, ex) => receivedException = ex);

            var testException = new ConnectionException("Test exception");
            relay.ThrowOnError(testException);

            Assert.NotNull(receivedException);
            Assert.AreEqual(testException, receivedException);
            LogAssert.Expect(LogType.Error, new Regex(".*"));
        }
    }

    public class MockRelay : IRelay
    {
        public event Action<ConnectionException> OnError;

        public CoherenceRelayManager RelayManager { get; set; }

        public void Open()
        {
        }

        public void Close()
        {
        }

        public void Update()
        {
        }

        public void ThrowOnError(ConnectionException exception) => OnError?.Invoke(exception);
    }
}
