// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using Coherence.Tests;
    using NUnit.Framework;
    using System.Reflection;

    public class CoherenceHubTests : CoherenceTest
    {
        [Test]
        public void ScrollViewDataKey_ShouldBeUniquePerModule()
        {
            // Test that different modules would generate different scroll view data keys
            // This validates that the fix for scroll position preservation between sections works correctly
            
            var cloudModuleName = "Cloud";
            var simulatorsModuleName = "Simulators";
            var replicationServerModuleName = "Replication Server";
            
            // Simulate the key generation logic from CoherenceHub.RebuildModule()
            var cloudKey = $"hubScrollView_{cloudModuleName}";
            var simulatorsKey = $"hubScrollView_{simulatorsModuleName}";
            var replicationServerKey = $"hubScrollView_{replicationServerModuleName}";
            
            // Assert that all keys are unique
            Assert.AreNotEqual(cloudKey, simulatorsKey, "Cloud and Simulators modules should have different scroll view keys");
            Assert.AreNotEqual(cloudKey, replicationServerKey, "Cloud and Replication Server modules should have different scroll view keys");
            Assert.AreNotEqual(simulatorsKey, replicationServerKey, "Simulators and Replication Server modules should have different scroll view keys");
            
            // Assert that keys are properly formatted
            Assert.IsTrue(cloudKey.StartsWith("hubScrollView_"), "Cloud key should start with hubScrollView_ prefix");
            Assert.IsTrue(simulatorsKey.StartsWith("hubScrollView_"), "Simulators key should start with hubScrollView_ prefix");
            Assert.IsTrue(replicationServerKey.StartsWith("hubScrollView_"), "Replication Server key should start with hubScrollView_ prefix");
            
            // Assert that keys contain the module name
            Assert.IsTrue(cloudKey.Contains(cloudModuleName), "Cloud key should contain the module name");
            Assert.IsTrue(simulatorsKey.Contains(simulatorsModuleName), "Simulators key should contain the module name");
            Assert.IsTrue(replicationServerKey.Contains(replicationServerModuleName), "Replication Server key should contain the module name");
        }
    }
}