// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using Brisk;
    using Common;
    using Connection;
    using Core;
    using Entities;
    using Log;
    using Moq;
    using ProtocolDef;
    using Stats;
    using Transport;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Can be used to <see cref="Build"/> a mock <see cref="IClient"/> object that can be used as a test double.
    /// </summary>
    internal sealed class MockClientBuilder : IDisposable
    {
        public event Action<ClientID> OnConnected;
        public event Action<ConnectionCloseReason> OnDisconnected;
        public event Action<ConnectionException> OnConnectionError { add { } remove { } }
        public event Action<EndpointData> OnConnectedEndpoint { add { } remove { } }
        public event Action<ConnectionValidationRequest> OnValidateConnectionRequest { add { } remove { } }
        public event Action<Entity, IncomingEntityUpdate> OnEntityCreated;
        public event Action<Entity, IncomingEntityUpdate> OnEntityUpdated;
        public event Action<Entity, DestroyReason> OnEntityDestroyed;
        public event Action<IEntityCommand> OnCommand;
        public event Action<IEntityInput> OnInput;
        public event Action<AuthorityRequest> OnAuthorityRequested;
        public event Action<AuthorityRequestRejection> OnAuthorityRequestRejected { add { } remove { } }
        public event Action<AuthorityChange> OnAuthorityChange;
        public event Action<Entity> OnAuthorityTransferred;
        public event Action<SceneIndexChanged> OnSceneIndexChanged { add { } remove { } }

        private ClientID clientID = new(1);
        private MockNetworkTimeBuilder networkTimeBuilder = new();
        private ConnectionType connectionType;
        private string hostname = "localhost";
        private Stats stats = new();
        private Ping ping = new();
        private EndpointData lastEndpointData = new() { host = "host", rsVersion = "1.0" };
        private ConnectionSettings connectionSettings = new();
        private uint initialScene;
        private CustomPayload validatedHostPayload;
        private CustomPayload connectionValidationPayload;
        private byte sendFrequency = 0;
        private Scene scene;
        private Vector3d floatingOrigin = Vector3d.zero;

        //private bool isConnected = true;
        private ConnectionState connectionState = ConnectionState.Connected;
        private EntityIDGenerator idGenerator;
        private Logger logger;
        private Mock<IClient> mock;
        private IClient client;
        private bool buildExecuted;

        public Mock<IClient> Mock
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

        public INetworkTime NetworkTime => networkTimeBuilder.Build();

        public MockClientBuilder SetClientID(ClientID clientID)
        {
            this.clientID = clientID;
            return this;
        }

        public MockClientBuilder SetConnectionType(ConnectionType connectionType)
        {
            this.connectionType = connectionType;
            return this;
        }

        public MockClientBuilder SetConnectionState(ConnectionState connectionState)
        {
            this.connectionState = connectionState;
            return this;
        }

        public MockClientBuilder SetIsConnected(bool isConnected = true)
        {
            this.connectionState = isConnected ? ConnectionState.Connected : ConnectionState.Disconnected;
            return this;
        }

        public MockClientBuilder SetEntityIDGenerator(EntityIDGenerator idGenerator)
        {
            this.idGenerator = idGenerator;
            return this;
        }

        public MockClientBuilder SetStats(Stats stats)
        {
            this.stats = stats;
            return this;
        }

        public MockClientBuilder SetPing(Ping ping)
        {
            this.ping = ping;
            return this;
        }

        public MockClientBuilder SetupNetworkTime(Action<MockNetworkTimeBuilder> setupNetworkTime)
        {
            setupNetworkTime(networkTimeBuilder);
            return this;
        }

        public IClient Build()
        {
            if (buildExecuted)
            {
                return client ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;
            logger = Log.GetLogger<TestableBridgeBuilder>();
            idGenerator ??= new(Entity.ClientInitialIndex, Entity.MaxRelativeID, Entity.Relative, logger);
            mock = new(MockBehavior.Strict);
            SetupEvents();
            SetupProperties();
            SetupMethods();
            client = mock.Object;
            return mock.Object;

            void SetupEvents()
            {
                mock.SetupAdd(client => client.OnConnected += It.IsAny<Action<ClientID>>()).Callback<Action<ClientID>>(handler => OnConnected += handler);
                mock.SetupRemove(client => client.OnConnected -= It.IsAny<Action<ClientID>>()).Callback<Action<ClientID>>(handler => OnConnected -= handler);
                mock.SetupAdd(client => client.OnDisconnected += It.IsAny<Action<ConnectionCloseReason>>()).Callback<Action<ConnectionCloseReason>>(handler => OnDisconnected += handler);
                mock.SetupRemove(client => client.OnDisconnected -= It.IsAny<Action<ConnectionCloseReason>>()).Callback<Action<ConnectionCloseReason>>(handler => OnDisconnected -= handler);
                mock.SetupAdd(client => client.OnConnectionError += It.IsAny<Action<ConnectionException>>()).Callback<Action<ConnectionException>>(handler => OnConnectionError += handler);
                mock.SetupRemove(client => client.OnConnectionError -= It.IsAny<Action<ConnectionException>>()).Callback<Action<ConnectionException>>(handler => OnConnectionError -= handler);
                mock.SetupAdd(client => client.OnConnectedEndpoint += It.IsAny<Action<EndpointData>>()).Callback<Action<EndpointData>>(handler => OnConnectedEndpoint += handler);
                mock.SetupRemove(client => client.OnConnectedEndpoint -= It.IsAny<Action<EndpointData>>()).Callback<Action<EndpointData>>(handler => OnConnectedEndpoint -= handler);
                mock.SetupAdd(client => client.OnValidateConnectionRequest += It.IsAny<Action<ConnectionValidationRequest>>()).Callback<Action<ConnectionValidationRequest>>(handler => OnValidateConnectionRequest += handler);
                mock.SetupRemove(client => client.OnValidateConnectionRequest -= It.IsAny<Action<ConnectionValidationRequest>>()).Callback<Action<ConnectionValidationRequest>>(handler => OnValidateConnectionRequest -= handler);
                mock.SetupAdd(client => client.OnEntityCreated += It.IsAny<Action<Entity, IncomingEntityUpdate>>()).Callback<Action<Entity, IncomingEntityUpdate>>(handler => OnEntityCreated += handler);
                mock.SetupRemove(client => client.OnEntityCreated -= It.IsAny<Action<Entity, IncomingEntityUpdate>>()).Callback<Action<Entity, IncomingEntityUpdate>>(handler => OnEntityCreated -= handler);
                mock.SetupAdd(client => client.OnEntityUpdated += It.IsAny<Action<Entity, IncomingEntityUpdate>>()).Callback<Action<Entity, IncomingEntityUpdate>>(handler => OnEntityUpdated += handler);
                mock.SetupRemove(client => client.OnEntityUpdated -= It.IsAny<Action<Entity, IncomingEntityUpdate>>()).Callback<Action<Entity, IncomingEntityUpdate>>(handler => OnEntityUpdated -= handler);
                mock.SetupAdd(client => client.OnEntityDestroyed += It.IsAny<Action<Entity, DestroyReason>>()).Callback<Action<Entity, DestroyReason>>(handler => OnEntityDestroyed += handler);
                mock.SetupRemove(client => client.OnEntityDestroyed -= It.IsAny<Action<Entity, DestroyReason>>()).Callback<Action<Entity, DestroyReason>>(handler => OnEntityDestroyed -= handler);
                mock.SetupAdd(client => client.OnCommand += It.IsAny<Action<IEntityCommand>>()).Callback<Action<IEntityCommand>>(handler => OnCommand += handler);
                mock.SetupRemove(client => client.OnCommand -= It.IsAny<Action<IEntityCommand>>()).Callback<Action<IEntityCommand>>(handler => OnCommand -= handler);
                mock.SetupAdd(client => client.OnInput += It.IsAny<Action<IEntityInput>>()).Callback<Action<IEntityInput>>(handler => OnInput += handler);
                mock.SetupRemove(client => client.OnInput -= It.IsAny<Action<IEntityInput>>()).Callback<Action<IEntityInput>>(handler => OnInput -= handler);
                mock.SetupAdd(client => client.OnAuthorityRequested += It.IsAny<Action<AuthorityRequest>>()).Callback<Action<AuthorityRequest>>(handler => OnAuthorityRequested += handler);
                mock.SetupRemove(client => client.OnAuthorityRequested -= It.IsAny<Action<AuthorityRequest>>()).Callback<Action<AuthorityRequest>>(handler => OnAuthorityRequested -= handler);
                mock.SetupAdd(client => client.OnAuthorityRequestRejected += It.IsAny<Action<AuthorityRequestRejection>>()).Callback<Action<AuthorityRequestRejection>>(handler => OnAuthorityRequestRejected += handler);
                mock.SetupRemove(client => client.OnAuthorityRequestRejected -= It.IsAny<Action<AuthorityRequestRejection>>()).Callback<Action<AuthorityRequestRejection>>(handler => OnAuthorityRequestRejected -= handler);
                mock.SetupAdd(client => client.OnAuthorityChange += It.IsAny<Action<AuthorityChange>>()).Callback<Action<AuthorityChange>>(handler => OnAuthorityChange += handler);
                mock.SetupRemove(client => client.OnAuthorityChange -= It.IsAny<Action<AuthorityChange>>()).Callback<Action<AuthorityChange>>(handler => OnAuthorityChange -= handler);
                mock.SetupAdd(client => client.OnAuthorityTransferred += It.IsAny<Action<Entity>>()).Callback<Action<Entity>>(handler => OnAuthorityTransferred += handler);
                mock.SetupRemove(client => client.OnAuthorityTransferred -= It.IsAny<Action<Entity>>()).Callback<Action<Entity>>(handler => OnAuthorityTransferred -= handler);
                mock.SetupAdd(client => client.OnSceneIndexChanged += It.IsAny<Action<SceneIndexChanged>>()).Callback<Action<SceneIndexChanged>>(handler => OnSceneIndexChanged += handler);
                mock.SetupRemove(client => client.OnSceneIndexChanged -= It.IsAny<Action<SceneIndexChanged>>()).Callback<Action<SceneIndexChanged>>(handler => OnSceneIndexChanged -= handler);
            }

            void SetupProperties()
            {
                mock.Setup(client => client.ClientID).Returns(() => clientID);
                mock.Setup(client => client.NetworkTime).Returns(NetworkTime);
                mock.Setup(client => client.ConnectionType).Returns(() => connectionType);
                mock.Setup(client => client.Hostname).Returns(() => hostname);
                mock.Setup(client => client.Stats).Returns(() => stats);
                mock.Setup(client => client.ConnectionState).Returns(() => connectionState);
                mock.Setup(client => client.Ping).Returns(() => ping);
                mock.Setup(client => client.LastEndpointData).Returns(() => lastEndpointData);
                mock.Setup(client => client.ConnectionSettings).Returns(() => connectionSettings);
                mock.Setup(client => client.InitialScene).Returns(() => initialScene);
                mock.SetupSet(client => client.InitialScene = It.IsAny<uint>()).Callback<uint>(value => initialScene = value);
                mock.Setup(client => client.ValidatedHostPayload).Returns(() => validatedHostPayload);
                mock.Setup(client => client.ConnectionValidationPayload).Returns(() => connectionValidationPayload);
                mock.SetupSet(client => client.ConnectionValidationPayload = It.IsAny<CustomPayload>()).Callback<CustomPayload>(value => connectionValidationPayload = value);
                mock.Setup(client => client.SendFrequency).Returns(sendFrequency);
            }

            void SetupMethods()
            {
                mock.Setup(client => client.Connect(It.IsAny<EndpointData>(), It.IsAny<ConnectionSettings>(), It.IsAny<ConnectionType>())).Callback<EndpointData, ConnectionSettings, ConnectionType>((endpointData, connectionSettings, connectionType) =>
                {
                    if (connectionState is ConnectionState.Connected)
                    {
                        return;
                    }

                    lastEndpointData = endpointData;
                    this.connectionSettings = connectionSettings;
                    this.connectionType = connectionType;
                    connectionState = ConnectionState.Connected;
                    OnConnected?.Invoke(clientID);
                });

                mock.Setup(client => client.IsConnected()).Returns(() => connectionState is ConnectionState.Connected);
                mock.Setup(client => client.IsDisconnected()).Returns(() => connectionState is ConnectionState.Disconnected);
                mock.Setup(client => client.IsConnecting()).Returns(() => connectionState is ConnectionState.Connecting);

                mock.Setup(client => client.UpdateReceiving());
                mock.Setup(client => client.UpdateSending());

                mock.Setup(client => client.Disconnect()).Callback(() =>
                {
                    if (connectionState is ConnectionState.Disconnected)
                    {
                        return;
                    }

                    connectionState = ConnectionState.Disconnected;
                    OnDisconnected?.Invoke(ConnectionCloseReason.GracefulClose);
                });

                mock.Setup(client => client.Reconnect()).Callback(() =>
                {
                    if (connectionState is ConnectionState.Connected)
                    {
                        return;
                    }

                    connectionState = ConnectionState.Connected;
                    OnConnected?.Invoke(clientID);
                });

                mock.Setup(client => client.KickConnection(It.IsAny<ClientID>(), It.IsAny<CustomPayload>())).Callback<ClientID, CustomPayload>((clientId, _) =>
                {
                    if (clientId == clientID)
                    {
                        connectionState = ConnectionState.Disconnected;
                        OnDisconnected?.Invoke(ConnectionCloseReason.KickedByHost);
                    }
                });

                mock.Setup(client => client.CreateEntity(It.IsAny<ICoherenceComponentData[]>(), It.IsAny<bool>(), It.IsAny<ChannelID>())).Returns(() =>
                {
                    idGenerator.GetEntity(out var entity);
                    var incomingEntityUpdate = new IncomingEntityUpdate { Meta = new() { EntityId = entity, HasStateAuthority = true, HasInputAuthority = true, Operation = EntityOperation.Create, DestroyReason = DestroyReason.BadReason }, Components = DeltaComponents.New(0) };
                    var spawnInfo = new SpawnInfo { assetId = "", clientId = client.ClientID };
                    MockSpawnInfo.Instance.SetSpawnInfo(incomingEntityUpdate, spawnInfo, shouldSpawn: true);
                    OnEntityCreated?.Invoke(entity, incomingEntityUpdate);
                    return entity;
                });

                mock.Setup(client => client.CanSendUpdates(It.IsAny<Entity>())).Returns(true);
                mock.Setup(client => client.UpdateComponents(It.IsAny<Entity>(), It.IsAny<ICoherenceComponentData[]>())).Callback<Entity, ICoherenceComponentData[]>((entity, _) => OnEntityUpdated?.Invoke(entity, new()));
                mock.Setup(client => client.RemoveComponents(It.IsAny<Entity>(), It.IsAny<uint[]>())).Callback<Entity, uint[]>((entity, _) => OnEntityUpdated?.Invoke(entity, new()));
                mock.Setup(client => client.DestroyEntity(It.IsAny<Entity>())).Callback<Entity>(entity => OnEntityDestroyed?.Invoke(entity, DestroyReason.ClientDestroy));
                mock.Setup(client => client.HasAuthorityOverEntity(It.IsAny<Entity>(), It.IsAny<AuthorityType>())).Returns(() => true);
                mock.Setup(client => client.IsEntityInAuthTransfer(It.IsAny<Entity>())).Returns(() => false);
                mock.Setup(client => client.SendCommand(It.IsAny<IEntityCommand>(), It.IsAny<ChannelID>())).Callback<IEntityCommand, ChannelID>((command, _) => OnCommand?.Invoke(command));
                mock.Setup(client => client.SendInput(It.IsAny<IEntityInput>(), It.IsAny<long>(), It.IsAny<Entity>())).Callback<IEntityInput, long, Entity>((input, _, _) => OnInput?.Invoke(input));

                mock.Setup(client => client.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>())).Callback<Entity, AuthorityType>((entity, authorityType) =>
                {
                    var request = new AuthorityRequest(entity, clientID, authorityType);
                    OnAuthorityRequested?.Invoke(request);
                    OnAuthorityTransferred?.Invoke(entity);
                    OnAuthorityChange?.Invoke(new(entity, authorityType));
                });

                mock.Setup(client => client.SendAdoptOrphanRequest(It.IsAny<Entity>())).Callback<Entity>(entity =>
                {
                    OnAuthorityTransferred?.Invoke(entity);
                    OnAuthorityChange?.Invoke(new(entity, AuthorityType.Full));
                });

                mock.Setup(client => client.SendAuthorityTransfer(It.IsAny<Entity>(), It.IsAny<ClientID>(), It.IsAny<bool>(), It.IsAny<AuthorityType>())).Callback<Entity, ClientID, bool, AuthorityType>((entity, _, _, authorityType) =>
                {
                    OnAuthorityTransferred?.Invoke(entity);
                    OnAuthorityChange?.Invoke(new(entity, authorityType));
                });

                mock.Setup(client => client.SetFloatingOrigin(It.IsAny<Vector3d>())).Callback<Vector3d>(origin => floatingOrigin = origin);
                mock.Setup(client => client.GetFloatingOrigin()).Returns(() => floatingOrigin);

                mock.Setup(client => client.SetTransportType(It.IsAny<TransportType>(), It.IsAny<TransportConfiguration>()));
                mock.Setup(client => client.SetTransportFactory(It.IsAny<ITransportFactory>()));

                mock.Setup(client => client.Dispose()).Callback(() =>
                {
                    if (connectionState is ConnectionState.Disconnected)
                    {
                        return;
                    }

                    connectionState = ConnectionState.Disconnected;
                    OnDisconnected?.Invoke(ConnectionCloseReason.GracefulClose);
                });
            }
        }

        public void Dispose()
        {
            logger?.Dispose();
            client = null;
            buildExecuted = false;
        }

        /// <summary>
        /// Raise the <see cref="IClient.OnDisconnected"/> event.
        /// </summary>
        public void RaiseOnDisconnected(ConnectionCloseReason reason = ConnectionCloseReason.Unknown) => Mock.Raise(client => client.OnDisconnected += null, reason);

        /// <summary>
        /// Raise the <see cref="IClient.OnConnected"/> event.
        /// </summary>
        public void RaiseOnConnected(ConnectionCloseReason clientID) => Mock.Raise(client => client.OnConnected += null, clientID);

        /// <summary>
        /// Raise the <see cref="IClient.OnValidateConnectionRequest"/> event.
        /// </summary>
        public void RaiseOnValidateConnectionRequest(ConnectionValidationRequest request) => Mock.Raise(client => client.OnValidateConnectionRequest += null, request);

        /// <summary>
        /// Raise the <see cref="IClient.OnConnectionError"/> event.
        /// </summary>
        public void RaiseOnConnectionError(ConnectionException exception) => Mock.Raise(client => client.OnConnectionError += null, exception);
    }
}
