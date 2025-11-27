using Coherence.Tests;
using Coherence.Toolkit;
using NUnit.Framework;
using UnityEngine;

namespace Coherence.Editor.Tests
{
    using Coherence.Toolkit.Bindings;
    using Coherence.Toolkit.Bindings.ValueBindings;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceSyncConfigUtils.GetBindingInfo"/>.
    /// </summary>
    public class CoherenceSyncConfigUtilsTests : CoherenceTest
    {
        private const int ExpectedVariableBindingsPerGameObject = 1;
        private const int ExpectedMethodBindingsPerGameObject = 1;

        private CoherenceSync sync;
        private GameObject root;
        private GameObject childWithSync;
        private GameObject childWithoutSync;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // Build hierarchy entirely in code (no Resources)
            root = new("Root");
            sync = root.AddComponent<CoherenceSync>();

            childWithSync = new("ChildWithSync");
            childWithSync.transform.SetParent(root.transform, worldPositionStays: false);
            childWithSync.AddComponent<CoherenceSync>();

            childWithoutSync = new("ChildWithoutSync");
            childWithoutSync.transform.SetParent(root.transform, worldPositionStays: false);

            AddTestBindings(sync, root);
            AddTestBindings(sync, childWithoutSync);
            AddTestBindings(childWithSync.GetComponent<CoherenceSync>(), childWithSync);
        }

        [TearDown]
        public override void TearDown()
        {
            if (sync != null)
            {
                Object.DestroyImmediate(sync.gameObject);
            }
            base.TearDown();
        }

        [Test]
        public void GetBindingInfo_WithNullContext_Includes_Bindings_From_Root_And_ChildWithoutSync()
        {
            var info = CoherenceSyncConfigUtils.GetBindingInfo(sync).Value;
            Assert.That(info.Variables, Is.EqualTo(ExpectedVariableBindingsPerGameObject * 2));
            Assert.That(info.Methods, Is.EqualTo(ExpectedMethodBindingsPerGameObject * 2));
        }

        [Test]
        public void GetBindingInfo_WithRootContext_Includes_Bindingsd_Only_From_Root()
        {
            var info = CoherenceSyncConfigUtils.GetBindingInfo(sync, root).Value;
            Assert.That(info.Variables, Is.EqualTo(ExpectedVariableBindingsPerGameObject));
            Assert.That(info.Methods, Is.EqualTo(ExpectedMethodBindingsPerGameObject));
        }

        [Test]
        public void GetBindingInfo_WithChildWithoutSync_Context_Includes_Bindings_Only_From_ChildWithoutSync()
        {
            var info = CoherenceSyncConfigUtils.GetBindingInfo(sync, childWithoutSync).Value;
            Assert.That(info.Variables, Is.EqualTo(ExpectedVariableBindingsPerGameObject));
            Assert.That(info.Methods, Is.EqualTo(ExpectedMethodBindingsPerGameObject));
        }

        [Test]
        public void GetBindingInfo_WithChildWithSync_Context_Includes_No_Bindings()
        {
            var info = CoherenceSyncConfigUtils.GetBindingInfo(sync, childWithSync).Value;
            Assert.That(info.Variables, Is.EqualTo(0));
            Assert.That(info.Methods, Is.EqualTo(0));
        }

        [Test]
        public void GetBindingInfo_With_Null_Context_Includes_Invalid_Bindings_From_Root_And_ChildWithoutSync()
        {
            InvalidateAllBindings();
            var info = CoherenceSyncConfigUtils.GetBindingInfo(sync).Value;
            Assert.That(info.InvalidBindings, Is.EqualTo((ExpectedVariableBindingsPerGameObject + ExpectedMethodBindingsPerGameObject) * 2));
        }

        [Test]
        public void GetBindingInfo_With_Root_Context_Includes_Invalid_Bindings_Only_From_Root()
        {
            InvalidateAllBindings();
            var info = CoherenceSyncConfigUtils.GetBindingInfo(sync, root).Value;
            Assert.That(info.InvalidBindings, Is.EqualTo(ExpectedVariableBindingsPerGameObject + ExpectedMethodBindingsPerGameObject));
        }

        [Test]
        public void GetBindingInfo_With_ChildWithoutSync_Context_Includes_Invalid_Bindings_Only_From_ChildWithoutSync()
        {
            InvalidateAllBindings();
            var info = CoherenceSyncConfigUtils.GetBindingInfo(sync, childWithoutSync).Value;
            Assert.That(info.InvalidBindings, Is.EqualTo(ExpectedVariableBindingsPerGameObject + ExpectedMethodBindingsPerGameObject));
        }

        [Test]
        public void GetBindingInfo_With_ChildWithSync_Context_Includes_No_Invalid_Bindings()
        {
            InvalidateAllBindings();
            var info = CoherenceSyncConfigUtils.GetBindingInfo(sync, childWithSync).Value;
            Assert.That(info.InvalidBindings, Is.EqualTo(0));
        }

        private void InvalidateAllBindings()
        {
            // Matches the original test logic: mark all bindings invalid by nulling their descriptor.
            foreach (var binding in sync.Bindings)
            {
                binding.Descriptor = null;
            }
        }

        private static void AddTestBindings(CoherenceSync sync, GameObject context)
        {
            var bindings = sync.Bindings;
            bindings.Add(new Vector3Binding(new(typeof(Vector3), typeof(Transform).GetProperty(nameof(Transform.position))), context.transform));
            bindings.Add(new CommandBinding(new(typeof(Vector3), typeof(Transform).GetMethod(nameof(Transform.SetPositionAndRotation))), context.transform));
        }
    }
}
