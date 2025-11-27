// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using Mode = UnityEditor.SceneManagement.PrefabStage.Mode;

    /// <summary>
    /// Utility methods related to prefabs.
    /// </summary>
    internal static class PrefabUtils
    {
        /// <summary>
        /// Opens the <see paramref="gameObject"/> in the Prefab Stage in isolation.
        /// </summary>
        /// <param name="gameObject"> Game object that is part of a prefab instance or an open prefab stage. </param>
        /// <param name="gameObjectStatus"> Status of the <see paramref="gameObject"/>. </param>
        /// <param name="select"> Also select the prefab asset? </param>
        public static void OpenInIsolation(GameObject gameObject, GameObjectStatus gameObjectStatus, bool select)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            string assetPath;
            if (!gameObject.scene.IsValid())
            {
                assetPath = AssetDatabase.GetAssetPath(gameObject);
            }
            else
            {
                assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);

                if (string.IsNullOrEmpty(assetPath) && gameObjectStatus.IsInPrefabStage)
                {
                    assetPath = prefabStage.assetPath;
                }
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));

            if (!prefabStage || prefabStage.assetPath != assetPath || prefabStage.mode is not Mode.InIsolation)
            {
                PrefabStageUtility.OpenPrefab(assetPath, null, Mode.InIsolation);
            }

            if (select)
            {
                prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                Selection.activeGameObject = prefabStage.prefabContentsRoot;
            }

            if (Event.current != null)
            {
                GUIUtility.ExitGUI();
            }
        }

        /// <summary>
        /// Determines if the component is in an instance of a prefab.
        /// </summary>
        /// <param name="component"><see cref="Component"/> to test.</param>
        /// <returns><see langword="true"/> if the component is part of a prefab instance, <see langword="false" /> otherwise.</returns>
        internal static bool IsInstance(Component component)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var inStage = stage && component && stage.prefabContentsRoot == component.gameObject;
            return PrefabUtility.IsPartOfPrefabInstance(component) && !inStage;
        }

        /// <summary>
        /// Given a component that is part of a prefab or open in the Prefab Stage,
        /// finds the corresponding component in all prefab variants of the prefab containing the component.
        /// </summary>
        internal static IEnumerable<T> FindInAllVariants<T>(T targetComponent, IEnumerable<GameObject> prefabs) where T : Component
        {
            GameObject targetPrefab;
            Component correspondingComponentInPrefab;
            if (!PrefabUtility.IsPartOfPrefabAsset(targetComponent))
            {
                if (PrefabStageUtility.GetPrefabStage(targetComponent.gameObject) is not { } prefabStage)
                {
                    yield break;
                }

                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabStage.assetPath);
                if (!targetPrefab)
                {
                    yield break;
                }

                correspondingComponentInPrefab = FindCorrespondingComponent(targetComponent, targetPrefab);
            }
            else
            {
                targetPrefab = targetComponent.transform.root.gameObject;
                correspondingComponentInPrefab = targetComponent;
            }

            var componentType = targetComponent.GetType();
            foreach (var potentialVariant in prefabs)
            {
                if (!potentialVariant)
                {
                    continue;
                }

                // Check if prefab is a variant or a nested variant of the target
                for (var basePrefab = GetBasePrefab(potentialVariant); basePrefab; basePrefab = GetBasePrefab(basePrefab))
                {
                    if (!ReferenceEquals(basePrefab, targetPrefab))
                    {
                        continue;
                    }

                    var componentsOfMatchingTypeInVariant = potentialVariant.GetComponentsInChildren(componentType);
                    var targetInVariant = componentsOfMatchingTypeInVariant
                        .FirstOrDefault(x =>
                        {
                            for (var correspondingComponent = PrefabUtility.GetCorrespondingObjectFromSource(x); correspondingComponent; correspondingComponent = PrefabUtility.GetCorrespondingObjectFromSource(correspondingComponent))
                            {
                                if (ReferenceEquals(correspondingComponent, correspondingComponentInPrefab))
                                {
                                    return true;
                                }
                            }

                            return false;
                        });

                    if (targetInVariant)
                    {
                        yield return (T)targetInVariant;
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Attempts to find the component in the prefab that corresponds with the given
        /// <paramref name="component"/> in an instance of the <paramref name="prefab"/>,
        /// or in a GameObject open in the Prefab Stage targeting the <paramref name="prefab"/>.
        /// </summary>
        [return: MaybeNull]
        private static Component FindCorrespondingComponent(Component component, GameObject prefab)
        {
            var componentType = component.GetType();
            int targetIndex;

            if (!component.transform.parent)
            {
                var componentsOfTypeInInstance = component.GetComponents(componentType).Where(c => c?.GetType() == componentType).ToArray();
                var componentsOfTypeInPrefab = prefab.GetComponents(componentType).Where(c => c?.GetType() == componentType).ToArray();
                targetIndex = Array.IndexOf(componentsOfTypeInInstance, component);
                if (targetIndex is -1 || targetIndex >= componentsOfTypeInPrefab.Length)
                {
                    return null;
                }

                return componentsOfTypeInPrefab[targetIndex];
            }

            var childPath = GetRelativePath(component.transform);
            var matchingTransformInPrefab = childPath.Length is 0 ? prefab.transform : prefab.transform.Find(childPath);
            if (matchingTransformInPrefab)
            {
                var componentsOfTypeInInstance = component.GetComponents(componentType).Where(c => c?.GetType() == componentType).ToArray();
                var componentsOfTypeInPrefab = matchingTransformInPrefab.GetComponents(componentType).Where(c => c?.GetType() == componentType).ToArray();
                targetIndex = Array.IndexOf(componentsOfTypeInInstance, component);
                if (targetIndex is -1 || targetIndex >= componentsOfTypeInPrefab.Length)
                {
                    return null;
                }

                return componentsOfTypeInPrefab[targetIndex];
            }

            var componentsOfTypeInInstanceRootChildren = component.transform.root.GetComponentsInChildren(componentType, includeInactive: true).Where(c => c?.GetType() == componentType).ToArray();
            var componentsOfTypeInPrefabChildren = prefab.GetComponentsInChildren(componentType, includeInactive: true).Where(c => c?.GetType() == componentType).ToArray();
            targetIndex = Array.IndexOf(componentsOfTypeInInstanceRootChildren, component);
            if (targetIndex is -1 || targetIndex >= componentsOfTypeInPrefabChildren.Length)
            {
                return null;
            }

            return componentsOfTypeInPrefabChildren[targetIndex];
        }

        /// <summary>
        /// Gets the path of the <see cref="Transform"/> relative to its root.
        /// </summary>
        private static string GetRelativePath(Transform transform)
        {
            var parent = transform.parent;
            if (!parent)
            {
                return "";
            }

            var grandParent = parent.parent;
            if (!grandParent)
            {
                return transform.name;
            }

            var sb = new StringBuilder();
            sb.Append(transform);
            sb.Insert(0, '/');
            sb.Insert(0, parent.name);
            while(grandParent.parent)
            {
                sb.Insert(0, '/');
                sb.Insert(0, grandParent.name);
                grandParent = grandParent.parent;
            }

            return sb.ToString();
        }

        internal static T FindBasePrefab<T>(T target) where T : Component => PrefabUtility.IsPartOfPrefabAsset(target) switch
        {
            true => PrefabUtility.GetPrefabAssetType(target) is PrefabAssetType.Variant ? PrefabUtility.GetCorrespondingObjectFromSource(target) : null,
            false => PrefabStageUtility.GetPrefabStage(target.gameObject) is { } prefabStage
                     && prefabStage
                     && AssetDatabase.LoadAssetAtPath<GameObject>(prefabStage.assetPath) is { } prefab
                     && prefab
                ? (T)FindCorrespondingComponent(target, prefab)
                : null
        };

        [return: MaybeNull]
        private static GameObject GetBasePrefab(GameObject prefab)
            => PrefabUtility.GetPrefabAssetType(prefab) is PrefabAssetType.Variant ? PrefabUtility.GetCorrespondingObjectFromSource(prefab) : null;
    }
}
