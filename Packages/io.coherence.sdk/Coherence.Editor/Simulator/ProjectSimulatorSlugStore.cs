// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using UnityEditor;
    using UnityEngine;

    internal static class ProjectSimulatorSlugStore
    {
        private static Dictionary<string, string> projectSlugs = new();

        static ProjectSimulatorSlugStore() => LoadValues();

        public static void Set(string key, string value)
        {
            projectSlugs[key] = value ?? "";
            Save();
        }

        /// <summary>
        /// Get the stored slug for the given key.
        /// </summary>
        /// <param name="key">A valid key.</param>
        /// <returns>The slug for the given key.
        /// If the key is <c>null</c> or cannot be found then a <c>null</c> is returned.</returns>
        [return: MaybeNull]
        public static string Get(string key) => string.IsNullOrEmpty(key) ? null : projectSlugs?.GetValueOrDefault(key);

        private static void Save()
        {
            try
            {
                const string path = Paths.simulatorProjectSlugsPath;
                var directory = Path.GetDirectoryName(path);
                _ = Directory.CreateDirectory(directory);

                var json = JsonConvert.SerializeObject(projectSlugs);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.WriteAllText(path, json);
                AssetDatabase.ImportAsset(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Keep only the keys that match the filter.
        /// </summary>
        /// <param name="filter">Filter containing the keys to keep.</param>
        public static void KeepOnly(Predicate<string> filter)
        {
            projectSlugs = projectSlugs.Where(pair => filter(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Save();
        }

        private static void LoadValues()
        {
            if (!File.Exists(Paths.simulatorProjectSlugsPath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(Paths.simulatorProjectSlugsPath);
                projectSlugs = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ??
                               new Dictionary<string, string>();
            }
            catch (Exception e)
            {
                projectSlugs = new Dictionary<string, string>();
                Debug.LogException(e);
            }
        }
    }
}
