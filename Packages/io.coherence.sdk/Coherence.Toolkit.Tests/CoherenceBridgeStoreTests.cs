namespace Coherence.Toolkit.Tests
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Cloud;
    using Coherence.Tests;
    using Log;
    using NUnit.Framework;
    using Runtime.Tests;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.TestTools;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceBridgeStore"/>.
    /// </summary>
    public class CoherenceBridgeStoreTests : CoherenceTest
    {
        private readonly List<TestableBridgeBuilder> bridgeBuilders = new();
        private Scene scene;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            scene = SceneManager.GetActiveScene();
        }

        [TearDown]
        public override void TearDown()
        {
            bridgeBuilders.ForEach(x => x.Dispose());
            bridgeBuilders.Clear();
            base.TearDown();
            Assert.That(CoherenceBridgeStore.bridges.Count, Is.Zero);
            Assert.That(CoherenceBridgeStore.MasterBridge, Is.Null);
        }

        private CoherenceBridge CreateBridge(bool isMain = false)
        {
            var builder = new TestableBridgeBuilder().SetIsMain(isMain);
            bridgeBuilders.Add(builder);
            return builder.Build();
        }

        [Test]
        public void MasterBridge_Returns_First_Bridge_Registered_As_Main()
        {
            CreateBridge(isMain: false);
            var secondBridge = CreateBridge(isMain: false);
            var thirdBridge = CreateBridge(isMain: false);

            CoherenceBridgeStore.RegisterBridge(secondBridge, scene.handle, isMaster: true);
            CoherenceBridgeStore.RegisterBridge(thirdBridge, scene.handle, isMaster: true);

            Assert.That(CoherenceBridgeStore.MasterBridge, Is.EqualTo(secondBridge));
        }

        [Test]
        public void MasterBridge_Returns_Null_When_No_Main_Bridge_Registered()
            => Assert.That(CoherenceBridgeStore.MasterBridge, Is.Null);

        [TestCase(false), TestCase(true)]
        public void RegisterBridge_Adds_Bridge_To_Store(bool isMain)
        {
            var bridge = CreateBridge(isMain: isMain);
            var sceneHandle = scene.handle;

            Assert.That(CoherenceBridgeStore.TryGetBridge(sceneHandle, out var retrievedBridge), Is.True);
            Assert.That(retrievedBridge, Is.EqualTo(bridge));
        }

        [TestCase(false), TestCase(true)]
        public void DeregisterBridge_By_Id_Removes_Bridge_From_Store(bool isMain)
        {
            CreateBridge(isMain: isMain);

            CoherenceBridgeStore.DeregisterBridge(scene.handle);

            Assert.That(CoherenceBridgeStore.TryGetBridge(scene.handle, out var result), Is.False);
            Assert.That(result, Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public void DeregisterBridge_By_Reference_Removes_Bridge_From_Store(bool isMain)
        {
            var bridge = CreateBridge(isMain: isMain);

            CoherenceBridgeStore.DeregisterBridge(bridge);

            Assert.That(CoherenceBridgeStore.TryGetBridge(scene.handle, out var result), Is.False);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void TryGetBridge_By_PlayerAccount_Returns_True_When_Bridge_With_Matching_CloudService_Exists()
        {
            using var cloudServiceBuilder = new FakeCloudServiceBuilder();
            var cloudService = cloudServiceBuilder.Build();
            using var playerAccount = new PlayerAccount(
                LoginInfo.WithPassword("Username", "Password", false),
                new CloudUniqueId("CloudUniqueId"),
                "ProjectId",
                cloudService);

            var bridge = CreateBridge(isMain: false);
            bridge.CloudService = cloudService;

            var result = CoherenceBridgeStore.TryGetBridge(playerAccount, out var retrievedBridge);

            Assert.That(result, Is.True);
            Assert.That(retrievedBridge, Is.EqualTo(bridge));
        }

        [Test]
        public void TryGetBridge_By_PlayerAccount_Returns_False_When_No_Bridge_With_Matching_CloudService_Exists()
        {
            using var cloudServiceBuilder = new FakeCloudServiceBuilder();
            var cloudService = cloudServiceBuilder.Build();
            using var playerAccount = new PlayerAccount(
                LoginInfo.WithPassword("Username", "Password", false),
                new("CloudUniqueId"),
                "ProjectId",
                cloudService);

            var result = CoherenceBridgeStore.TryGetBridge(playerAccount, out var retrievedBridge);

            Assert.That(result, Is.False);
            Assert.That(retrievedBridge, Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public void TryGetBridge_By_Predicate_Returns_True_When_Match_Found(bool isMain)
        {
            var bridge = CreateBridge(isMain: isMain);

            var result = CoherenceBridgeStore.TryGetBridge(x => x == bridge, out var retrievedBridge);
            Assert.That(result, Is.True);
            Assert.That(retrievedBridge, Is.EqualTo(bridge));
        }

        [TestCase(false), TestCase(true)]
        public void TryGetBridge_By_Predicate_Returns_False_When_No_Match_Found(bool isMain)
        {
            CreateBridge(isMain: isMain);

            var result = CoherenceBridgeStore.TryGetBridge(_ => false, out var retrievedBridge);

            Assert.That(result, Is.False);
            Assert.That(retrievedBridge, Is.Null);
        }

        [Test]
        public void BridgeResolve_Only_Accepts_Single_Event_Handler()
        {
            var firstResolverExecuted = false;
            CoherenceBridgeResolver<MonoBehaviour> resolver1 = _ =>
            {
                firstResolverExecuted = true;
                return null;
            };
            var secondResolverExecuted = false;
            CoherenceBridgeResolver<MonoBehaviour> resolver2 = _ =>
            {
                secondResolverExecuted = true;
                return null;
            };
            Error? loggedError = null;
            UnityLogger.OnLogErrorEventExt += OnLogErrorEventExt;

            try
            {
                CoherenceBridgeStore.BridgeResolve += resolver1;
                CoherenceBridgeStore.BridgeResolve += resolver2;
                CoherenceBridgeStore.TryGetBridge<MonoBehaviour>(scene, null, null, out _);
            }
            finally
            {
                CoherenceBridgeStore.BridgeResolve -= resolver1;
                CoherenceBridgeStore.BridgeResolve -= resolver2;
                UnityLogger.OnLogErrorEventExt -= OnLogErrorEventExt;
            }

            Assert.That(loggedError, Is.EqualTo(Error.ToolkitBridgeStoreBridgeResolveTooManyCallbacks));
            Assert.That(firstResolverExecuted, Is.True);
            Assert.That(secondResolverExecuted, Is.False);

            void OnLogErrorEventExt(object context, Error error, string message, (string key, object value)[] args)
            {
                loggedError = error;
                LogAssert.Expect(LogType.Error, new Regex(".*"));
            }
        }

        [Test]
        public void TryGetBridge_By_SceneHandle_Has_Expected_Bridge_Priority()
        {
            var sceneHandle = SceneManager.GetActiveScene().handle;
            var firstBridge = CreateBridge(isMain: false);

            try
            {
                // static BridgeResolve is ignored
                CoherenceBridgeStore.DeregisterBridge(firstBridge);
                CoherenceBridgeStore.BridgeResolve += GetFirstBridge;
                Assert.That(TryGet(out var result), Is.False);
                Assert.That(result, Is.Null);

                // Only bridge registered for scene found
                CoherenceBridgeStore.RegisterBridge(firstBridge, sceneHandle, isMaster: false);
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(firstBridge));

                // Second bridge registered for same scene takes precedence over first one
                var secondBridge = CreateBridge(isMain: false);
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(secondBridge));

                // Second bridge registered for same scene takes precedence over main bridge
                CoherenceBridgeStore.RegisterBridge(firstBridge, sceneHandle, isMaster: true);
                Assert.That(CoherenceBridgeStore.MasterBridge, Is.EqualTo(firstBridge));
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(secondBridge));

                // Instantiating bridge takes precedence over other bridges in the scene
                CoherenceBridgeStore.instantiatingBridge = firstBridge;
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(firstBridge));
            }
            finally
            {
                CoherenceBridgeStore.BridgeResolve -= GetFirstBridge;
            }

            bool TryGet(out CoherenceBridge foundBridge) => CoherenceBridgeStore.TryGetBridge(sceneHandle, out foundBridge);
            CoherenceBridge GetFirstBridge(MonoBehaviour resolvingComponent) => firstBridge;
        }

        [Test]
        public void TryGetBridge_By_Scene_Has_Expected_Bridge_Priority()
        {
            var firstBridge = CreateBridge(isMain: false);

            try
            {
                // static BridgeResolve is ignored
                CoherenceBridgeStore.DeregisterBridge(firstBridge);
                CoherenceBridgeStore.BridgeResolve += GetFirstBridge;
                Assert.That(TryGet(out var result), Is.False);
                Assert.That(result, Is.Null);

                // Only bridge registered for scene found
                CoherenceBridgeStore.RegisterBridge(firstBridge, scene, isMaster: false);
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(firstBridge));

                // Second bridge registered for same scene takes precedence over first one
                var secondBridge = CreateBridge(isMain: false);
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(secondBridge));

                // Second bridge registered for same scene takes precedence over main bridge
                CoherenceBridgeStore.RegisterBridge(firstBridge, scene.handle, isMaster: true);
                Assert.That(CoherenceBridgeStore.MasterBridge, Is.EqualTo(firstBridge));
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(secondBridge));

                // Instantiating bridge takes precedence over other bridges in the scene
                CoherenceBridgeStore.instantiatingBridge = firstBridge;
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(firstBridge));
            }
            finally
            {
                CoherenceBridgeStore.BridgeResolve -= GetFirstBridge;
            }

            bool TryGet(out CoherenceBridge foundBridge) => CoherenceBridgeStore.TryGetBridge(scene, out foundBridge);
            CoherenceBridge GetFirstBridge(MonoBehaviour resolvingComponent) => firstBridge;
        }

        [Test]
        public void TryGetBridge_With_Resolver_Has_Expected_Bridge_Priority()
        {
            var gameObject = new GameObject(nameof(TryGetBridge_With_Resolver_Has_Expected_Bridge_Priority));
            var client = gameObject.AddComponent<TestBehaviour>();
            CoherenceBridge secondBridge = null;

            try
            {
                // Only bridge registered for scene found
                var firstBridge = CreateBridge(isMain: false);
                Assert.That(TryGet(out var result), Is.True);
                Assert.That(result, Is.EqualTo(firstBridge));

                // Second bridge registered for same scene takes precedence over first one
                secondBridge = CreateBridge(isMain: false);
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(secondBridge));

                // Second bridge registered for same scene takes precedence over main bridge
                CoherenceBridgeStore.RegisterBridge(firstBridge, scene.handle, isMaster: true);
                Assert.That(CoherenceBridgeStore.MasterBridge, Is.EqualTo(firstBridge));
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(secondBridge));

                // Instantiating bridge takes precedence over other bridges in the scene
                CoherenceBridgeStore.instantiatingBridge = firstBridge;
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(firstBridge));

                // static BridgeResolve takes precedence over instantiating bridge
                CoherenceBridgeStore.BridgeResolve += GetSecondBridge;
                Assert.That(TryGet(out result), Is.True);
                Assert.That(result, Is.EqualTo(secondBridge));

                // Resolver passed to TryGetBridge method takes precedence over static BridgeResolve
                Assert.That(TryGetWithResolver(x => firstBridge, out result), Is.True);
                Assert.That(result, Is.EqualTo(firstBridge));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                CoherenceBridgeStore.BridgeResolve -= GetSecondBridge;
            }

            bool TryGet(out CoherenceBridge foundBridge) => CoherenceBridgeStore.TryGetBridge(scene, null, client, out foundBridge);
            bool TryGetWithResolver(CoherenceBridgeResolver<TestBehaviour> resolver, out CoherenceBridge foundBridge) => CoherenceBridgeStore.TryGetBridge(scene, resolver, client, out foundBridge);
            CoherenceBridge GetSecondBridge(MonoBehaviour resolvingComponent) => secondBridge;
        }

        private sealed class TestBehaviour : MonoBehaviour {}
    }
}
