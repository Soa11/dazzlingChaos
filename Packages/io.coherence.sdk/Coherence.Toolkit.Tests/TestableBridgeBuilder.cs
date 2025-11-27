// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Cloud;
    using Common;
    using Connection;
    using Moq;
    using ProtocolDef;
    using Runtime.Tests;
    using SimulationFrame;
    using Toolkit.Relay;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Can be used to <see cref="Build"/> a <see cref="CoherenceBridge"/> with test double dependencies for use in a test.
    /// </summary>
    internal sealed class TestableBridgeBuilder : IDisposable
    {
        private CoherenceBridge bridge;
        private bool isMain;
        private bool createGlobalQuery = true;
        private bool enableClientConnections = true;
        private CoherenceBridgePlayerAccount playerAccountAutoConnect;
        private MockClientBuilder clientBuilder;
        private CoherenceSyncConfig clientConnectionEntry;
        private CoherenceSyncConfig simulatorConnectionEntry;
        private FakeCloudServiceBuilder cloudServiceBuilder;
        private Mock<CoherenceSyncConfig> clientConnectionEntryMock;
        private Mock<CloudService> cloudServiceMock;
        private CoherenceGlobalQuery globalQuery;
        private CoherenceClientConnectionManager clientConnections;
        private CoherenceInputManager inputManager;
        private EntitiesManager entitiesManager;
        private bool globalQueryCanBeNull;
        private bool buildExecuted;
        private bool clientAsHost;
        private UniquenessManager uniquenessManager;
        private CoherenceRelayManager relayManager;
        private FakeCloudServiceBuilder CloudServiceBuilder => cloudServiceBuilder ??= new();
        public MockClientBuilder ClientBuilder => clientBuilder ??= new();
        private IClient Client => ClientBuilder.Build();
        private CloudService CloudService => CloudServiceBuilder.Build();
        private CoherenceSyncConfig ClientConnectionEntry => clientConnectionEntry ? clientConnectionEntry : clientConnectionEntry = (CoherenceSyncConfig)ScriptableObject.CreateInstance(typeof(CoherenceSyncConfig));
        private CoherenceSyncConfig SimulatorConnectionEntry => simulatorConnectionEntry ? simulatorConnectionEntry : simulatorConnectionEntry = (CoherenceSyncConfig)ScriptableObject.CreateInstance(typeof(CoherenceSyncConfig));
        private CoherenceGlobalQuery GlobalQuery => globalQuery || globalQueryCanBeNull ? globalQuery : globalQuery = new GameObject("GlobalQuery").AddComponent<CoherenceGlobalQuery>();

        public TestableBridgeBuilder SetIsMain(bool isMain = true)
        {
            this.isMain = isMain;
            return this;
        }

        public TestableBridgeBuilder SetPlayerAccountAutoConnect(CoherenceBridgePlayerAccount playerAccountAutoConnect)
        {
            this.playerAccountAutoConnect = playerAccountAutoConnect;
            return this;
        }

        public TestableBridgeBuilder SetCloudService(CloudService cloudService)
        {
            CloudServiceBuilder.CloudService = cloudService;
            return this;
        }

        public TestableBridgeBuilder SetClientID(ClientID clientID)
        {
            ClientBuilder.SetClientID(clientID);
            return this;
        }

        public TestableBridgeBuilder SetupClient([DisallowNull] Action<MockClientBuilder> setupClientBuilder)
        {
            setupClientBuilder(ClientBuilder);
            return this;
        }

        public TestableBridgeBuilder SetupCloudService([DisallowNull] Action<FakeCloudServiceBuilder> setupCloudServiceBuilder)
        {
            setupCloudServiceBuilder(CloudServiceBuilder);
            return this;
        }

        public TestableBridgeBuilder SetGlobalQuery(CoherenceGlobalQuery globalQuery)
        {
            this.globalQuery = globalQuery;
            if (globalQuery is null)
            {
                globalQueryCanBeNull = true;
            }

            return this;
        }

        public TestableBridgeBuilder SetCreateGlobalQuery(bool createGlobalQuery)
        {
            this.createGlobalQuery = createGlobalQuery;
            return this;
        }

        public TestableBridgeBuilder SetEnableClientConnections(bool enableClientConnections)
        {
            this.enableClientConnections = enableClientConnections;
            return this;
        }

        public TestableBridgeBuilder SetClientAsHost(bool clientAsHost)
        {
            this.clientAsHost = clientAsHost;
            return this;
        }

        public CoherenceBridge Build()
        {
            var (result, initTask) = BuildAsync();
            initTask.Then(x => Debug.LogException(x.Exception), TaskContinuationOptions.OnlyOnFaulted);
            return result;
        }

        public (CoherenceBridge, Task<CoherenceBridge>) BuildAsync()
        {
            if (buildExecuted)
            {
                if (bridge is null)
                {
                    return (null, Task.FromException<CoherenceBridge>(new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!")));
                }

                return (bridge, Task.FromResult(bridge));
            }

            buildExecuted = true;

            var logger = Log.Log.GetLogger<CoherenceBridge>(bridge);
            uniquenessManager = new(logger);
            relayManager = new(logger);
            Func<CoherenceBridge, CoherenceClientConnectionManager> clientConnectionsFactory = bridge => clientConnections ??= new(bridge, logger);
            Func<CoherenceBridge, CoherenceInputManager> inputManagerFactory = bridge => inputManager ??= new(bridge);
            Func<CoherenceBridge, AuthorityManager> authorityManagerFactory = bridge => new(Client, bridge);
            Func<CoherenceBridge, CoherenceSceneManager> sceneManagerFactory = bridge => new(clientConnectionsFactory(bridge), Client);
            var definitionMock = new Mock<IDefinition>();
            definitionMock.Setup(d => d.GenerateCoherenceUUIDData(It.IsAny<string>(), It.IsAny<AbsoluteSimulationFrame>())).Returns(new MockComponentData(1));
            var definition = definitionMock.Object;
            Func<CoherenceBridge, EntitiesManager> entitiesManagerFactory = bridge => entitiesManager ??= new(bridge, clientConnectionsFactory(bridge), inputManagerFactory(bridge), uniquenessManager, definition, logger);
            Func<CoherenceBridge, FloatingOriginManager> floatingOriginManagerFactory = bridge => new(Client, entitiesManagerFactory(bridge), logger);

            Task<CoherenceBridge> initTask;
            (bridge, initTask) = CoherenceBridge.Create
            (
                logger,
                Client,
                ClientConnectionEntry,
                SimulatorConnectionEntry,
                uniquenessManager,
                CloudService,
                GlobalQuery,
                relayManager,
                clientConnectionsFactory,
                inputManagerFactory,
                authorityManagerFactory,
                entitiesManagerFactory,
                sceneManagerFactory,
                floatingOriginManagerFactory,
                isMain: isMain,
                createGlobalQuery: createGlobalQuery,
                enableClientConnections: enableClientConnections,
                playerAccountAutoConnect: playerAccountAutoConnect,
                clientAsHost: clientAsHost
            );

            return (bridge, initTask);
        }

        public void Dispose()
        {
            if (bridge)
            {
                ((IDisposable)bridge).Dispose();
                cloudServiceBuilder.Dispose();
                Object.DestroyImmediate(bridge.gameObject);
                bridge = null;
            }

            if (clientConnectionEntry)
            {
                Object.DestroyImmediate(clientConnectionEntry);
                clientConnectionEntry = null;
            }

            if (simulatorConnectionEntry)
            {
                Object.DestroyImmediate(simulatorConnectionEntry);
                simulatorConnectionEntry = null;
            }

            if (globalQuery)
            {
                Object.DestroyImmediate(globalQuery.gameObject);
                globalQuery = null;
            }
        }
    }
}
