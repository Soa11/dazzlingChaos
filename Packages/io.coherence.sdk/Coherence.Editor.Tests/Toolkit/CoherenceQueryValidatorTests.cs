// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Coherence.Tests;
    using Coherence.Toolkit;
    using Editor.Toolkit;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceQueryValidator"/>.
    /// </summary>
    public class CoherenceQueryValidatorTests : CoherenceTest
    {
        private CoherenceQuery query;
        private CoherenceBridge bridge;

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Validate_Returns_True_When_Bridge_Exists_And_Has_Baked_Data(Type queryType)
        {
            CreateQuery(queryType);
            CreateBridge();
            var foundIssues = new List<CoherenceQueryValidator.Issue>();

            var result = CoherenceQueryValidator.Validate(query, new(query.gameObject), foundIssues);

            Assert.That(result, Is.True);
            Assert.That(foundIssues.Count, Is.EqualTo(0));
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Validate_Returns_BridgeNotFound_When_Bridge_Does_Not_Exist(Type queryType)
        {
            CreateQuery(queryType);
            var foundIssues = new List<CoherenceQueryValidator.Issue>();

            var result = CoherenceQueryValidator.Validate(query, new(query.gameObject), foundIssues);

            Assert.That(result, Is.False);
            var bridgeNotFoundIssue = foundIssues.FirstOrDefault(x => x.Type is CoherenceQueryValidator.IssueType.BridgeNotFound);
            Assert.That(bridgeNotFoundIssue.Type, Is.EqualTo(CoherenceQueryValidator.IssueType.BridgeNotFound));
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Validate_Returns_BridgeDisabledOrInactive_When_Bridge_Is_Disabled(Type queryType)
        {
            CreateQuery(queryType);
            CreateBridge();
            bridge.enabled = false;
            var foundIssues = new List<CoherenceQueryValidator.Issue>();

            var result = CoherenceQueryValidator.Validate(query, new(query.gameObject), foundIssues);

            Assert.That(result, Is.False);
            var bridgeDisabledIssue = foundIssues.FirstOrDefault(x => x.Type is CoherenceQueryValidator.IssueType.BridgeDisabledOrInactive);
            Assert.That(bridgeDisabledIssue.Type, Is.EqualTo(CoherenceQueryValidator.IssueType.BridgeDisabledOrInactive));
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Validate_Returns_BridgeDisabledOrInactive_When_Bridge_GameObject_Is_Inactive(Type queryType)
        {
            CreateQuery(queryType);
            CreateBridge();
            bridge.gameObject.SetActive(false);
            var foundIssues = new List<CoherenceQueryValidator.Issue>();

            var result = CoherenceQueryValidator.Validate(query, new(query.gameObject), foundIssues);

            Assert.That(result, Is.False);
            var bridgeDisabledIssue = foundIssues.FirstOrDefault(x => x.Type is CoherenceQueryValidator.IssueType.BridgeDisabledOrInactive);
            Assert.That(bridgeDisabledIssue.Type, Is.EqualTo(CoherenceQueryValidator.IssueType.BridgeDisabledOrInactive));
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Validate_Returns_BakedDataNotFound_When_Bridge_Has_No_Baked_Data(Type queryType)
        {
            CreateQuery(queryType);
            CreateBridge();
            bridge.HasBakedData = false;
            var foundIssues = new List<CoherenceQueryValidator.Issue>();

            var result = CoherenceQueryValidator.Validate(query, new(query.gameObject), foundIssues);

            Assert.That(result, Is.False);
            var bakedDataNotFoundIssue = foundIssues.FirstOrDefault(x => x.Type is CoherenceQueryValidator.IssueType.BakedDataNotFound);
            Assert.That(bakedDataNotFoundIssue.Type, Is.EqualTo(CoherenceQueryValidator.IssueType.BakedDataNotFound));
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void HasIssue_Returns_True_When_Issue_Matches_Predicate(Type queryType)
        {
            CreateQuery(queryType);
            var foundIssues = new List<CoherenceQueryValidator.Issue>();
            CoherenceQueryValidator.Validate(query, new(query.gameObject), foundIssues);

            var hasIssue = CoherenceQueryValidator.HasIssue(query, new(query.gameObject), issue => issue.Type == CoherenceQueryValidator.IssueType.BridgeNotFound);

            Assert.That(hasIssue, Is.True);
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void HasIssue_Returns_False_When_Issue_Does_Not_Match_Predicate(Type queryType)
        {
            CreateQuery(queryType);
            CreateBridge();

            var hasIssue = CoherenceQueryValidator.HasIssue(query, new(query.gameObject), issue => issue.Type == CoherenceQueryValidator.IssueType.BridgeNotFound);

            Assert.That(hasIssue, Is.False);
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void HasAnyIssues_Returns_True_When_Issues_Exist(Type queryType)
        {
            CreateQuery(queryType);
            var hasAnyIssues = CoherenceQueryValidator.HasAnyIssues(query);

            Assert.That(hasAnyIssues, Is.True);
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void HasAnyIssues_Returns_False_When_No_Issues_Exist(Type queryType)
        {
            CreateQuery(queryType);
            CreateBridge();

            var hasAnyIssues = CoherenceQueryValidator.HasAnyIssues(query);

            Assert.That(hasAnyIssues, Is.False);
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Issue_ToString_Returns_Correct_Message_For_BridgeNotFound(Type queryType)
        {
            CreateQuery(queryType);
            var issue = CoherenceQueryValidator.Issue.BridgeNotFound;

            var message = issue.ToString();

            Assert.That(message, Is.EqualTo("CoherenceBridge required in the scene."));
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Issue_ToString_Returns_Correct_Message_For_BridgeDisabledOrInactive(Type queryType)
        {
            CreateQuery(queryType);
            var issue = CoherenceQueryValidator.Issue.BridgeDisabledOrInactive;

            var message = issue.ToString();

            Assert.That(message, Is.EqualTo("An active CoherenceBridge is required in the scene."));
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Issue_ToString_Returns_Correct_Message_For_BakedDataNotFound_In_PlayMode(Type queryType)
        {
            CreateQuery(queryType);
            var issue = CoherenceQueryValidator.Issue.BakedDataNotFound(isPlaying: true);

            var message = issue.ToString();

            Assert.That(message, Is.EqualTo("Network code not found. Exit Play Mode, and bake via coherence > Bake."));
        }

        [TestCase(typeof(CoherenceLiveQuery)), TestCase(typeof(CoherenceGlobalQuery))]
        public void Issue_ToString_Returns_Correct_Message_For_BakedDataNotFound_In_EditMode(Type queryType)
        {
            CreateQuery(queryType);
            var issue = CoherenceQueryValidator.Issue.BakedDataNotFound(isPlaying: false);

            var message = issue.ToString();

            Assert.That(message, Is.EqualTo("Network code not found. Bake via coherence > Bake."));
        }

        [TearDown]
        public override void TearDown()
        {
            if (query)
            {
                Object.DestroyImmediate(query.gameObject);
            }

            if (bridge)
            {
                Object.DestroyImmediate(bridge.gameObject);
            }

            base.TearDown();
        }

        private void CreateQuery(Type queryType) => query = (CoherenceQuery)new GameObject(queryType.Name).AddComponent(queryType);

        private void CreateBridge()
        {
            bridge = new GameObject("Bridge").AddComponent<CoherenceBridge>();
            bridge.HasBakedData = true;
        }
    }
}
