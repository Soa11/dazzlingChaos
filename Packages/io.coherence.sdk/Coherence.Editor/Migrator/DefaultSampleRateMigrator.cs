// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using Coherence.Toolkit;
    using Common;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Interpolation;
    using UnityEditor;
    using UnityEngine;

    [Preserve]
    internal class DefaultSampleRateMigrator : IDataMigrator
    {
        public SemVersion MaxSupportedVersion => new(3);
        public int Order => -100;
        public string MigrationMessage => "Updated CoherenceSyncs sample rate defaults.";

        public void Initialize()
        {
        }

        public IEnumerable<Object> GetMigrationTargets()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");

            foreach (var guid in guids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab.TryGetComponent(out CoherenceSync sync))
                {
                    yield return sync;
                }
            }
        }

        public bool RequiresMigration(Object obj)
        {
            if (obj is not CoherenceSync sync)
            {
                return false;
            }

            return sync.Bindings.Any(binding => binding.archetypeData.SampleRate <= 0);
        }

        public bool MigrateObject(Object obj)
        {
            if (obj is not CoherenceSync sync)
            {
                return false;
            }

            foreach (var binding in sync.Bindings)
            {
                if (binding.archetypeData.SampleRate <= 0)
                {
                    binding.archetypeData.SetSampleRate(InterpolationSettings.DefaultSampleRate);
                    EditorUtility.SetDirty(sync);
                }
            }

            return true;
        }
    }
}
