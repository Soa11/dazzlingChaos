// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Archetypes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Bindings;
    using UnityEngine;

    [Serializable]
    public sealed class ArchetypeComponent : IEquatable<ArchetypeComponent>
    {
        public string DisplayName => GetComponentDisplayName();
        public string ComponentFullName => GetComponentFullName();
        internal bool ExpandedInEditor { get; set; } = true;

        public Component Component => component;
        public int LodStepsActive => lodStepsActive;
        public int MaxLods => maxLods;

        // These bindings are not persistent and only used by the UI to access data, do not edit these.
        public List<Binding> Bindings => bindings;

        [NonSerialized] internal List<Binding> bindings = new();
        [SerializeField] private Component component;
        [SerializeField] private int lodStepsActive;

        private int maxLods;

        // Default constructor has to be defined,
        // otherwise field initializers are skipped.
        private ArchetypeComponent()
        {
        }

        public ArchetypeComponent(Component component, int maxLods)
        {
            this.component = component;
            this.maxLods = maxLods;
            lodStepsActive = maxLods;
        }

        // Adding a new LOD step
        public void AddLODStep(int step, bool fromEditor = false, Action onBeforeSerializedModifications = null)
        {
            foreach (var binding in bindings)
            {
                // bindings are not serialized, so no need to invoke onBeforeSerializedModifications
                binding.archetypeData.AddLODStep(step);
            }

            if (fromEditor && lodStepsActive == MaxLods)
            {
                var setLodStepsActive = Mathf.Max(1, lodStepsActive + 1);
                if (lodStepsActive != setLodStepsActive)
                {
                    onBeforeSerializedModifications?.Invoke();
                    lodStepsActive = setLodStepsActive;
                }
            }

            // maxLods is not serialized, so no need to invoke onBeforeSerializedModifications
            maxLods = Mathf.Max(step+1, maxLods);
        }

        // Removing a LOD step
        public void RemoveLODStep(int step)
        {
            maxLods--;
            foreach (Binding binding in bindings)
            {
                binding.archetypeData.RemoveLODLevel(step, maxLods);
            }

            if (lodStepsActive > step)
            {
                lodStepsActive = Mathf.Max(lodStepsActive - 1, 1);
            }
        }

        internal void ClearBindings(CoherenceSync sync)
        {
            bindings.Clear();
        }

        internal bool HasSyncedBindings()
        {
            return GetTotalActiveBindings() > 0;
        }

        internal bool ShouldBeIncludedInArchetype()
        {
            if (HasSyncedBindings())
            {
                return bindings.Any(b => !b.IsMethod);
            }

            return false;
        }

        // Get Total of active Bits.
        public int GetTotalBitsOfLOD(int lodStep)
        {
            if (lodStep >= LodStepsActive)
            {
                return 0;
            }

            return bindings.Sum(binding => binding.archetypeData.GetTotalBitsOfLOD(lodStep));
        }

        public int GetTotalActiveBindings()
        {
            return bindings.Count;
        }

        public int GetTotalActiveMethodBindings()
        {
            return bindings.Count(x => x.IsMethod);
        }

        public int GetTotalActiveValueBindings()
        {
            return bindings.Count(x => !x.IsMethod);
        }

        internal void UpdateLODCountToArchetype(int archetypeLODs, Action onBeforeSerializedModifications = null)
        {
            maxLods = archetypeLODs;

            if (lodStepsActive < 1)
            {
                onBeforeSerializedModifications?.Invoke();
                lodStepsActive = 1;
            }

            AddLODStep(archetypeLODs, onBeforeSerializedModifications: onBeforeSerializedModifications);
        }

        public bool AddBinding(Binding binding, SchemaType type)
        {
            bool changed = false;

            if (!bindings.Contains(binding))
            {
                bindings.Add(binding);
            }

            changed |= binding.archetypeData.Update(type, binding.MonoAssemblyRuntimeType, maxLods);

            _ = HasSyncedBindings();

            return changed;
        }

        public void RemoveBinding(Binding binding)
        {
            if (bindings.Contains(binding))
            {
                _ = bindings.Remove(binding);
            }
            _ = HasSyncedBindings();
        }

        internal void SetLodActive(bool isActive, int lodStep)
        {
            lodStepsActive = Mathf.Max(1, isActive ? lodStep + 1 : lodStep);
        }

        // Caching these to minimize  string operations
        private string displayName;
        private string componentFullname;

        private string GetComponentFullName()
        {
            if (component == null)
            {
                Debug.LogWarning("Trying to get FullName of a BoundComponent that is missing a reference");
                return "None";
            }
            if (string.IsNullOrEmpty(displayName))
            {
                Type type = component.GetType();
                componentFullname = TypeUtils.TidyAssemblyTypeName(type.AssemblyQualifiedName);
            }
            return componentFullname;
        }

        private string GetComponentDisplayName()
        {
            if (component == null)
            {
                Debug.LogWarning("Trying to get DisplayName of a boundcomponent that is missing a reference");
                return "None";
            }
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = ComponentFullName.Split(",".ToCharArray())[0];
            }
            return displayName;
        }

        internal List<Binding> GetAllBindingsOnSync(CoherenceSync sync)
        {
            List<Binding> bindings = new List<Binding>();

            foreach (Binding binding in this.bindings)
            {

                var bind = sync.Bindings.FirstOrDefault(b =>
                b != null && b.Name == binding.Name && b.unityComponent == component);

                if (bind != null)
                {
                    bindings.Add(bind);
                }
            }

            return bindings;
        }

        public static bool operator ==(ArchetypeComponent x, ArchetypeComponent y) => Equals(x, y);
        public static bool operator !=(ArchetypeComponent x, ArchetypeComponent y) => !Equals(x, y);

        public bool Equals(ArchetypeComponent other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ReferenceEquals(component, other.component) && lodStepsActive == other.lodStepsActive && maxLods == other.maxLods;
        }

        public override bool Equals(object obj) => ReferenceEquals(this, obj) || obj is ArchetypeComponent other && Equals(other);
        public override int GetHashCode() => component?.GetHashCode() ?? 0;

#if UNITY_EDITOR
        /// <summary>
        /// Contains names of serialized properties found in <see cref="ArchetypeComponent"/>.
        /// Can be used in the editor with SerializedObject.FindProperty etc.
        /// </summary>
        internal static class Property
        {
            public const string lodStepsActive = nameof(ArchetypeComponent.lodStepsActive);
        }
#endif
    }
}
