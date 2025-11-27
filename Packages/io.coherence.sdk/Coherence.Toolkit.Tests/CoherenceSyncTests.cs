// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_EDITOR

namespace Coherence.Toolkit.Tests
{
    using Moq;
    using NUnit.Framework;
    using ProtocolDef;
    using System;
    using Bindings;
    using UnityEngine;
    using Entities;
    using Log;
    using System.Collections.Generic;
    using Coherence.Tests;
    using Index = Entities.Index;

    public class CoherenceSyncTests : CoherenceTest
    {
        private static object[] commonTestCases =
        {
            new object[] {MessageTarget.StateAuthorityOnly, MessageTarget.StateAuthorityOnly, true},
            new object[] {MessageTarget.StateAuthorityOnly, MessageTarget.All, true},
            new object[] {MessageTarget.StateAuthorityOnly, MessageTarget.Other, true},
            new object[] {MessageTarget.All, MessageTarget.StateAuthorityOnly, false},
            new object[] {MessageTarget.All, MessageTarget.All, true},
            new object[] {MessageTarget.All, MessageTarget.Other, true},
            new object[] {MessageTarget.Other, MessageTarget.StateAuthorityOnly, false},
            new object[] {MessageTarget.Other, MessageTarget.All, true},
            new object[] {MessageTarget.Other, MessageTarget.Other, true},
        };

        [Test]
        [TestCaseSource(nameof(commonTestCases))]
        public void ReceiveCommand_Baked_HandlesRouting(MessageTarget commandTarget, MessageTarget routing, bool expectReceived)
        {
            // Arrange
            var mockSync = new Mock<ICoherenceSync>();

            var bakedScript = new CoherenceSyncBakedMock();
            var bridgeGo = new GameObject();
            var bridge = bridgeGo.AddComponent<CoherenceBridge>();

            mockSync.Setup(cs => cs.BakedScript).Returns(bakedScript);
            mockSync.Setup(cs => cs.EntityState).Returns(new NetworkEntityState(Entity.InvalidRelative, AuthorityType.Full, false, false, mockSync.Object, String.Empty));
            mockSync.Setup(cs => cs.CoherenceBridge).Returns(bridge);

            IEntityCommand command = Mock.Of((IEntityCommand m) => m.Routing == routing);

            CommandsHandler handler = new CommandsHandler(mockSync.Object, new List<Binding>(), new UnityLogger());
            // Act
            handler.HandleCommand(command, commandTarget);

            // Assert
            Assert.That(bakedScript.TimesCalled(nameof(CoherenceSyncBaked.ReceiveCommand)), Is.EqualTo(expectReceived ? 1 : 0));
        }

