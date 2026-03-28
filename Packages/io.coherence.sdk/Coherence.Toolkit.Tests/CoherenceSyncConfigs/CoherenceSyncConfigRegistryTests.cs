namespace Coherence.Toolkit.Tests
{
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using Coherence.Tests;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceSyncConfigRegistry"/>.
    /// </summary>
    public class CoherenceSyncConfigRegistryTests : CoherenceTest
    {
        private const string DefaultPrefabName = nameof(CoherenceSyncConfigRegistryTests);
        private const string DefaultPrefabPath = "Assets/" + DefaultPrefabName + ".prefab";
        private string prefabPath;
        private GameObject prefab;
        private CoherenceSync sync;
        private CoherenceSyncConfigRegistry registry;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            prefabPath = AssetDatabase.GenerateUniqueAssetPath(DefaultPrefabPath);
            prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                ObjectFactory.CreateGameObject(DefaultPrefabName, typeof(CoherenceSync)),
                prefabPath, InteractionMode.AutomatedAction);
            sync = prefab.GetComponent<CoherenceSync>();
            registry = CoherenceSyncConfigRegistry.Instance;
            registry.ResetState();
        }

        [TearDown]
        public override void TearDown()
        {
            AssetDatabase.DeleteAsset(prefabPath);
            registry.ResetState();

            base.TearDown();
        }

        [Test]
        [Description("Verifies that created CoherenceSyncConfig objects are registered using the ID.GetHashCode().")]
        public void TestConfigHashCodeUsed()
        {
            // Arrange / Act - create a CoherenceSyncPrefab which automatically registers.
            var config = sync.CoherenceSyncConfig;
            var networkID = config.GetNetworkAssetId();

            // Assert
            Assert.True(registry.GetFromNetworkId(networkID, out var testConfig));
            Assert.That(testConfig, Is.EqualTo(config));
        }

        [Test]
        [Description("Verifies that deleted configs are removed from the registry network IDs.")]
        public void TestDestroyedConfigsRemovedFromNetworkID()
        {
            // Arrange / Act - create a CoherenceSyncPrefab which automatically registers.
            var config = sync.CoherenceSyncConfig;
            var networkID = config.GetNetworkAssetId();

            // Act
            AssetDatabase.DeleteAsset(prefabPath);

            // Assert
            Assert.False(registry.GetFromNetworkId(networkID, out var testConfig));
        }

        [Test]
        public void CleanUp_Is_Idempotent()
        {
            var config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            var instantiator = new FakeInstantiator();
            using var serializedObject = new SerializedObject(config);

            using var instantiatorProperty = serializedObject.FindProperty(CoherenceSyncConfig.Property.objectInstantiator);
            instantiatorProperty.managedReferenceValue = instantiator;

            using var idProperty = serializedObject.FindProperty(CoherenceSyncConfig.Property.id);
            idProperty.stringValue = System.Guid.NewGuid().ToString();

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            registry.Register(config);

            registry.CleanUp();
            registry.CleanUp();
            registry.CleanUp();

            Assert.That(instantiator.OnApplicationQuitCallCount, Is.EqualTo(1));

            registry.Deregister(config);
            Object.DestroyImmediate(config);
        }

        private class FakeInstantiator : INetworkObjectInstantiator
        {
            public int OnApplicationQuitCallCount { get; private set; }

            public void OnUniqueObjectReplaced(ICoherenceSync instance) { }
            public ICoherenceSync Instantiate(SpawnInfo spawnInfo) => null;
            public void Destroy(ICoherenceSync obj) { }
            public void WarmUpInstantiator(CoherenceBridge bridge, CoherenceSyncConfig config, INetworkObjectProvider assetLoader) { }
            public void OnApplicationQuit() => OnApplicationQuitCallCount++;
        }
    }
}
