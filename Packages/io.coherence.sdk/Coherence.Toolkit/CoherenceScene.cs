// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.SceneManagement;
    using Common;
    using Connection;
    using Log;
    using Logger = Log.Logger;
#if UNITY_EDITOR
    using UnityEditor.SceneManagement;
#endif

    /// <summary>
    /// Reacts when the scene this component is on is loaded via <see cref="CoherenceSceneLoader"/>.
    /// When it does, establishes a connection using the <see cref="CoherenceBridge"/> on said scene.
    /// The connection data used (EndpointData) is facilitated by the <see cref="CoherenceSceneLoader"/>
    /// that triggered the scene load.
    /// </summary>
    [AddComponentMenu("coherence/Scene Loading/Coherence Scene")]
    [DefaultExecutionOrder(ScriptExecutionOrder.CoherenceScene)]
    [DisallowMultipleComponent]
    [NonBindable]
    [HelpURL("https://docs.coherence.io/v/2.0/manual/multiple-connections-within-a-game-instance")]
    [CoherenceDocumentation(DocumentationKeys.CoherenceScene)]
    public sealed class CoherenceScene : CoherenceBehaviour
    {
        // for components, we don't expose direct creation of instances - add as component instead
        private CoherenceScene()
        {
        }

        private enum EditorSceneVisibility
        {
            [InspectorName("Don't Show")]
            DontShow,
            ShowOnAwake,
            ShowOnConnect,
        }

        private Logger logger;
        internal static readonly Dictionary<Scene, CoherenceScene> map = new();

        internal bool Active => CoherenceSceneLoader.dataMap.ContainsKey(gameObject.scene);

        private PhysicsScene physicsScene;
        private PhysicsScene2D physicsScene2d;

        /// <summary>
        /// The <see cref="CoherenceBridge"/> associated with the scene this component is in.
        /// </summary>
        [MaybeNull]
        public CoherenceBridge Bridge => CoherenceBridgeStore.TryGetBridge(gameObject.scene, out var bridge) ? bridge : null;

        /// <summary>
        /// Is the <see cref="Bridge"/> connected?
        /// </summary>
        public bool IsConnected
        {
            get
            {
                var bridge = Bridge;
                return bridge && (bridge.Client?.IsConnected() ?? false);
            }
        }

        [Tooltip("Once enabled, connect to the replication server using the loader's provided endpoint.")]
        public bool connect = true;
        public float reconnectDelay = 3f;
        public int maxRetries = 2;

#pragma warning disable CS0219,CS0414
        [Header("Editor Scene Visibility")]
        [SerializeField] private EditorSceneVisibility sceneVisibilityForClient = EditorSceneVisibility.DontShow;
        [SerializeField] private EditorSceneVisibility sceneVisibilityForSimulator = EditorSceneVisibility.DontShow;
        [SerializeField] private bool hideEditorSceneOnDisconnect = true;
#pragma warning restore CS0219,CS0414

        [Tooltip("List of GameObjects to deactivate when this CoherenceScene completes loading through a CoherenceSceneLoader.")]
        public GameObject[] deactivateOnLoad;

        public UnityEvent onLoaded;

        private Coroutine reconnectCoroutine;
        private IClient subscribedToClient;
        private bool connecting;

        private void Awake()
        {
            logger = Log.GetLogger<CoherenceScene>(this);

            var bridge = Bridge;
            if (!bridge)
            {
                logger.Warning(Warning.ToolkitSceneMissingBridge, ("scene", gameObject.scene.name));
            }

            if (!Active)
            {
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            if (!bridge)
            {
                return;
            }

            var editorSceneVisibility = bridge.ConnectionType == ConnectionType.Simulator ? sceneVisibilityForSimulator : sceneVisibilityForClient;

            if (editorSceneVisibility == EditorSceneVisibility.ShowOnAwake)
            {
                // render scene
                EditorSceneManager.SetSceneCullingMask(gameObject.scene, EditorSceneManager.GetSceneCullingMask(gameObject.scene) | 0xE000000000000000UL);
            }
            else
            {
                // don't render scene
                EditorSceneManager.SetSceneCullingMask(gameObject.scene, EditorSceneManager.GetSceneCullingMask(gameObject.scene) & ~0xE000000000000000UL);
            }
#endif
        }

        private void OnEnable()
        {
            if (!Active)
            {
                enabled = false;
                return;
            }

            if (map.ContainsKey(gameObject.scene))
            {
                enabled = false;
                logger.Warning(Warning.ToolkitSceneAlreadyExists,
                    ("scene", gameObject.scene.name));
                return;
            }

            var bridge = Bridge;
            subscribedToClient = bridge ? bridge.Client : null;
            if (subscribedToClient is null)
            {
                enabled = false;
                logger.Warning(Warning.ToolkitSceneMissingClient, ("scene", gameObject.scene.name));
                return;
            }

            map.Add(gameObject.scene, this);

            subscribedToClient.OnConnected += OnConnect;
            subscribedToClient.OnDisconnected += OnDisconnect;
            subscribedToClient.OnConnectionError += OnConnectionError;

            FetchPhysics();

            if (deactivateOnLoad != null)
            {
                foreach (var go in deactivateOnLoad)
                {
                    if (!go)
                    {
                        continue;
                    }

                    go.SetActive(false);
                }
            }

            onLoaded?.Invoke();

            if (CoherenceSceneLoader.loaderMap.TryGetValue(gameObject.scene, out var loader))
            {
                loader.onLoaded.Invoke(bridge);
            }
        }

        private void Start()
        {
            TryConnect();
            reconnectCoroutine = StartCoroutine(DoReconnect(reconnectDelay));
        }

        private void FixedUpdate()
        {
            if (CoherenceSceneLoader.dataMap.TryGetValue(gameObject.scene, out var data))
            {
                switch (data.LocalPhysicsMode)
                {
                    case LocalPhysicsMode.None:
                        break;
                    case LocalPhysicsMode.Physics2D:
                        _ = physicsScene2d.Simulate(Time.fixedDeltaTime);
                        break;
                    case LocalPhysicsMode.Physics3D:
                        physicsScene.Simulate(Time.fixedDeltaTime);
                        break;
                }
            }
        }

        private void FetchPhysics()
        {
            if (CoherenceSceneLoader.dataMap.TryGetValue(gameObject.scene, out var data))
            {
                switch (data.LocalPhysicsMode)
                {
                    case LocalPhysicsMode.None:
                        break;
                    case LocalPhysicsMode.Physics2D:
                        physicsScene2d = gameObject.scene.GetPhysicsScene2D();
                        break;
                    case LocalPhysicsMode.Physics3D:
                        physicsScene = gameObject.scene.GetPhysicsScene();
                        break;
                }
            }
        }

        private void TryConnect()
        {
            connecting = true;
            if (!connect || !CoherenceSceneLoader.dataMap.TryGetValue(gameObject.scene, out var data))
            {
                return;
            }

            var bridge = Bridge;
            if (!bridge || bridge.Client is not { } client)
            {
                return;
            }

            if (!ReferenceEquals(client, subscribedToClient))
            {
                if (subscribedToClient is not null)
                {
                    subscribedToClient.OnConnected -= OnConnect;
                    subscribedToClient.OnDisconnected -= OnDisconnect;
                    subscribedToClient.OnConnectionError -= OnConnectionError;
                }

                subscribedToClient = client;

                subscribedToClient.OnConnected += OnConnect;
                subscribedToClient.OnDisconnected += OnDisconnect;
                subscribedToClient.OnConnectionError += OnConnectionError;
            }

            if (!client.IsConnected())
            {
                logger.Debug($"Trying to connect to {data.EndpointData} as {data.ConnectionType}");
                // TODO expose connection settings
                var settings = ConnectionSettings.Default;
                settings.UseDebugStreams = RuntimeSettings.Instance.UseDebugStreams;
                bridge.Connect(data.EndpointData, data.ConnectionType, settings);
            }
        }

        private void OnDisable()
        {
            if (map.TryGetValue(gameObject.scene, out var listener) && listener == this)
            {
                _ = map.Remove(gameObject.scene);
            }

            if (subscribedToClient is not null)
            {
                subscribedToClient.OnConnected -= OnConnect;
                subscribedToClient.OnDisconnected -= OnDisconnect;
                subscribedToClient.OnConnectionError -= OnConnectionError;
                subscribedToClient = null;
            }
        }

        private void OnConnect(ClientID _)
        {
#if UNITY_EDITOR
            var bridge = Bridge;
            if (bridge)
            {
                var editorSceneVisibility = bridge.ConnectionType == ConnectionType.Simulator ? sceneVisibilityForSimulator : sceneVisibilityForClient;

                if (editorSceneVisibility == EditorSceneVisibility.ShowOnConnect)
                {
                    // render scene
                    EditorSceneManager.SetSceneCullingMask(gameObject.scene, EditorSceneManager.GetSceneCullingMask(gameObject.scene) | 0xE000000000000000UL);
                }
            }
#endif
            connecting = false;

            if (reconnectCoroutine is not null)
            {
                StopCoroutine(reconnectCoroutine);
            }
        }

        private void OnDisconnect(ConnectionCloseReason closeReason)
        {
#if UNITY_EDITOR
            if (hideEditorSceneOnDisconnect)
            {
                // don't render scene
                EditorSceneManager.SetSceneCullingMask(gameObject.scene, EditorSceneManager.GetSceneCullingMask(gameObject.scene) & ~0xE000000000000000UL);
            }
#endif
            connecting = false;

            if (closeReason != ConnectionCloseReason.GracefulClose)
            {
                reconnectCoroutine = StartCoroutine(DoReconnect(reconnectDelay));
            }
        }

        private void OnConnectionError(ConnectionException exception)
        {
            connecting = false;

            switch (exception)
            {
                case ConnectionDeniedException denyException:
                    logger.Error(Error.ToolkitSceneConnectionDenied,
                        ("Reason", denyException.CloseReason));

                    // Can't connect because the room port is reused but the ID changed.
                    StopCoroutine(reconnectCoroutine);

                    if (CoherenceSceneLoader.loaderMap.TryGetValue(gameObject.scene, out var loader))
                    {
                        loader.Unload();
                    }

                    break;
                default:
                    logger.Error(Error.ToolkitSceneConnectionError,
                        ("exception", exception.Message));
                    break;
            }
        }

        private IEnumerator DoReconnect(float delay)
        {
            while (connecting)
            {
                yield return null;
            }

            if (subscribedToClient?.IsConnected() ?? false)
            {
                yield break;
            }

            var retryCount = 0;

            while (true)
            {
                // skip one frame in case the disconnects occurs on a shutdown/disable/destroy
                yield return null;

                logger.Warning(Warning.ToolkitSceneReconnect, $"Trying to reconnect in {delay} seconds...");

                yield return new WaitForSecondsRealtime(delay);
                TryConnect();

                while (connecting)
                {
                    yield return null;
                }

                if (subscribedToClient?.IsConnected() ?? false)
                {
                    yield break;
                }

                retryCount++;

                // It's likely the RS was rebooted and the endpoint is forever invalid, so we
                // only retry a certain number of times before giving up.
                if (retryCount >= maxRetries)
                {
                    logger.Error(Error.ToolkitSceneFailedToReconnect, ("retries", retryCount));
                    if (CoherenceSceneLoader.loaderMap.TryGetValue(gameObject.scene, out var loader))
                    {
                        loader.Unload();
                    }

                    yield break;
                }
            }
        }
    }
}