        [Test]
        [Description("CoherenceBridge instance will not be destroyed when the updater is null")]
        public void HandleConnected_HandleNetworkedDestruction_BridgeNotDestroyed_WhenUpdaterIsNull()
        {
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            using var mockBridgeBuilder = new MockBridgeBuilder();
            var bridge = mockBridgeBuilder.Build();

            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;

            sync.SetBridge(bridge);

            // Casts to interface needed because sync.CoherenceBridge returns instance of CoherenceBridge
            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.Not.Null);
            ((ICoherenceSync)sync).HandleNetworkedDestruction(false);
            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.Not.Null);
        }

        [Test]
        [Description("The current bridge connection is set to null when a destruction is called for over the network")]
        public void HandleConnected_HandleNetworkedDestruction_BridgeDestroyed_WhenUpdaterIsNotNull()
        {
            var updaterMock = new Mock<ICoherenceSyncUpdater>();
            var taggedForNetworkedDestruction = false;
            updaterMock.SetupGet(u => u.TaggedForNetworkedDestruction).Returns(() => taggedForNetworkedDestruction);
            updaterMock.SetupSet(u => u.TaggedForNetworkedDestruction = It.IsAny<bool>()).Callback<bool>(value => taggedForNetworkedDestruction = value);

            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.SetUpdater(updaterMock.Object);

            using var mockBridgeBuilder = new MockBridgeBuilder();
            var bridge = mockBridgeBuilder.Build();

            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;

            sync.SetBridge(bridge);

            // Casts to interface needed because sync.CoherenceBridge returns instance of CoherenceBridge
            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.Not.Null);
            ((ICoherenceSync)sync).HandleNetworkedDestruction(false);
            Assert.That(sync.IsBeingSynced, Is.False);
            updaterMock.VerifySet(u => u.TaggedForNetworkedDestruction = true, Times.Once());
        }

        [Test]
        [Description("Syncing network entity state is not called when there is no bridge")]
        public void HandleConnected_BridgeIsNull_SyncNetworkEntityState_NotCalled()
        {
            var updaterMock = new Mock<ICoherenceSyncUpdater>();
            updaterMock.Setup(u => u.TaggedForNetworkedDestruction).Returns(false);

            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.SetUpdater(updaterMock.Object);

            using var mockBridgeBuilder = new MockBridgeBuilder();
            mockBridgeBuilder.SetupEntitiesManager(x => x.SetSyncNetworkEntityStateThrows(new ArgumentNullException()));
            mockBridgeBuilder.Build();

            Assert.That(sync.CoherenceBridge, Is.Null);

            sync.EntityState = new(new Entity(0,0,false), AuthorityType.Input, false, true, sync, "uuid");
            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;

            mockBridgeBuilder.RaiseOnConnectedInternal();
            mockBridgeBuilder.MockEntitiesManagerBuilder.Mock.Verify(m => m.SyncNetworkEntityState(It.IsAny<ICoherenceSync>()), Times.Never);
        }

        [Test]
        [Description("Syncing the entity state happens on a new connection with a valid bridge")]
        public void HandleConnected_BridgeIsNotNull_SyncNetworkEntityState_Called()
        {
            var updaterMock = new Mock<ICoherenceSyncUpdater>();
            updaterMock.Setup(u => u.TaggedForNetworkedDestruction).Returns(false);

            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.SetUpdater(updaterMock.Object);
            var componentUpdates = new ComponentUpdates();

            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetupEntitiesManager(x => x.SetSyncNetworkEntityStateReturns(()=> (null, componentUpdates, 0, false)))
                .SetIsConnected(true)
                .SetClientID(new(1));

            mockBridgeBuilder.EntitiesManager.SetClient(mockBridgeBuilder.Client);

            var bridge = mockBridgeBuilder.Build();
            sync.SetBridge(bridge);
            sync.ConnectBridge(bridge);
            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.Not.Null);

            sync.EntityState = new NetworkEntityState(new Entity(0, 0, false), AuthorityType.Input, false, true, sync, "uuid");
            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;
            mockBridgeBuilder.RaiseOnConnectedInternal();
            mockBridgeBuilder.MockEntitiesManagerBuilder.Mock.Verify(m => m.SyncNetworkEntityState(It.IsAny<ICoherenceSync>()), Times.Once);
        }

        [Test]
        [Description("Ensure that CoherenceSync.CoherenceBridge will continue to return the bridge " +
                     "that the CoherenceSync was connected to even after the networked entity has been destroyed.\n\n" +
                     "This can help avoid resource leaks if other components try to access the CoherenceSync's bridge " +
                     "after the entity has been destroyed - e.g. to unsubscribe event handlers from CoherenceBridge's events.")]
        public void HandleNetworkedDestruction_Does_Not_Set_Bridge_To_Null()
        {
            var updaterMock = new Mock<ICoherenceSyncUpdater>();
            updaterMock.Setup(u => u.TaggedForNetworkedDestruction).Returns(false);
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.SetUpdater(updaterMock.Object);
            using var mockBridgeBuilder = new MockBridgeBuilder();
            var bridge = mockBridgeBuilder.Build();
            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;
            sync.SetBridge(bridge);

            ((ICoherenceSync)sync).HandleNetworkedDestruction(false);

            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.EqualTo(bridge));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ConnectedEntityChanged_Does_Not_Raise_ConnectedEntityChangeOverride_If_Connected_Entity_Did_Not_Change(bool hasCoherenceNode, bool parentIsNull)
        {
            var entity = parentIsNull ? Entity.InvalidRelative : new((Index)1u, 1, isAbsolute: false);
            var newParentEntity = entity;
            var oldParentEntity = entity;
            var parentSync = parentIsNull ? CoherenceSync.Create() : null;
            using var mockBridgeBuilder = new MockBridgeBuilder();
            mockBridgeBuilder.GetCoherenceSyncForEntityReturns(e => e == entity ? parentSync : null);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            ((ICoherenceSync)sync).ConnectedEntityChanged(oldParentEntity, out _);
            var wasEventRaised = false;
            sync.ConnectedEntityChangeOverride += _ => wasEventRaised = true;

            ((ICoherenceSync)sync).ConnectedEntityChanged(newParentEntity, out _);

            Assert.That(wasEventRaised, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConnectedEntityChanged_Raises_ConnectedEntityChangeOverride_If_Connected_Entity_Changed_And_No_CoherenceNode_Is_Attached(bool newParentIsNull)
        {
            var notNullEntity = new Entity((Index)1u, 1, isAbsolute: false);
            var newParentEntity = newParentIsNull ? Entity.InvalidRelative : notNullEntity;
            var oldParentEntity = newParentIsNull ? notNullEntity : Entity.InvalidRelative;
            var oldParentSync = newParentIsNull ? CoherenceSync.Create() : null;
            var newParentSync = newParentIsNull ? null : CoherenceSync.Create();
            using var mockBridgeBuilder = new MockBridgeBuilder();
            mockBridgeBuilder.GetCoherenceSyncForEntityReturns(e => e == newParentEntity ? newParentSync : e == oldParentEntity ? oldParentSync : null);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            ((ICoherenceSync)sync).ConnectedEntityChanged(oldParentEntity, out _);
            var wasEventRaised = false;
            sync.ConnectedEntityChangeOverride += _ => wasEventRaised = true;

            ((ICoherenceSync)sync).ConnectedEntityChanged(newParentEntity, out _);

            Assert.That(wasEventRaised, Is.True);
        }
    }
}

#endif // UNITY_EDITOR
