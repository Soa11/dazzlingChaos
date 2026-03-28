namespace Coherence.Editor
{
    using Coherence.Toolkit;
    using Toolkit;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Handles automatically fixing missing <see cref="CoherenceSync.CoherenceSyncConfig"/>
    /// references on <see cref="CoherenceSync"/> components on prefab instances.
    /// </summary>
    [InitializeOnLoad]
    public sealed class OnPrefabInstanceUpdated
    {
        static OnPrefabInstanceUpdated() => PrefabUtility.prefabInstanceUpdated += PrefabInstanceUpdated;

        private static void PrefabInstanceUpdated(GameObject instance)
        {
            if (!instance.TryGetComponent(out CoherenceSync instanceSync)
                || instanceSync.CoherenceSyncConfig
                || PrefabUtility.GetPropertyModifications(instanceSync) is not { } modifications)
            {
                return;
            }

            foreach (var x in modifications)
            {
                if (x.propertyPath is not CoherenceSync.Property.coherenceSyncConfig)
                {
                    continue;
                }

                var prefab = CoherenceSyncEditor.GetPrefab(instance);
                if (!prefab || !prefab.TryGetComponent<CoherenceSync>(out _))
                {
                    return;
                }

                using var serializedObject = new SerializedObject(instanceSync);
                using var property = serializedObject.FindProperty(CoherenceSync.Property.coherenceSyncConfig);
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                return;
            }
        }
    }
}
