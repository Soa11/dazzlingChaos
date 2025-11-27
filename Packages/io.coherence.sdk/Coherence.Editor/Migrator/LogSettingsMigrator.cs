// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Collections.Generic;
    using System.IO;
    using Common;
    using Newtonsoft.Json;
    using UnityEditor;
    using Object = UnityEngine.Object;

    [Preserve]
    // Migrates the LogSettings from JSON to the RuntimeSettings asset.
    internal class LogSettingsMigrator : IDataMigrator
    {
        public SemVersion MaxSupportedVersion => new(3);
        public string MigrationMessage => "LogSettings have been moved to the RuntimeSettings asset.";
        public int Order => -100;

        public void Initialize() { }

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

            return !settings.LogSettings?.MigratedToSerializedObject ?? true;
        }

        public bool MigrateObject(Object obj)
        {
            if (obj is not RuntimeSettings settings)
            {
                return false;
            }

            settings.logSettings = LoadLogSettings("Library/coherence/logSettings.json");
            settings.logSettings.MigratedToSerializedObject = true;

            EditorUtility.SetDirty(obj);

            return true;
        }

        private static Log.Settings LoadLogSettings(string path)
        {
            if (!File.Exists(path))
            {
                return new Log.Settings();
            }

            var json = File.ReadAllText(path);
            var settings = DeserializeLogSettings(json);
            return settings;
        }

        private static Log.Settings DeserializeLogSettings(string json)
        {
            var jsonSerializer = JsonSerializer.Create(null);
            using var reader = new JsonTextReader(new StringReader(json));
            return (Log.Settings)jsonSerializer.Deserialize(reader, typeof(Log.Settings));
        }
    }
}
