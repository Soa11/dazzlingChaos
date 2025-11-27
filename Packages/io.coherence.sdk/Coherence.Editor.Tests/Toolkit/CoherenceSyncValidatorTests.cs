// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Coherence.Tests;
    using Coherence.Toolkit;
    using Coherence.Toolkit.Archetypes;
    using Coherence.Toolkit.Bindings;
    using Coherence.Toolkit.Bindings.ValueBindings;
    using Editor.Toolkit;
    using NUnit.Framework;
    using UnityEngine;

    /// <summary>
    /// Edit mode unit tests for <see cref="Coherence.Editor.Toolkit.CoherenceSyncValidator"/>.
    /// </summary>
    public class CoherenceSyncValidatorTests : CoherenceTest
    {
        private CoherenceSync sync;
        private Component component;

        [Test]
        public void Validate_Does_Not_Return_TooManySyncedVariables_When_Synced_Variable_Count_Is_At_Limit()
        {
            CreateTestComponent(ArchetypeComponentValidator.MaxSyncedVariablesPerComponent);
            var foundIssues = new List<CoherenceSyncValidator.Issue>();

            CoherenceSyncValidator.Validate(sync, new(sync.gameObject), foundIssues);

            var tooManySyncedVariablesIssue = foundIssues.FirstOrDefault(x => x.Type is CoherenceSyncValidator.IssueType.TooManySyncedVariables);
            Assert.That(tooManySyncedVariablesIssue.Type, Is.EqualTo(default(CoherenceSyncValidator.IssueType)));
        }

        [Test]
        public void Validate_Returns_TooManySyncedVariables_When_Synced_Variable_Count_Is_Above_Limit()
        {
            CreateTestComponent(ArchetypeComponentValidator.MaxSyncedVariablesPerComponent + 1);
            var foundIssues = new List<CoherenceSyncValidator.Issue>();

            var result = CoherenceSyncValidator.Validate(sync, new(sync.gameObject), foundIssues);

            Assert.That(result, Is.False);
            var tooManySyncedVariablesIssue = foundIssues.FirstOrDefault(x => x.Type is CoherenceSyncValidator.IssueType.TooManySyncedVariables);
            Assert.That(tooManySyncedVariablesIssue.Type, Is.EqualTo(CoherenceSyncValidator.IssueType.TooManySyncedVariables));
            Assert.That(tooManySyncedVariablesIssue.Component, Is.EqualTo(component));
            Assert.That(tooManySyncedVariablesIssue.SyncedVariableCount, Is.EqualTo(ArchetypeComponentValidator.MaxSyncedVariablesPerComponent + 1));
        }

        [Test]
        public void Validate_Returns_NotConnectedToPrefab_For_Scene_Object()
        {
            CreateTestComponent(0);
            var foundIssues = new List<CoherenceSyncValidator.Issue>();

            var result = CoherenceSyncValidator.Validate(sync, new(sync.gameObject), foundIssues);

            Assert.That(result, Is.False);
            var notConnectedToPrefabIssue = foundIssues.FirstOrDefault(x => x.Type is CoherenceSyncValidator.IssueType.NotConnectedToPrefab);
            Assert.That(notConnectedToPrefabIssue.Type, Is.EqualTo(CoherenceSyncValidator.IssueType.NotConnectedToPrefab));
        }

        private void CreateTestComponent(int syncedVariableCount)
        {
            var gameObject = new GameObject(nameof(CoherenceSyncValidatorTests));
            sync = gameObject.AddComponent<CoherenceSync>();
            component = gameObject.AddComponent<TestComponent>();
            var fields = typeof(TestComponent).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var boundComponent = new ArchetypeComponent(component, maxLods:1);
            for (var i = 0; i < syncedVariableCount; i++)
            {
                var field = fields[i];
                var descriptor = new Descriptor(typeof(TestComponent), field);
                var binding = new BoolBinding(descriptor, component);
                binding.BindingArchetypeData = new(SchemaType.Bool, typeof(bool));
                boundComponent.AddBinding(binding, SchemaType.Bool);
            }

            sync.Archetype.BoundComponents.Add(boundComponent);
            sync.Bindings.AddRange(boundComponent.Bindings);
        }
    }

    public class TestComponent : MonoBehaviour
    {
        public bool field1;
        public bool field2;
        public bool field3;
        public bool field4;
        public bool field5;
        public bool field6;
        public bool field7;
        public bool field8;
        public bool field9;
        public bool field10;
        public bool field11;
        public bool field12;
        public bool field13;
        public bool field14;
        public bool field15;
        public bool field16;
        public bool field17;
        public bool field18;
        public bool field19;
        public bool field20;
        public bool field21;
        public bool field22;
        public bool field23;
        public bool field24;
        public bool field25;
        public bool field26;
        public bool field27;
        public bool field28;
        public bool field29;
        public bool field30;
        public bool field31;
        public bool field32;
        public bool field33;
    }
}
