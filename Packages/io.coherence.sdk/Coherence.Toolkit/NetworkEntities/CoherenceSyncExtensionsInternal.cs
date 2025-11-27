// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    /// <summary>
    /// Extensions for <see cref="ICoherenceSync"/> that are intended for internal use only.
    /// </summary>
    internal static class CoherenceSyncExtensionsInternal
    {
        /// <summary>
        /// Unparents all networked objects currently parented to this networked object.
        /// </summary>
        public static void UnparentChildren(this ICoherenceSync sync)
        {
            var gameObject = sync.gameObject;
            foreach (var child in gameObject.GetComponentsInChildren<CoherenceSync>(!gameObject.activeInHierarchy))
            {
                if (!ReferenceEquals(child, sync))
                {
                    child.SetParent(null);
                }
            }
        }

        /// <summary>
        /// Sets the networked entity inactive.
        /// </summary>
        /// <remarks>
        /// This is the same as setting the entity inactive using
        /// <see cref="UnityEngine.GameObject.SetActive(bool)"/>, except that it handles
        /// unparenting child networked objects before setting the GameObject inactive if
        /// <see cref="ICoherenceSync.PreserveChildren"/> is enabled.
        /// This helps avoid encountering the error 'Cannot set the parent of the GameObject 'X'
        /// while activating or deactivating the parent GameObject 'Y'.
        /// </remarks>
        public static void SetActive(this ICoherenceSync sync, bool value)
        {
            if (!value && sync.PreserveChildren && sync.gameObject.activeInHierarchy)
            {
                sync.UnparentChildren();
            }

            sync.gameObject.SetActive(value);
        }
    }
}
