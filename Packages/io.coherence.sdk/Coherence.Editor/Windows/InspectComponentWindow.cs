// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    internal class InspectComponentWindow<T> : EditorWindow where T : Component
    {
        public bool KeepInspectorOnChildren { get; protected set; } = true;
        public bool IsInspectingChildren => context && component && context != component.gameObject;

        private T component;

        /// <summary>
        /// Is <see cref="Component"/> a prefab asset from the Project window?
        /// </summary>
        /// <remarks>
        /// Returns <see langword="false"/> for prefab instances being edited in Prefab Mode.
        /// </remarks>
        internal bool ComponentIsPrefabAsset => isAsset && !inStage && component;

        public T Component
        {
            get => component;

            set
            {
                component = value;
                UpdateFlags();
            }
        }

        protected GameObject context;

        public GameObject Context
        {
            get => context;
            set => context = value;
        }

        protected string guid; // last stored guid for the object being viewed
        protected int instanceId; // last stored instance id for the object being viewed

        protected SerializedObject serializedObject;
        protected SerializedObject serializedGameObject;
        protected SerializedProperty componentsProperty;

        protected PrefabStage stage;

        protected bool inStage;
        protected bool isInstance;
        protected bool isAsset;

        public string searchString;

        protected virtual void UpdateFlags()
        {
            if (!component)
            {
                return;
            }

            var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            inStage = currentPrefabStage && currentPrefabStage.prefabContentsRoot == component.gameObject;
            isInstance = PrefabUtility.IsPartOfPrefabInstance(component) && !inStage;
            isAsset = PrefabUtility.IsPartOfPrefabAsset(component) || inStage;
        }

        protected virtual void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.modifierKeysChanged += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            Undo.undoRedoPerformed += OnUndoRedo;
            Refresh(forceNewSelection: true, canExitGUI: false);
        }

        protected virtual void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.modifierKeysChanged -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            Undo.undoRedoPerformed -= OnUndoRedo;

            if (componentsProperty != null)
            {
                componentsProperty.Dispose();
            }

            if (serializedObject != null)
            {
                serializedObject.Dispose();
            }

            if (serializedGameObject != null)
            {
                serializedGameObject.Dispose();
            }
        }

        protected virtual void OnDestroy() { }

        protected virtual void OnUndoRedo() => Repaint();

        protected void TryReloadFromGUID()
        {
            if (!component)
            {
                // NOTE there is an edge case where deserializing a
                // component whose gameobject contains a missing reference
                // causes the component to be null after OnEnable.
                //
                // it happens e.g. when the MonoScript that's
                // being referenced gets deleted.
                //
                // although the reference renders invalid,
                // subsequent script reloads restore it.
                //
                // here we workaround this storing the object's guid
                // at OnEnable time, and attempt to load it back
                // from the AssetDatabase.

                var s = ReloadFromGUID();
                if (s)
                {
                    Component = s;
                    Refresh();
                }
            }
        }

        private T ReloadFromGUID()
        {
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return !string.IsNullOrEmpty(path) ? AssetDatabase.LoadAssetAtPath<T>(path) : null;
            }

            return null;
        }

        protected virtual void OnGUI()
        {
            TryReloadFromGUID();
        }

        private protected void OnInspectorUpdate()
        {
            var newStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == newStage)
            {
                return;
            }

            if (stage)
            {
                OnPrefabStageClosing(stage);
            }

            if (newStage)
            {
                OnPrefabStageOpened(newStage);
            }
        }

        protected virtual void OnHierarchyChange()
        {
            Refresh(canExitGUI: false);
            Repaint();
        }

        protected virtual void OnFocus()
        {
            Refresh(canExitGUI: false);
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // when switching between play modes while selecting a reference, try to persist it.
            // Unity honors instance ids between states.

            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.ExitingPlayMode:
                    instanceId = context ? context.GetInstanceID() : 0;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                    if (instanceId != 0)
                    {
#if UNITY_6000_3_OR_NEWER
                        Selection.activeEntityId
#else
                        Selection.activeInstanceID
#endif
                            = instanceId;

                        OnSelectionChanged(true);
                    }

                    break;
            }
        }

        private void OnSelectionChanged() => OnSelectionChanged(false);

        private void OnSelectionChanged(bool exitGUI)
        {
            var newContext = Selection.activeGameObject;

            var previousComponent = component;
            var newComponent = newContext
                ? KeepInspectorOnChildren
                    ? newContext.GetComponentInParent<T>(true)
                    : newContext.GetComponent<T>()
                : null;

            var componentChanged = newComponent != previousComponent;
            var contextChanged = newContext != context;
            var changed = componentChanged || contextChanged;

            if (changed)
            {
                OnActiveSelectionChanging(previousComponent, ref newComponent);

                // Check if user canceled the selection change.
                if (componentChanged && previousComponent == newComponent)
                {
                    return;
                }
            }

            Component = newComponent;
            context = newContext;

            Refresh(canExitGUI: false);

            if (changed)
            {
                OnActiveSelectionChanged(previousComponent, newComponent);
            }

            Repaint();

            if (exitGUI && Event.current != null)
            {
                GUIUtility.ExitGUI();
            }
        }

        protected virtual void OnPrefabStageOpened(PrefabStage newStage)
        {
            stage = newStage;
            if (Selection.activeTransform && newStage.prefabContentsRoot == Selection.activeTransform.root.gameObject)
            {
                Component = newStage.prefabContentsRoot.GetComponent<T>();
                Context = component ? component.gameObject : null;
                Refresh(canExitGUI: false);
            }
        }

        protected virtual void OnPrefabStageClosing(PrefabStage oldStage)
        {
            stage = null;
            var s = AssetDatabase.LoadAssetAtPath<T>(oldStage.assetPath);
            Component = s;
            Context = s ? s.gameObject : null;
            Refresh(canExitGUI: false);
            Repaint();
        }

        public virtual void Refresh(T component, bool canExitGUI = true)
        {
            Component = component;
            Refresh(canExitGUI: canExitGUI);
            Repaint();
        }

        private int GetComponentsPropertyHash()
        {
            try
            {
                unchecked
                {
                    if (serializedObject == null)
                    {
                        return 0;
                    }

                    if (serializedGameObject == null)
                    {
                        return 0;
                    }

                    if (componentsProperty == null)
                    {
                        return 0;
                    }

                    int id = 17;
                    for (int i = 0; i < componentsProperty.arraySize; i++)
                    {
                        using (var baseComponentProperty = componentsProperty.GetArrayElementAtIndex(i))
                        using (var componentProperty = baseComponentProperty.FindPropertyRelative("component"))
                        {
                            id = (id * 23) + (componentProperty != null
                                ? componentProperty.objectReferenceInstanceIDValue
                                : 0);
                        }
                    }

                    return id;
                }
            }
            catch
            {
                // we can't guarantee the serialized objects/properties are still valid (i.e. not disposed),
                // so let's safeguard this
                return 0;
            }
        }

        public virtual void Refresh(bool forceNewSelection = false, bool canExitGUI = true)
        {
            if (forceNewSelection)
            {
                OnSelectionChanged(false);
            }

            if (!component)
            {
                guid = null;

                if (canExitGUI && Event.current != null)
                {
                    GUIUtility.ExitGUI();
                }
                return;
            }

            var componentHashBefore = GetComponentsPropertyHash();

            componentsProperty?.Dispose();
            serializedObject?.Dispose();
            serializedGameObject?.Dispose();

            serializedObject = new SerializedObject(component);
            guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(component));

            serializedGameObject = new SerializedObject(Context ? Context : component.gameObject);
            componentsProperty = serializedGameObject.FindProperty("m_Component");

            if (componentHashBefore != GetComponentsPropertyHash())
            {
                OnComponentsChanged();
            }
        }

        protected virtual void OnComponentsChanged() { }
        protected virtual void OnActiveSelectionChanging(T currentComponent, ref T nextComponent) { }
        protected virtual void OnActiveSelectionChanged(T previousComponent, T newComponent) { }
        protected void IterateComponents(Action<SerializedProperty> fn) => IterateComponents(fn, componentsProperty);

        protected void IterateComponents(Action<SerializedProperty> fn, SerializedProperty componentsProperty)
        {
            for (int i = 0; i < componentsProperty.arraySize; i++)
            {
                using (var baseComponentProperty = componentsProperty.GetArrayElementAtIndex(i))
                using (var componentProperty = baseComponentProperty.FindPropertyRelative("component"))
                {
                    fn(componentProperty);
                }
            }
        }

        protected void IterateComponents(Action<SerializedProperty> fn, GameObject gameObject)
        {
            using (var so = new SerializedObject(gameObject))
            using (var p = so.FindProperty("m_Component"))
            {
                IterateComponents(fn, p);
            }
        }

        protected void IterateOnChildren(Action<SerializedProperty> fn, Transform root)
        {
            foreach (Transform t in root)
            {
                IterateComponents(fn, t.gameObject);
                IterateOnChildren(fn, t);
            }
        }

        protected bool IncludedInSearchFilter(string content, bool exactMatch = false)
        {
            return string.IsNullOrEmpty(searchString) ||
                   (exactMatch
                       ? content.Equals(searchString, StringComparison.InvariantCultureIgnoreCase)
                       : content.ToLowerInvariant().Contains(searchString.ToLowerInvariant()));
        }
    }
}
