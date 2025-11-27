namespace Coherence.Editor.Tests
{
    using Coherence.Tests;
    using Coherence.Toolkit;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Edit mode unit tests for <see cref="NetworkPrefabProcessor"/>.
    /// </summary>
    public class NetworkPrefabProcessorTests : CoherenceTest
    {
        [Test]
        public void TryUpdatePrefab_Updates_bakedScriptType()
        {
#if COHERENCE_DONT_UPDATE_PREFABS
            Assert.Ignore("Can't run test because COHERENCE_DONT_UPDATE_PREFABS is defined.");
#endif
            var sync = CreateSync();
            var callbackWasInvoked = false;
            NetworkPrefabProcessor.TryUpdatePrefab(sync);
            var serializedObject = new SerializedObject(sync);
            using var bakedScriptTypeProperty = serializedObject.FindProperty(CoherenceSync.Property.bakedScriptType);
            const string oldValue = "x";
            bakedScriptTypeProperty.stringValue = oldValue;
            serializedObject.ApplyModifiedProperties();

            var wasUpdated = NetworkPrefabProcessor.TryUpdatePrefab(sync, () => callbackWasInvoked = true);

            Assert.That(wasUpdated, Is.True);
            Assert.That(callbackWasInvoked, Is.True);
            serializedObject.Update();
            Assert.That(bakedScriptTypeProperty.stringValue, Is.Not.EqualTo(oldValue));
        }

        [Test]
        public void TryUpdatePrefab_Updates_scenePrefabInstanceUUID()
        {
#if COHERENCE_DONT_UPDATE_PREFABS
            Assert.Ignore("Can't run test because COHERENCE_DONT_UPDATE_PREFABS is defined.");
#endif
            var sync = CreateSync();
            var callbackWasInvoked = false;
            NetworkPrefabProcessor.TryUpdatePrefab(sync);
            var serializedObject = new SerializedObject(sync);
            using var scenePrefabInstanceUUIDProperty = serializedObject.FindProperty(CoherenceSync.Property.scenePrefabInstanceUUID);
            const string oldValue = "x";
            scenePrefabInstanceUUIDProperty.stringValue = oldValue;
            serializedObject.ApplyModifiedProperties();

            var wasUpdated = NetworkPrefabProcessor.TryUpdatePrefab(sync, () => callbackWasInvoked = true);

            Assert.That(wasUpdated, Is.True);
            Assert.That(callbackWasInvoked, Is.True);
            serializedObject.Update();
            Assert.That(scenePrefabInstanceUUIDProperty.stringValue, Is.Not.EqualTo(oldValue));
        }

        [Test]
        public void TryUpdatePrefab_Does_Nothing_On_Repeat_Executions()
        {
            #if COHERENCE_DONT_UPDATE_PREFABS
            Assert.Ignore("Can't run test because COHERENCE_DONT_UPDATE_PREFABS is defined.");
            #endif

            var sync = CreateSync();
            var wasCalled = false;

            NetworkPrefabProcessor.TryUpdatePrefab(sync);
            NetworkPrefabProcessor.TryUpdatePrefab(sync, () => wasCalled = true);
            NetworkPrefabProcessor.TryUpdatePrefab(sync, () => wasCalled = true);

            Assert.That(wasCalled, Is.False);
        }

        private static CoherenceSync CreateSync() => new GameObject(nameof(CoherenceSync)).AddComponent<CoherenceSync>();
    }
}
