// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Begins an IMGUI scope inside which controls are only editable
    /// when targeting a GameObject that is persisted on disk.
    /// </summary>
    /// <remarks>
    /// Editable targets include prefab assets, objects open in Prefab Stage,
    /// and by default scene objects in Edit Mode.
    /// </remarks>
    /// <example>
    /// <code>
    /// using (new OnlyAssetsEditableScope(new GameObjectStatus(gameObject)))
    /// {
    ///     EditorGUILayout.PropertyField(serializedProperty);
    /// }
    /// </code>
    /// </example>
    public struct OnlyAssetsEditableScope : IDisposable
    {
        private bool disposed;

        public OnlyAssetsEditableScope(GameObjectStatus status, bool resultIfSceneObjectInEditMode = true)
        {
            this.disposed = false;
            EditorGUI.BeginDisabledGroup(disabled: !IsAsset());

            bool IsAsset()
            {
                if (status.IsAsset || status.IsInPrefabStage)
                {
                    return true;
                }

                return !Application.isPlaying && resultIfSceneObjectInEditMode;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            EditorGUI.EndDisabledGroup();
        }
    }
}
