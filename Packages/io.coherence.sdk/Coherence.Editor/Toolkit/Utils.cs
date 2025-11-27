// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System.Diagnostics.CodeAnalysis;
    using UnityEditor;
    using UnityEngine;
    using Coherence.Toolkit;
    using Coherence.Simulator;
    using Coherence.UI;
    using UnityEditor.SceneManagement;

    internal static class Utils
    {
        private const int BaseGroup = 20;
        private const int GlobalGroup = BaseGroup + 0;
        private const int QueriesGroup = BaseGroup + 1;
        private const int CloudGroup = BaseGroup + 20;
        private const int SceneLoadingGroup = BaseGroup + 40;
        private const int SimulatorGroup = BaseGroup + 60;
        private const int ImportGroup = BaseGroup + 100;

        public static GameObject CreateInstance<T>(string name, GameObject parent) where T : Component
        {
            var go = ObjectFactory.CreateGameObject(name, typeof(T));
            GameObjectCreationCommands.Place(go, parent);
            return go;
        }

        public static GameObject CreateInstance<T>(string name) where T : Component
        {
            return CreateInstance<T>(name, null);
        }

        // global

        [MenuItem("GameObject/coherence/Bridge", false, GlobalGroup)]
        public static void AddBridgeInstanceInScene(MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "bridge"));
            _ = CreateInstance<CoherenceBridge>(nameof(CoherenceBridge), menuCommand.context as GameObject);
        }

        // queries

        [MenuItem("GameObject/coherence/Live Query", false, QueriesGroup + 1)]
        public static void AddLiveQueryInstanceInScene(MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "live_query"));
            _ = CreateInstance<CoherenceLiveQuery>(nameof(CoherenceLiveQuery), menuCommand.context as GameObject);
        }

        [MenuItem("GameObject/coherence/Tag Query", false, QueriesGroup + 2)]
        public static void AddTagQueryInstanceInScene(MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "tag_query"));
            _ = CreateInstance<CoherenceTagQuery>(nameof(CoherenceTagQuery), menuCommand.context as GameObject);
        }

        [MenuItem("GameObject/coherence/Global Query", false, QueriesGroup + 3)]
        public static void AddGlobalQueryInstanceInScene(MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "global_query"));
            _ = CreateInstance<CoherenceGlobalQuery>(nameof(CoherenceGlobalQuery), menuCommand.context as GameObject);
        }

        // coherence Cloud

        [MenuItem("GameObject/coherence/Cloud Login", false, CloudGroup)]
        public static void AddCloudLoginInstanceInScene(MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "cloud_login"));
            Selection.activeGameObject = CreateInstance<CoherenceCloudLogin>(nameof(CoherenceCloudLogin), menuCommand.context as GameObject);
        }

        // scene loading

        [MenuItem("GameObject/coherence/Scene Loader", false, SceneLoadingGroup)]
        public static void AddCoherenceSceneLoaderInstanceInScene(MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "scene_loader"));
            _ = CreateInstance<CoherenceSceneLoader>(nameof(CoherenceSceneLoader), menuCommand.context as GameObject);
        }

        [MenuItem("GameObject/coherence/Scene (Listener)", false, SceneLoadingGroup + 1)]
        public static void AddCoherenceSceneInstanceInScene(MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "scene"));
            _ = CreateInstance<CoherenceScene>(nameof(CoherenceScene), menuCommand.context as GameObject);
        }

        // sims

        [MenuItem("GameObject/coherence/Auto Simulator Connection", false, SimulatorGroup)]
        public static void AddAutoSimulatorConnection([DisallowNull] MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "auto_sim_connection"));
            _ = CreateInstance<AutoSimulatorConnection>(nameof(AutoSimulatorConnection), menuCommand.context as GameObject);
        }

        [MenuItem("GameObject/coherence/Open Sample UIs in Hub", false, ImportGroup)]
        public static void AddSampleDialogInScene([DisallowNull] MenuCommand menuCommand)
        {
            Analytics.Capture(Analytics.Events.MenuItem, ("menu", "gameobject"), ("item", "connect_dialog"));
            var samplesModule = CoherenceHub.Open<SamplesModule>();
            EditorApplication.delayCall += () => samplesModule.SetCategory("UIs");
        }
    }
}
