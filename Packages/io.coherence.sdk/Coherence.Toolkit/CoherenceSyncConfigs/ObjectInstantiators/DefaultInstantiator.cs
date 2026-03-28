// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Toolkit
{
    using System;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// The default <see cref="CoherenceSyncConfig.Instantiator"/> responsible for
    /// replicating instantiation and destruction of networked objects on
    /// non-authoritative clients.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <see cref="Object.Instantiate(Object, Vector3, Quaternion)"/> to instantiate
    /// objects and <see cref="Object.Destroy(Object)"/> to destroy them.
    /// </para>
    /// <para>
    /// If the object's Uniqueness has been set to No Duplicates, then the object will
    /// be set inactive instead of being destroyed when <see cref="Destroy(ICoherenceSync)"/>
    /// is called. This is to avoid local changes made to its state being lost in cases where
    /// the unique object was only temporarily set inactive on the authoritative client,
    /// and later becomes active again.
    /// </para>
    /// </remarks>
    [DisplayName("Default", "Instances of this prefab will be instantiated and destroyed when they are no longer needed.")]
    [Serializable]
    public sealed class DefaultInstantiator : INetworkObjectInstantiator
    {
        public void OnUniqueObjectReplaced(ICoherenceSync instance) { }
        public void WarmUpInstantiator(CoherenceBridge bridge, CoherenceSyncConfig config, INetworkObjectProvider assetLoader) { }

        public ICoherenceSync Instantiate(SpawnInfo spawnInfo)
            => (ICoherenceSync)Object.Instantiate((Object)spawnInfo.prefab, spawnInfo.position, spawnInfo.rotation ?? Quaternion.identity);

        public void Destroy(ICoherenceSync obj)
        {
            if (obj.IsUnique && !string.IsNullOrEmpty(obj.ManualUniqueId))
            {
                obj.SetActive(false);
                return;
            }

            Object.Destroy(obj.gameObject);
        }

        public void OnApplicationQuit() { }
    }
}
