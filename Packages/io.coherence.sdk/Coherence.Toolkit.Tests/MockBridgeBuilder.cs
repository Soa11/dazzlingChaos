// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using Connection;
    using Core;
    using Entities;
    using Moq;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Can be used to <see cref="Build"/> a mock <see cref="ICoherenceBridge"/> object for use in a test.
    /// </summary>
    internal sealed class MockBridgeBuilder : IDisposable
    {
        private bool isSimulatorOrHost;
        private ConnectionType connectionType;
        private Scene scene;
        private ClientID? clientID;
        private bool isConnected;
        private CoherenceSyncConfig clientConnectionEntry;
        private MockClientBuilder mockClientBuilder;
        private MockEntitiesManagerBuilder mockEntitiesManagerBuilder;
        private Mock<ICoherenceBridge> mock;
        private ICoherenceBridge bridge;
        private bool buildExecuted;
        private Func<Entity, ICoherenceSync> getCoherenceSyncForEntityResult;

        public MockClientBuilder MockClientBuilder => mockClientBuilder ??= new();
        public IClient Client => MockClientBuilder.Build();
        public MockEntitiesManagerBuilder MockEntitiesManagerBuilder => mockEntitiesManagerBuilder ??= new();
        public EntitiesManager EntitiesManager => MockEntitiesManagerBuilder.Build();

        public Mock<ICoherenceBridge> Mock
        {
            get
            {
                if (mock is null)
                {
                    Build();
                }

                return mock;
            }
        }

        public ICoherenceBridge Bridge => Build();


        public MockBridgeBuilder SetScene(Scene scene)
        {
            this.scene = scene;
            return this;
        }

        public MockBridgeBuilder SetIsSimulatorOrHost(bool isSimulatorOrHost = true)
        {
            this.isSimulatorOrHost = isSimulatorOrHost;
            return this;
        }

        public MockBridgeBuilder SetConnectionType(ConnectionType connectionType)
        {
            this.connectionType = connectionType;
            return this;
        }

        public MockBridgeBuilder SetClientID(ClientID id)
        {
            this.clientID = id;
            return this;
        }

        public MockBridgeBuilder SetIsConnected(bool isConnected = true)
        {
            this.isConnected = isConnected;
            return this;
        }

        public MockBridgeBuilder SetupEntitiesManager(Action<MockEntitiesManagerBuilder> setupEntitiesManagerBuilder)
        {
            setupEntitiesManagerBuilder(MockEntitiesManagerBuilder);
            return this;
        }

        public MockBridgeBuilder GetCoherenceSyncForEntityReturns(Func<Entity, ICoherenceSync> getCoherenceSyncForEntityResult)
        {
            this.getCoherenceSyncForEntityResult = getCoherenceSyncForEntityResult;
            return this;
        }

        public ICoherenceBridge Build()
        {
            if (buildExecuted)
            {
                return bridge ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;

            clientConnectionEntry = (CoherenceSyncConfig)ScriptableObject.CreateInstance(typeof(CoherenceSyncConfig));

            mock = new Mock<ICoherenceBridge>(MockBehavior.Strict);
            if (clientID.HasValue)
            {
                mock.Setup(bridge => bridge.ClientID).Returns(clientID.Value);
                MockClientBuilder.SetClientID(clientID.Value);
            }

            mock.Setup(x => x.Client).Returns(()=> Client);
            mock.Setup(x => x.ClientFixedSimulationFrame).Returns(0);
            mock.Setup(x => x.GetClientConnectionEntry()).Returns(()=> clientConnectionEntry);
            mock.Setup(x => x.EnableClientConnections).Returns(true);
            mock.Setup(x => x.InstantiationScene).Returns(()=> scene);
            mock.Setup(x => x.IsSimulatorOrHost).Returns(()=> isSimulatorOrHost);
            mock.Setup(x => x.ConnectionType).Returns(()=> connectionType);
            mock.Setup(x => x.EntitiesManager).Returns(()=> EntitiesManager);
            mock.Setup(x => x.OnNetworkEntityCreatedInvoke(It.IsAny<NetworkEntityState>())).Verifiable();
            mock.Setup(x => x.IsConnected).Returns(()=> isConnected);
            if (getCoherenceSyncForEntityResult is not null)
            {
                mock.Setup(x => x.GetCoherenceSyncForEntity(It.IsAny<Entity>())).Returns(getCoherenceSyncForEntityResult);
            }
            else
            {
                mock.Setup(x => x.GetCoherenceSyncForEntity(It.IsAny<Entity>())).Returns((Entity entity) => null);
            }

            var networkTime = new NetworkTime();
            mock.Setup(x => x.NetworkTime).Returns(networkTime);
            bridge = mock.Object;
            return bridge;
        }

        public void RaiseOnConnectedInternal()
        {
            Build();
            mock.Raise(b => b.OnConnectedInternal += null, bridge);
        }

        public void Dispose()
        {
            if (clientConnectionEntry)
            {
                Object.DestroyImmediate(clientConnectionEntry);
                clientConnectionEntry = null;
            }
        }
    }
}
