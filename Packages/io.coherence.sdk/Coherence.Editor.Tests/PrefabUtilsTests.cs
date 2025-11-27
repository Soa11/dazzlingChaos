namespace Coherence.Editor.Tests
{
    using System.Linq;
    using Editor;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    public class PrefabUtilsTests : CoherenceTest
    {
        private const string ResourcesPath = "FindAllVariants";

        [TestCase("TestBasePrefab", new[]
         {
             "TestPrefabVariant1",
             "TestPrefabVariant2",
             "TestPrefabVariant1NestedVariant1",
             "TestPrefabVariant1NestedVariant2"
         }),
         TestCase("TestPrefabVariant1", new[]
         {
             "TestPrefabVariant1NestedVariant1",
             "TestPrefabVariant1NestedVariant2"
         }),
         TestCase("TestPrefabVariant2", new string[0]),
         TestCase("NotVariant", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant1", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant2", new string[0])]
        public void FindAllVariants_Finds_All_Variants_Of_Prefab_Root_Component(string basePrefabName, string[] expectedPaths)
        {
            var prefabs = Resources.LoadAll<GameObject>(ResourcesPath);
            var basePrefab = prefabs.Single(x => x.name == basePrefabName);
            var basePrefabPath = AssetDatabase.GetAssetPath(basePrefab);
            var prefabStage = PrefabStageUtility.OpenPrefab(basePrefabPath);
            try
            {
                var prefabStageRoot = prefabStage.prefabContentsRoot.transform;
                var target = prefabStageRoot.GetComponent<Camera>() as Component;
                if (!target)
                {
                    Debug.LogWarning($"Could not find Camera component on root of {basePrefabName}.");
                }

                var variants = PrefabUtils.FindInAllVariants(target, prefabs).ToArray();
                Assert.That(variants.Select(GetPath), Is.EquivalentTo(expectedPaths));
                Assert.That(variants.All(x => x is Camera), Is.True);
            }
            finally
            {
                StageUtility.GoToMainStage();
            }
        }

        [TestCase("TestBasePrefab", new[]
         {
             "TestPrefabVariant1/Child",
             "TestPrefabVariant2/Child",
             "TestPrefabVariant1NestedVariant1/Child",
             "TestPrefabVariant1NestedVariant2/Child"
         }),
         TestCase("TestPrefabVariant1", new[]
         {
             "TestPrefabVariant1NestedVariant1/Child",
             "TestPrefabVariant1NestedVariant2/Child"
         }),
         TestCase("TestPrefabVariant2", new string[0]),
         TestCase("NotVariant", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant1", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant2", new string[0])]
        public void FindAllVariants_Finds_All_Variants_Of_Prefab_Child_Component(string basePrefabName, string[] expectedPaths)
        {
            var prefabs = Resources.LoadAll<GameObject>(ResourcesPath);
            var basePrefab = prefabs.Single(x => x.name == basePrefabName);
            var target = basePrefab.transform.GetChild(0).GetComponent<Camera>() as Component;
            if (!target)
            {
                Debug.LogWarning($"Could not find Camera component on root of {basePrefabName}.");
            }

            var variants = PrefabUtils.FindInAllVariants(target, prefabs).ToArray();
            Assert.That(variants.Select(GetPath), Is.EquivalentTo(expectedPaths));
            Assert.That(variants.All(x => x is Camera), Is.True);
        }


        [TestCase("TestBasePrefab", new[]
         {
             "TestPrefabVariant1",
             "TestPrefabVariant2",
             "TestPrefabVariant1NestedVariant1",
             "TestPrefabVariant1NestedVariant2"
         }),
         TestCase("TestPrefabVariant1", new[]
         {
             "TestPrefabVariant1NestedVariant1",
             "TestPrefabVariant1NestedVariant2"
         }),
         TestCase("TestPrefabVariant2", new string[0]),
         TestCase("NotVariant", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant1", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant2", new string[0])]
        public void FindAllVariants_Finds_All_Variants_Of_PrefabStage_Root_Component(string basePrefabName, string[] expectedPaths)
        {
            var prefabs = Resources.LoadAll<GameObject>(ResourcesPath);
            var basePrefab = prefabs.Single(x => x.name == basePrefabName);
            var target = basePrefab.GetComponent<Camera>() as Component;
            if (!target)
            {
                Debug.LogWarning($"Could not find Camera component on root of {basePrefabName}.");
            }

            var variants = PrefabUtils.FindInAllVariants(target, prefabs).ToArray();
            Assert.That(variants.Select(GetPath), Is.EquivalentTo(expectedPaths));
            Assert.That(variants.All(x => x is Camera), Is.True);
        }

        [TestCase("TestBasePrefab", new[]
         {
             "TestPrefabVariant1/Child",
             "TestPrefabVariant2/Child",
             "TestPrefabVariant1NestedVariant1/Child",
             "TestPrefabVariant1NestedVariant2/Child"
         }),
         TestCase("TestPrefabVariant1", new[]
         {
             "TestPrefabVariant1NestedVariant1/Child",
             "TestPrefabVariant1NestedVariant2/Child"
         }),
         TestCase("TestPrefabVariant2", new string[0]),
         TestCase("NotVariant", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant1", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant2", new string[0])]
        public void FindAllVariants_Finds_All_Variants_Of_PrefabStage_Child_Component(string basePrefabName, string[] expectedPaths)
        {
            var prefabs = Resources.LoadAll<GameObject>(ResourcesPath);
            var basePrefab = prefabs.Single(x => x.name == basePrefabName);
            var basePrefabPath = AssetDatabase.GetAssetPath(basePrefab);
            var prefabStage = PrefabStageUtility.OpenPrefab(basePrefabPath);
            try
            {
                var prefabStageRoot = prefabStage.prefabContentsRoot.transform;
                var target = prefabStageRoot.GetChild(0).GetComponent<Camera>() as Component;
                if (!target)
                {
                    Debug.LogWarning($"Could not find Camera component on root of {basePrefabName}.");
                }

                var variants = PrefabUtils.FindInAllVariants(target, prefabs).ToArray();
                Assert.That(variants.Select(GetPath), Is.EquivalentTo(expectedPaths));
                Assert.That(variants.All(x => x is Camera), Is.True);
            }
            finally
            {
                StageUtility.GoToMainStage();
            }
        }

        private static string GetPath(Component component) => GetPath(component.transform);
        private static string GetPath(Transform transform) => transform.parent ? GetPath(transform.parent) + "/" + transform.name : transform.name;
    }
}
