// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Editor
{
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;

    /// <summary>
    /// Preprocess build step for <see cref="RuntimeSettings"/> that stores non-persistent data into a persistent form,
    /// and clears it after the build is done, failed or cancelled.
    /// </summary>
    /// <remarks>
    /// This is done to not clutter RuntimeSettings with constant changes (hence data is not persisted at editor time),
    /// while still allowing the data to be available on builds (persisted on builds).
    /// </remarks>
    /// <see cref="RuntimeSettings.Store"/>
    /// <see cref="RuntimeSettings.ClearStore"/>
    internal class RuntimeSettingsBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public async void OnPreprocessBuild(BuildReport report)
        {
            OnBeforeBuild();

            while (BuildPipeline.isBuildingPlayer)
            {
                await Task.Yield();
            }

            OnAfterBuild();
        }

        private static void OnBeforeBuild()
        {
            if (RuntimeSettings.TryGet(out var runtimeSettings))
            {
                RevisionInfo.TryUpdateSdkVersionOverride();
                runtimeSettings.Store();
                AssetDatabase.SaveAssetIfDirty(runtimeSettings);
            }
        }

        private static void OnAfterBuild()
        {
            if (RuntimeSettings.TryGet(out var runtimeSettings))
            {
                runtimeSettings.ClearStore();
                AssetDatabase.SaveAssetIfDirty(runtimeSettings);
            }
        }
    }
}
