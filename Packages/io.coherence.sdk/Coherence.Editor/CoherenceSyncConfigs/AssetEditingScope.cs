// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using UnityEditor;

    /// <summary>
    /// Responsible for starting and finishing a batch of asset modifications,
    /// causing asset importing to be delayed until all modifications have been performed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Start()"/> should be called before any operation that modifies an asset.
    /// This class ensures that <see cref="AssetDatabase.StartAssetEditing"/> is only called once.
    /// </para>
    /// <para>
    /// <see cref="Stop"/> or <see cref="Dispose"/> should be called after every potential
    /// asset modification belonging to the same batch has been performed.
    /// This class ensures that <see cref="AssetDatabase.StopAssetEditing"/> is only called
    /// if <see cref="Start()"/> was called at least once.
    /// </para>
    /// </remarks>
    internal sealed class AssetEditingScope : IDisposable
    {
        private bool startedAssetEditing;

        public void Start()
        {
            if (!startedAssetEditing)
            {
                startedAssetEditing = true;
                AssetDatabase.StartAssetEditing();
            }
        }

        public void Start(Object asset) => Start();

        public void Stop() => Dispose();

        public void Dispose()
        {
            if (startedAssetEditing)
            {
                AssetDatabase.StopAssetEditing();
                startedAssetEditing = false;
            }
        }
    }
}
