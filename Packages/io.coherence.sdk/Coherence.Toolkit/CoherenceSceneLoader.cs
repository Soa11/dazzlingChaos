// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Connection;
    using Log;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.SceneManagement;
    using Logger = Log.Logger;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// Handles loading one scene additively, and holds instructions for how that loaded scene should connect to the network.
    /// </summary>
    [AddComponentMenu("coherence/Scene Loading/Coherence Scene Loader")]
    [NonBindable]
    [HelpURL("https://docs.coherence.io/v/2.0/manual/multiple-connections-within-a-game-instance")]
    [CoherenceDocumentation(DocumentationKeys.CoherenceSceneLoader)]
    public sealed class CoherenceSceneLoader : CoherenceBehaviour
    {
        // for components, we don't expose direct creation of instances - add as component instead
        private CoherenceSceneLoader() { }

        private static EndpointData lastEndpointData;
        internal static readonly Dictionary<Scene, CoherenceSceneData> dataMap = new();
        internal static readonly Dictionary<Scene, CoherenceSceneLoader> loaderMap = new();
        public static readonly List<Scene> scenes = new();

        private Scene scene;
        private Logger logger;

        [Tooltip("If enabled, the loader will load/unload on CohereceBridge connections/disconnections. Otherwise, the loader only responds to the Load/Unload API.")]
        [SerializeField] private bool attach;

        public bool Attach
        {
            get => attach;
            set
            {
                attach = value;
                UpdateAttachState();
            }
        }

        [Header("Scene Loading Settings")]
        public ConnectionType connectionType = ConnectionType.Simulator;
        public string sceneName;
        public LocalPhysicsMode localPhysicsMode;
        public UnloadSceneOptions unloadSceneOptions;

        // invoked by the listener
        public UnityEvent<CoherenceBridge> onLoaded = new();
        public UnityEvent<CoherenceBridge> onBeforeUnload = new();

        public Coroutine LoadingCoroutine { get; private set; }
        public Coroutine UnloadingCoroutine { get; private set; }

        public Scene Scene => scene;

        private CoherenceBridge bridge;

        /// <summary>
        /// Has the scene that contains this CoherenceSceneLoader been loaded by another CoherenceSceneLoader instance?
        /// </summary>
        /// <remarks>
        /// Enables scenarios where the CoherenceSceneLoader is configured to load additional instances of the same scene to which it belongs.
        /// </remarks>>
        private bool IsThisSceneLoadedByAnotherLoader => dataMap.ContainsKey(gameObject.scene);

        private bool isEnabled;

        [MaybeNull] internal CoherenceScene CoherenceScene => CoherenceScene.map.GetValueOrDefault(scene);
        private Logger Logger => logger ??= Log.GetLogger<CoherenceSceneLoader>(this);

        public static CoherenceSceneLoader CreateInstance() => CreateInstance("Local Client Loader");
        public static CoherenceSceneLoader CreateInstance(string name) => CreateInstance(new GameObject(name));
        public static CoherenceSceneLoader CreateInstance(GameObject go) => go.AddComponent<CoherenceSceneLoader>();

        public CoherenceSceneLoader Configure(CoherenceSceneLoaderConfig config)
        {
            sceneName = config.sceneName;
            connectionType = config.connectionType;
            localPhysicsMode = config.localPhysicsMode;
            unloadSceneOptions = config.unloadSceneOptions;
            return this;
        }

        public CoherenceSceneLoader Configure(string sceneName)
        {
            var config = new CoherenceSceneLoaderConfig
            {
                sceneName = sceneName
            };
            return Configure(config);
        }

        public CoherenceSceneLoader Configure(string sceneName, ConnectionType connectionType)
        {
            var config = new CoherenceSceneLoaderConfig
            {
                sceneName = sceneName,
                connectionType = connectionType
            };
            return Configure(config);
        }

        public CoherenceSceneLoader Load(EndpointData endpointData)
        {
            var data = new CoherenceSceneData
            {
                SceneName = sceneName,
                ConnectionType = connectionType,
                EndpointData = endpointData,
                LocalPhysicsMode = localPhysicsMode,
            };

            LoadingCoroutine = StartCoroutine(DoLoadScene(data));
            return this;
        }

        public CoherenceSceneLoader Unload()
        {
            UnloadingCoroutine = StartCoroutine(DoUnloadScene());
            return this;
        }

        private void OnValidate()
        {
            // OnValidate is called whenever the script’s properties are set, including when an object is deserialized,
            // which can occur at various times, such as when you open a scene in the Editor and after a domain reload.
            if (Application.isPlaying && isEnabled)
            {
                UpdateAttachState();
            }
        }

        protected override void Reset()
        {
            base.Reset();

            connectionType = ConnectionType.Simulator;
            sceneName = gameObject.scene.name;
#if UNITY_EDITOR
            switch (EditorSettings.defaultBehaviorMode)
            {
                case EditorBehaviorMode.Mode3D:
                    localPhysicsMode = LocalPhysicsMode.Physics3D;
                    break;
                case EditorBehaviorMode.Mode2D:
                    localPhysicsMode = LocalPhysicsMode.Physics2D;
                    break;
                default:
                    localPhysicsMode = ~LocalPhysicsMode.None;
                    break;
            }
#else
            localPhysicsMode = ~LocalPhysicsMode.None;
#endif
            unloadSceneOptions = UnloadSceneOptions.None;
        }

        private void UpdateAttachState()
        {
            if (bridge)
            {
                bridge.Client.OnConnected -= OnConnect;
                bridge.Client.OnDisconnected -= OnDisconnect;
                bridge.Client.OnConnectionError -= OnConnectionError;
                bridge.Client.OnConnectedEndpoint -= OnConnectedEndpoint;
            }

            if (attach && !bridge)
            {
                if (!CoherenceBridgeStore.TryGetBridge(gameObject.scene, out bridge))
                {
                    Logger.Warning(Warning.ToolkitSceneLoaderMissingBridge,
                        ("scene", gameObject.scene.name));
                }
            }

            if (bridge?.Client is not { } client)
            {
                return;
            }

            client.OnConnected += OnConnect;
            client.OnDisconnected += OnDisconnect;
            client.OnConnectionError += OnConnectionError;
            client.OnConnectedEndpoint += OnConnectedEndpoint;

            if (client.IsConnected())
            {
                if (!lastEndpointData.Validate().isValid)
                {
                    lastEndpointData = client.LastEndpointData;
                }

                OnConnect(client.ClientID);
            }
        }


        private void OnEnable()
        {
            isEnabled = true;

            UpdateAttachState();
        }

        private void OnDisable()
        {
            isEnabled = false;

            if (!bridge)
            {
                return;
            }

            bridge.Client.OnConnected -= OnConnect;
            bridge.Client.OnDisconnected -= OnDisconnect;
            bridge.Client.OnConnectionError -= OnConnectionError;
            bridge.Client.OnConnectedEndpoint -= OnConnectedEndpoint;
        }

        private void OnConnect(ClientID _)
        {
            if (!IsThisSceneLoadedByAnotherLoader && attach)
            {
                // lastEndpointData is stored via OnConnectedEndpoint before OnConnect hits
                var data = new CoherenceSceneData
                {
                    SceneName = sceneName,
                    ConnectionType = connectionType,
                    EndpointData = lastEndpointData,
                    LocalPhysicsMode = localPhysicsMode,
                };
                LoadingCoroutine = StartCoroutine(DoLoadScene(data));
            }
        }

        private void OnDisconnect(ConnectionCloseReason closeReason)
        {
            if (!IsThisSceneLoadedByAnotherLoader && attach)
            {
                UnloadingCoroutine = StartCoroutine(DoUnloadScene());
            }
        }

        private void OnConnectionError(ConnectionException exception)
        {
            switch (exception)
            {
                case ConnectionDeniedException denyException:
                    Logger.Error(Error.ToolkitSceneConnectionDenied,
                        ("Reason", denyException.CloseReason));

                    break;
                default:
                    Logger.Error(Error.ToolkitSceneConnectionError,
                        ("exception", exception.Message));

                    break;
            }
        }

        private void OnConnectedEndpoint(EndpointData endpointData)
        {
            if (IsThisSceneLoadedByAnotherLoader)
            {
                return;
            }

            lastEndpointData = endpointData;
        }

        private IEnumerator DoUnloadScene()
        {
            if (LoadingCoroutine != null)
            {
                yield return LoadingCoroutine;
            }

            CoherenceBridge listenerBridge = null;
            if (CoherenceScene.map.TryGetValue(scene, out var listener))
            {
                listenerBridge = listener.Bridge;
                if (listenerBridge)
                {
                    listenerBridge.Client?.Disconnect();
                }
            }

            onBeforeUnload.Invoke(listenerBridge);

            _ = loaderMap.Remove(scene);
            _ = dataMap.Remove(scene);
            _ = scenes.Remove(scene);

            if (scene.IsValid())
            {
                yield return SceneManager.UnloadSceneAsync(scene, unloadSceneOptions);
            }
        }

        private IEnumerator DoLoadScene(CoherenceSceneData data)
        {
            if (UnloadingCoroutine != null)
            {
                yield return UnloadingCoroutine;
            }

            var index = SceneManager.sceneCount;

            var op = SceneManager.LoadSceneAsync(sceneName, new LoadSceneParameters(LoadSceneMode.Additive, data.LocalPhysicsMode));
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                yield return null;
            }

            var scene = GetScene();
            this.scene = scene;
            scenes.Add(scene);
            dataMap.Add(scene, data);
            loaderMap.Add(scene, this);
            var loadCompleted = false;
            SceneManager.sceneLoaded += OnSomeSceneLoaded;
            op.allowSceneActivation = true;

            while (!op.isDone || !loadCompleted)
            {
                yield return null;
            }

            Scene GetScene()
            {
                if (SceneManager.sceneCount > index)
                {
                    if (TryGetSceneAt(index, out var result))
                    {
                        return result;
                    }
                }

                for(var i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    if (TryGetSceneAt(i, out var result))
                    {
                        return result;
                    }
                }

                throw new($"Failed to find loaded scene '{sceneName}' (expected index: {index}, scene count: {SceneManager.sceneCount})");

                bool TryGetSceneAt(int i, out Scene sceneAt)
                {
                    sceneAt = SceneManager.GetSceneAt(i);
                    if (string.Equals(sceneAt.name, sceneName) && !sceneAt.isLoaded && !scenes.Contains(sceneAt))
                    {
                        return true;
                    }

                    return false;
                }
            }

            void OnSomeSceneLoaded(Scene loadedScene, LoadSceneMode mode)
            {
                if (loadedScene != scene)
                {
                    return;
                }

                if (!CoherenceScene.map.ContainsKey(scene))
                {
                    var activeSceneWas = SceneManager.GetActiveScene();
                    if (SceneManager.SetActiveScene(scene))
                    {
                        _ = new GameObject("CoherenceScene (Runtime)", typeof(CoherenceScene));

                        Logger.Warning(Warning.ToolkitSceneMissingScene, ("scene", scene.name));

                        SceneManager.SetActiveScene(activeSceneWas);
                    }
                }

                loadCompleted = true;
            }
        }
    }
}
