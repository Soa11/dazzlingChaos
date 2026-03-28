// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using Transport;
    using Common;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [Preserve]
    internal class RuntimeSettingsTransportMigrator : IDataMigrator
    {
        public SemVersion MaxSupportedVersion => new(3);
        public int Order => 2;
        public string MigrationMessage => "Updated RuntimeSettings with new transport type settings.";

        public void Initialize()
        {
        }

        public IEnumerable<Object> GetMigrationTargets()
        {
            yield return RuntimeSettings.Instance;
        }

        public bool RequiresMigration(Object obj)
        {
            if (obj is not RuntimeSettings settings)
            {
                return false;
            }

            return false;
        }

        public bool MigrateObject(Object obj)
        {
            if (obj is not RuntimeSettings settings)
            {
                return false;
            }

            EditorUtility.SetDirty(obj);

            return true;
        }
    }
}
