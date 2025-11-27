// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using Entities;
    using Moq;
    using SimulationFrame;
    using UnityEngine;
    using static CoherenceSync;
    using Object = UnityEngine.Object;

    public sealed class MockSyncBuilder : IDisposable
    {
        private static readonly Entity DefaultID = new((Entities.Index)1, 0, Entity.Relative);

        private Entity entityID = DefaultID;
        private AuthorityType authorityType = AuthorityType.Full;
        private bool isOrphaned;
        private Func<bool> isDestroyed;
        private bool isNetworkInstantiated;
        private bool isUnique;
        private string uuid = string.Empty;
        private string manualUUID = string.Empty;
        private SimulationType simulationTypeConfig = SimulationType.ClientSide;
        private bool isChildFromSyncGroup;
        private ICoherenceComponentData[] initialComps = Array.Empty<ICoherenceComponentData>();
        private CoherenceSyncBaked bakedScript;
        private InterpolationLoop interpolationLocationConfig = InterpolationLoop.Update;
        private bool hasInput;
        private string assetID = string.Empty;
        private IncomingEntityUpdate? update;
        private SpawnInfo? spawnInfo;
        private bool shouldSpawn = true;
        private UnsyncedNetworkEntityPriority unsyncedEntityPriority = UnsyncedNetworkEntityPriority.AssetId;
        private Func<bool> adopt;
        private Func<bool> requestAuthority;
        private AuthorityType requestAuthorityType;
        private string name = "MockSync";
        private Action onHandleDisconnected;
        private Exception handleDisconnectedThrows;
        private Mock<ICoherenceSync> mock;
        private Mock<ICoherenceSyncUpdater> mockUpdater;
        private MockNetworkObjectInstantiatorBuilder networkObjectInstantiatorBuilder;
        private bool buildExecuted;
        private Result? result;
        private CoherenceSyncConfig config;
        private bool disposed;

        public MockNetworkObjectInstantiatorBuilder NetworkObjectInstantiatorBuilder
            => networkObjectInstantiatorBuilder ??= (new MockNetworkObjectInstantiatorBuilder().SetSyncBuilder(this));

        public ICoherenceSync Sync => Build().AsSync();
        public Mock<ICoherenceSync> Mock => Build().mockSync;

        private Mock<ICoherenceSyncUpdater> MockUpdater
        {
            get
            {
                if (mockUpdater is null)
                {
                    Build();
                }

                return mockUpdater;
            }
        }

        public MockSyncBuilder Name(string name)
        {
            this.name = name;
            return this;
        }

        public MockSyncBuilder EntityID(Entity entityID)
        {
            this.entityID = entityID;
            return this;
        }

        public MockSyncBuilder SetAuthorityType(AuthorityType authorityType)
        {
            this.authorityType = authorityType;
            return this;
        }

        public MockSyncBuilder SetSimulationTypeConfig(SimulationType simulationTypeConfig)
        {
            this.simulationTypeConfig = simulationTypeConfig;
            return this;
        }

        public MockSyncBuilder SetIsOrphaned(bool isOrphaned = true)
        {
            this.isOrphaned = isOrphaned;
            return this;
        }

        public MockSyncBuilder SetIsDestroyed(bool isDestroyed) => SetIsDestroyed(() => isDestroyed);

        public MockSyncBuilder SetIsDestroyed(Func<bool> isDestroyed = null)
        {
            this.isDestroyed = isDestroyed ?? (() => true);
            return this;
        }

        public MockSyncBuilder SetHandleDisconnected(Action onHandleDisconnected)
        {
            this.onHandleDisconnected = onHandleDisconnected;
            return this;
        }

        public MockSyncBuilder SetHandleDisconnected(Exception handleDisconnectedThrows)
        {
            this.handleDisconnectedThrows = handleDisconnectedThrows;
            return this;
        }

        public MockSyncBuilder SetIsNetworkInstantiated(bool isNetworkInstantiated = true)
        {
            this.isNetworkInstantiated = isNetworkInstantiated;
            return this;
        }

        public MockSyncBuilder SetUUID(string uuid)
        {
            this.uuid = uuid;
            return this;
        }

        public MockSyncBuilder SetIsUnique(bool isUnique = true)
        {
            this.isUnique = isUnique;
            return this;
        }

        public MockSyncBuilder SetManualUUID(string manualUUID)
        {
            this.manualUUID = manualUUID;
            return this;
        }

        public MockSyncBuilder SetIsChildFromSyncGroup(bool isChildFromSyncGroup = true)
        {
            this.isChildFromSyncGroup = isChildFromSyncGroup;
            return this;
        }

        public MockSyncBuilder SetInitialComps(ICoherenceComponentData[] initialComps)
        {
            this.initialComps = initialComps;
            return this;
        }

        public MockSyncBuilder SetBakedScript(CoherenceSyncBaked bakedScript)
        {
            this.bakedScript = bakedScript;
            return this;
        }

        public MockSyncBuilder SetInterpolationLocationConfig(InterpolationLoop interpolationLocationConfig)
        {
            this.interpolationLocationConfig = interpolationLocationConfig;
            return this;
        }

        public MockSyncBuilder SetHasInput(bool hasInput = true)
        {
            this.hasInput = hasInput;
            return this;
        }

        public MockSyncBuilder SetAssetID(string ID)
        {
            assetID = ID;
            return this;
        }

        public MockSyncBuilder SetUpdate(IncomingEntityUpdate update)
        {
            this.update = update;
            return this;
        }

        public MockSyncBuilder SetSpawnInfo(SpawnInfo spawnInfo, bool shouldSpawn)
        {
            this.spawnInfo = spawnInfo;
            this.shouldSpawn = shouldSpawn;
            return this;
        }

        public MockSyncBuilder SetUnsyncedEntityPriority(UnsyncedNetworkEntityPriority unsyncedEntityPriority)
        {
            this.unsyncedEntityPriority = unsyncedEntityPriority;
            return this;
        }

        public MockSyncBuilder SetAdoptReturns(bool result) => SetAdoptReturns(() => result);

        public MockSyncBuilder SetAdoptReturns(Func<bool> adopt)
        {
            this.adopt = adopt;
            return this;
        }

        public MockSyncBuilder SetRequestAuthorityReturns(AuthorityType authorityType, bool result)
            => SetRequestAuthorityReturns(authorityType, () => result);

        public MockSyncBuilder SetRequestAuthorityReturns(AuthorityType authorityType, Func<bool> requestAuthority)
        {
            this.requestAuthority = requestAuthority;
            this.requestAuthorityType = authorityType;
            return this;
        }

        public MockSyncBuilder SetInstantiatorDestroy(Exception instantiatorDestroyThrows)
        {
            NetworkObjectInstantiatorBuilder.SetDestroyCallback(_ => throw instantiatorDestroyThrows);
            return this;
        }

        public MockSyncBuilder SetNetworkObjectInstantiatorBuilder(MockNetworkObjectInstantiatorBuilder builder)
        {
            networkObjectInstantiatorBuilder = builder;
            return this;
        }

        public Result Build()
        {
            if (buildExecuted)
            {
                return result ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;

            mockUpdater = new(MockBehavior.Strict);
            _ = mockUpdater.Setup(updater => updater.SampleAllBindings(0));
            _ = mockUpdater.Setup(updater => updater.GetComponentUpdates(It.IsAny<List<ICoherenceComponentData>>(),
                It.IsAny<double>(), It.IsAny<AbsoluteSimulationFrame>(), It.IsAny<bool>()));
            _ = mockUpdater.Setup(updater => updater.ApplyComponentDestroys(It.IsAny<HashSet<uint>>()));
            _ = mockUpdater.Setup(updater => updater.ApplyComponentUpdates(It.IsAny<ComponentUpdates>()));
            _ = mockUpdater.Setup(updater => updater.ApplyConnectedEntityChanges());

            mock = new(MockBehavior.Strict);
            config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            config.Init(spawnInfo?.assetId ?? assetID);
            config.Instantiator = NetworkObjectInstantiatorBuilder
                .Build();
            var networkObjectProvider = new Mock<INetworkObjectProvider>(MockBehavior.Strict);
            _ = networkObjectProvider.Setup(provider => provider.LoadAsset(It.IsAny<string>(), It.IsAny<Action<ICoherenceSync>>()))
                .Callback((string _, Action<ICoherenceSync> onLoaded) => onLoaded(spawnInfo?.prefab));
            config.Provider = networkObjectProvider.Object;
            _ = CoherenceSyncConfigRegistry.Instance.Register(config);

            var entityState = new NetworkEntityState(entityID, authorityType, isOrphaned, isNetworkInstantiated, mock.Object, uuid);
            _ = mock.Setup(sync => sync.EntityState).Returns(entityState);
            _ = mock.Setup(sync => sync.HasStateAuthority).Returns(authorityType is AuthorityType.State or AuthorityType.Full);
            _ = mock.Setup(sync => sync.IsSynchronizedWithNetwork).Returns(true);
            _ = mock.Setup(sync => sync.CoherenceSyncConfig).Returns(config);
            _ = mock.Setup(sync => sync.ManualUniqueId).Returns(manualUUID);
            _ = mock.Setup(sync => sync.SimulationTypeConfig).Returns(simulationTypeConfig);
            _ = mock.Setup(sync => sync.IsUnique).Returns(isUnique);
            _ = mock.Setup(sync => sync.IsChildFromSyncGroup()).Returns(isChildFromSyncGroup);
            _ = mock.Setup(sync => sync.BakedScript).Returns(bakedScript);
            _ = mock.Setup(sync => sync.InterpolationLocationConfig).Returns(interpolationLocationConfig);
            _ = mock.Setup(sync => sync.HasInput).Returns(hasInput);
            _ = mock.Setup(sync => sync.IsOrphaned).Returns(isOrphaned);
            _ = mock.Setup(sync => sync.IsDestroyed).Returns(isDestroyed ??= (() => false));
            _ = mock.Setup(sync => sync.Updater).Returns(mockUpdater.Object);
            _ = mock.Setup(sync => sync.UnsyncedEntityPriority).Returns(unsyncedEntityPriority);
            _ = mock.Setup(sync => sync.name).Returns(this.name);
            _ = mock.Setup(sync => sync.UsesLODsAtRuntime).Returns(false);
            _ = mock.Setup(sync => sync.SynchronizationChannel).Returns(Channel.Default.Name);

            SetupHandleDisconnected();

            if (adopt is not null)
            {
                _ = mock.Setup(sync => sync.Adopt()).Returns(adopt);
            }

            if (requestAuthority is not null)
            {
                _ = mock.Setup(sync => sync.RequestAuthority(requestAuthorityType)).Returns(requestAuthority);
            }

            PrepareSyncInitialComps(initialComps);

            if (update != null && spawnInfo != null)
            {
                MockSpawnInfo.Instance.SetSpawnInfo(update.Value, spawnInfo.Value, shouldSpawn);
            }

            result = new(mock, mockUpdater);
            return result.Value;

            void SetupHandleDisconnected()
            {
                onHandleDisconnected ??= () => { };
                _ = mock.Setup(client => client.HandleDisconnected()).Callback(onHandleDisconnected);

                if (handleDisconnectedThrows is not null)
                {
                    _ = mock.Setup(client => client.HandleDisconnected()).Throws(handleDisconnectedThrows);
                }
            }

            static void PrepareSyncInitialComps(ICoherenceComponentData[] comps) => Impl.CreateInitialComponents = (_, _, _, _) => comps;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            networkObjectInstantiatorBuilder?.Dispose();
            CoherenceSyncConfigRegistry.Instance.Deregister(config);
            if (config)
            {
                Object.DestroyImmediate(config);
            }
        }

        public readonly struct Result
        {
            public readonly Mock<ICoherenceSync> mockSync;
            public readonly Mock<ICoherenceSyncUpdater> mockUpdater;

            public Result(Mock<ICoherenceSync> mockSync, Mock<ICoherenceSyncUpdater> mockUpdater)
            {
                this.mockSync = mockSync;
                this.mockUpdater = mockUpdater;
            }

            public void Deconstruct(out Mock<ICoherenceSync> mockSync, out Mock<ICoherenceSyncUpdater> mockUpdater)
            {
                mockSync = this.mockSync;
                mockUpdater = this.mockUpdater;
            }

            public static implicit operator Mock<ICoherenceSync>(Result result) => result.mockSync;

            public ICoherenceSync AsSync() => mockSync.Object;
        }
    }
}
