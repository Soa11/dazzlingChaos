// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Editor
{
    using System;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEditor.Callbacks;
    using UnityEngine;
    using Object = UnityEngine.Object;

    internal sealed class CoherenceSyncPostprocessor : AssetPostprocessor
    {
        // We want to run this postprocessor as late as possible, so that all prefabs are properly imported.
        // https://docs.unity3d.com/Manual/ScriptCompileOrderFolders.html
        [RunAfterAssembly("Assembly-CSharp")]
        [RunAfterAssembly("Assembly-CSharp-Editor")]
        [RunAfterClass(typeof(CoherenceSyncConfigPostprocessor))]
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (CloneMode.Enabled)
            {
                return;
            }

            try
            {
                using var assetEditing = new AssetEditingScope();
                ProcessImportedAssets(importedAssets, assetEditing);
                ProcessMovedAssets(movedAssets, assetEditing);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void ProcessImportedAssets(string[] importedAssets, AssetEditingScope assetEditing)
        {
            foreach (var assetPath in importedAssets)
            {
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (assetType != typeof(GameObject))
                {
                    continue;
                }

                // Currently we're loading every imported Prefab and checking for CoherenceSync presence,
                // but this comes at a price: loading every Prefab imported into memory.
                // Doing this as part of OnPostProcessAllAssets rather than OnPostprocessPrefab because the latter
                // doesn't allow us to create nor reference external objects (outside of the scope of the hierarchy)
                var gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                OnImportPrefab(gameObject, assetEditing);
            }
        }

        private static void ProcessMovedAssets(string[] movedPaths, AssetEditingScope assetEditing)
        {
            for (var i = 0; i < movedPaths.Length; i++)
            {
                var path = movedPaths[i];
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (assetType == typeof(GameObject))
                {
                    OnPrefabMoved(path, assetEditing);
                }
            }
        }

        private static void OnImportPrefab(GameObject gameObject, AssetEditingScope assetEditing)
        {
            // On creating and deleting assets on a postprocessor:
            // AssetDatabase calls discard the selection.
            // https://issuetracker.unity3d.com/product/unity/issues/guid/UUM-61690
            // This means that currently users will see how their Inspector window goes blank
            // after adding or removing a CoherenceSync component.

            if (gameObject.TryGetComponent(out CoherenceSync sync))
            {
                SelectionRestorer.RequestRestore();
                if (!CoherenceSyncConfigUtils.TryGetFromAsset(sync, out var config))
                {
                    assetEditing.Start(sync);
                    BakeUtil.CoherenceSyncSchemasDirty = true;
                    _ = CoherenceSyncConfigUtils.Create(sync.gameObject);
                    // This class post-processes Prefabs. The call above (CreateCoherenceSyncConfig) is creating an
                    // asset that hasn't been imported and processed yet, so we can't use it at this point.
                    // Instead, let Unity reimport this Prefab. Given the order in which postprocessors resolve,
                    // the config asset will be processed first, and then the Prefab, which will reference to the newly
                    // created CoherenceSyncConfig (logic below).
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(gameObject), ImportAssetOptions.ForceSynchronousImport);
                    return;
                }

                if (sync.CoherenceSyncConfig != config)
                {
                    assetEditing.Start(sync);
                    sync.CoherenceSyncConfig = config;
                    EditorUtility.SetDirty(sync);
                }

                _ = NetworkPrefabProcessor.TryUpdatePrefab(sync, assetEditing.Start);
                _ = EditorCache.UpdateBindings(sync, onBeforeModifications: assetEditing.Start);
            }
            else
            {
                if (!CoherenceSyncConfigUtils.TryGetFromAsset(gameObject, out var config))
                {
                    return;
                }

                BakeUtil.CoherenceSyncSchemasDirty = true;

                var path = AssetDatabase.GetAssetPath(config);
                if (!string.IsNullOrEmpty(path))
                {
                    SelectionRestorer.RequestRestore();
                    assetEditing.Start(config);
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void OnPrefabMoved(string movedPath, AssetEditingScope assetEditing)
        {
            var guid = AssetDatabase.AssetPathToGUID(movedPath);
            if (CoherenceSyncConfigRegistry.Instance.TryGetFromAssetId(guid, out var config))
            {
                assetEditing.Start(config);
                CoherenceSyncConfigUtils.InitializeProvider(config);
            }
        }
    }
}
