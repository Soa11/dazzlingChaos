// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Common;
    using Log;
    using Runtime;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Logger = Log.Logger;

    public class LobbiesService : IAsyncDisposable, IDisposable
    {
        private readonly IRequestFactory requestFactory;
        private readonly IAuthClientInternal authClient;
        private readonly RequestThrottle throttle;
        private readonly IRuntimeSettings runtimeSettings;
        private readonly Logger logger = Log.GetLogger<LobbiesService>();

        private const string lobbiesResolveEndpoint = "/lobbies";
        internal const string refreshLobbiesAsyncPathParams = "?ids={0}";
        private const string playCallback = lobbiesResolveEndpoint + "/play";

        private readonly HashSet<string> lobbySessionIds = new();
        private readonly Dictionary<string, LobbySession> lobbySessions = new();
        private readonly List<Action<RequestResponse<IReadOnlyList<LobbyData>>>> fetchLobbiesCallbackList = new();
        private bool shouldDisposeRequestFactoryAndAuthClient;

        /// <summary>
        /// Callback that will be invoked when a Lobby owner you're a part of starts a game.
        /// The Callback contains the Lobby ID and the RoomData that you can use to join the game session through CoherenceBridge.JoinRoom.
        /// If a Callback is not supplied, coherence will automatically join the specified RoomData.
        /// </summary>
        public event Action<string, RoomData> OnPlaySessionStarted;

        internal event Action<RoomData> OnPlaySessionStartedInternal;

        public LobbiesService(CloudCredentialsPair credentialsPair = null, IRuntimeSettings runtimeSettings = null) : this(credentialsPair, runtimeSettings, null) { }

        internal LobbiesService([MaybeNull] CloudCredentialsPair credentialsPair, [MaybeNull] IRuntimeSettings runtimeSettings, [MaybeNull] RequestThrottle throttle)
        {
#if UNITY
            runtimeSettings ??= RuntimeSettings.Instance;
#endif
            this.runtimeSettings = runtimeSettings;

            if (credentialsPair is null)
            {
                shouldDisposeRequestFactoryAndAuthClient = true;
                credentialsPair = CloudCredentialsFactory.ForClient(runtimeSettings);
                credentialsPair.authClient.LoginAsGuest().Then(task => logger.Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception.ToString()), TaskContinuationOptions.OnlyOnFaulted);
            }

            this.requestFactory = credentialsPair.requestFactory;
            this.authClient = credentialsPair.authClient;
            this.throttle = throttle ?? credentialsPair.requestFactory.Throttle;

            this.authClient.OnLogin += OnLogin;
            this.authClient.OnLogout += OnLogout;

            requestFactory.AddPushCallback(playCallback, OnPlayStarted);
        }

        /// <returns>Returns the internal cooldown for the Find Or Create Lobby endpoint.</returns>
        public TimeSpan GetFindOrCreateLobbyCooldown()
        {
            return requestFactory.GetRequestCooldown(lobbiesResolveEndpoint + "/match", "POST");
        }

        /// <summary>Endpoint to do matchmaking and find a suitable Lobby. If no suitable Lobby is found, one will be created using the CreateLobbyOptions.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="findOptions">Options that will be used to try to find a suitable Lobby.</param>
        /// <param name="createOptions">Options that will be used to create a Lobby if no suitable Lobby is found.</param>
        public void FindOrCreateLobby(FindLobbyOptions findOptions, CreateLobbyOptions createOptions, Action<RequestResponse<LobbySession>> onRequestFinished)
        {
            var pathParams = "/match";

            var requestBodyForRegion = FindLobbyRequest.GetRequestBody(findOptions, createOptions);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBodyForRegion,
                null, $"{nameof(LobbiesService)}.{nameof(FindOrCreateLobby)}", authClient.SessionToken, response =>
            {
                var requestResponse = RequestResponse<LobbySession>.GetRequestResponse(response);

                if (requestResponse.Status == RequestStatus.Fail)
                {
                    onRequestFinished?.Invoke(requestResponse);
                    return;
                }

                try
                {
                    LobbyData lobby = DeserializeLobbyData(response.Result);

                    logger.Trace("FindLobby - end", ("lobbyId", lobby.Id));

                    requestResponse.Result = AddOrUpdateSession(lobby);
                }
                catch (Exception exception)
                {
                    requestResponse.Status = RequestStatus.Fail;
                    requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                    logger.Error(Error.RuntimeCloudDeserializationException,
                        ("Request", nameof(FindOrCreateLobby)),
                        ("Response", response.Result),
                        ("exception", exception));
                }
                finally
                {
                    onRequestFinished?.Invoke(requestResponse);
                }
            });
        }

        /// <summary>Endpoint to do matchmaking and find a suitable Lobby. If no suitable Lobby is found, one will be created using the CreateLobbyOptions.</summary>
        /// <param name="findOptions">Options that will be used to try to find a suitable Lobby.</param>
        /// <param name="createOptions">Options that will be used to create a Lobby if no suitable Lobby is found.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<LobbySession> FindOrCreateLobbyAsync(FindLobbyOptions findOptions, CreateLobbyOptions createOptions, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            var pathParams = "/match";
            var requestBodyForRegion = FindLobbyRequest.GetRequestBody(findOptions, createOptions);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, requestBodyForRegion, null, $"{nameof(LobbiesService)}.{nameof(FindOrCreateLobbyAsync)}", authClient.SessionToken);

            try
            {
                LobbyData lobby = DeserializeLobbyData(textResponse);

                logger.Trace("FindLobbyAsync - end", ("lobbyId", lobby.Id));

                return AddOrUpdateSession(lobby);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(FindOrCreateLobbyAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <returns>Returns the internal cooldown for the Create Lobby endpoint.</returns>
        public TimeSpan GetCreateLobbyCooldown()
        {
            return requestFactory.GetRequestCooldown(lobbiesResolveEndpoint, "POST");
        }

        /// <summary>Endpoint to create a Lobby directly without doing matchmaking.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="createOptions">Options that will be used to create a Lobby.</param>
        public void CreateLobby(CreateLobbyOptions createOptions, Action<RequestResponse<LobbySession>> onRequestFinished)
        {
            var requestBodyForRegion = LobbyCreationRequest.GetRequestBody(createOptions);

            requestFactory.SendRequest(lobbiesResolveEndpoint, "POST", requestBodyForRegion,
                null, $"{nameof(LobbiesService)}.{nameof(CreateLobby)}", authClient.SessionToken, response =>
                {
                    var requestResponse = RequestResponse<LobbySession>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    try
                    {
                        LobbyData lobby = DeserializeLobbyData(response.Result);

                        logger.Trace("CreateLobby - end", ("lobbyId", lobby.Id));

                        requestResponse.Result = AddOrUpdateSession(lobby);
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                        logger.Error(Error.RuntimeCloudDeserializationException,
                            ("Request", nameof(CreateLobby)),
                            ("Response", response.Result),
                            ("exception", exception));
                    }
                    finally
                    {
                        onRequestFinished?.Invoke(requestResponse);
                    }
                });
        }

        /// <summary>Endpoint to create a Lobby directly without doing matchmaking.</summary>
        /// <param name="createOptions">Options that will be used to create a Lobby.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<LobbySession> CreateLobbyAsync(CreateLobbyOptions createOptions, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            var requestBodyForRegion = LobbyCreationRequest.GetRequestBody(createOptions);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint,
                method, requestBodyForRegion, null, $"{nameof(LobbiesService)}.{nameof(CreateLobbyAsync)}", authClient.SessionToken);

            try
            {
                LobbyData lobby = DeserializeLobbyData(textResponse);

                logger.Trace("CreateLobbyAsync - end", ("lobbyId", lobby.Id));

                return AddOrUpdateSession(lobby);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(CreateLobbyAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <returns>Returns the internal cooldown for the Find Lobbies endpoint.</returns>
        public TimeSpan GetFindLobbiesCooldown()
        {
            return requestFactory.GetRequestCooldown(lobbiesResolveEndpoint + "/search", "POST");
        }

        /// <summary>Find current active Lobbies that you will be able to join.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="findOptions">Optional parameter to filter the returned Lobbies.</param>
        public void FindLobbies(Action<RequestResponse<IReadOnlyList<LobbyData>>> onRequestFinished, FindLobbyOptions findOptions = null)
        {
            if (WaitForOngoingRequest(onRequestFinished))
            {
                return;
            }

            var pathParams = "/search";

            var requestBodyForRegion = FetchLobbiesRequest.GetRequestBody(findOptions);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBodyForRegion,
                null, $"{nameof(LobbiesService)}.{nameof(FindLobbies)}", authClient.SessionToken, response =>
                {
                    var requestResponse = RequestResponse<IReadOnlyList<LobbyData>>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        requestResponse.Result = new List<LobbyData>();

                        foreach (var callback in fetchLobbiesCallbackList)
                        {
                            callback?.Invoke(requestResponse);
                        }

                        fetchLobbiesCallbackList.Clear();
                        return;
                    }

                    try
                    {
                        var lobbies = OnFetch(response.Result);

                        requestResponse.Result = lobbies;
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                        logger.Error(Error.RuntimeCloudDeserializationException,
                            ("Request", nameof(FindLobbies)),
                            ("Response", response.Result),
                            ("exception", exception));
                    }
                    finally
                    {
                        foreach (var callback in fetchLobbiesCallbackList)
                        {
                            callback?.Invoke(requestResponse);
                        }

                        fetchLobbiesCallbackList.Clear();
                    }
                });
        }

        /// <summary>Find current active Lobbies that you will be able to join.</summary>
        /// <param name="findOptions">Optional parameter to filter the returned Lobbies.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<IReadOnlyList<LobbyData>> FindLobbiesAsync(FindLobbyOptions findOptions = null, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            const string pathParams = "/search";

            var requestBodyForRegion = FetchLobbiesRequest.GetRequestBody(findOptions);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, requestBodyForRegion, null, $"{nameof(LobbiesService)}.{nameof(FindLobbiesAsync)}", authClient.SessionToken);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var lobbies = OnFetch(textResponse);

                logger.Trace("FindLobbiesAsync - end", ("lobbies count", lobbies.Count));

                return lobbies;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(FindLobbiesAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <summary>Join the supplied Lobby.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="playerAttr">Optional parameter to add player attributes to the joined Lobby.</param>
        /// <param name="secret">Optional parameter to specify the Secret to join a private Lobby.</param>
        public void JoinLobby(LobbyData lobby, Action<RequestResponse<LobbySession>> onRequestFinished,
            List<CloudAttribute> playerAttr = null, string secret = null)
        {
            var pathParams = $"/{lobby.Id}/players";

            var requestBody = JoinLobbyRequest.GetRequestBody(playerAttr, secret);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBody,
                null, $"{nameof(LobbiesService)}.{nameof(JoinLobby)}", authClient.SessionToken, response =>
                {
                    var requestResponse = RequestResponse<LobbySession>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    try
                    {
                        LobbyData joinedLobby = DeserializeLobbyData(response.Result);

                        logger.Trace("JoinLobby - end", ("lobbyId", joinedLobby.Id));

                        requestResponse.Result = AddOrUpdateSession(joinedLobby);
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                        logger.Error(Error.RuntimeCloudDeserializationException,
                            ("Request", nameof(JoinLobby)),
                            ("Response", response.Result),
                            ("exception", exception));
                    }
                    finally
                    {
                        onRequestFinished?.Invoke(requestResponse);
                    }
                });
        }

        /// <summary>Join the supplied Lobby.</summary>
        /// <param name="playerAttr">Optional parameter to add player attributes to the joined Lobby.</param>
        /// <param name="secret">Optional parameter to specify the Secret to join a private Lobby.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<LobbySession> JoinLobbyAsync(LobbyData lobby, List<CloudAttribute> playerAttr = null, string secret = null, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            var pathParams = $"/{lobby.Id}/players";

            var requestBody = JoinLobbyRequest.GetRequestBody(playerAttr, secret);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, requestBody, null, $"{nameof(LobbiesService)}.{nameof(JoinLobbyAsync)}", authClient.SessionToken);

            try
            {
                LobbyData joinedLobby = DeserializeLobbyData(textResponse);

                logger.Trace("JoinLobbyAsync - end", ("lobbyId", joinedLobby.Id));

                return AddOrUpdateSession(joinedLobby);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(JoinLobbyAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <summary>Refresh the current data for the supplied Lobby.</summary>
        /// <param name="lobby">Lobby you want to refresh the data of.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        public void RefreshLobby(LobbyData lobby, Action<RequestResponse<LobbyData>> onRequestFinished)
        {
            RefreshLobby(lobby.Id, onRequestFinished);
        }

        /// <summary>Refresh the current data for the supplied Lobby.</summary>
        /// <param name="lobby">Lobby you want to refresh the data of.</param>
        public async Task<LobbyData> RefreshLobbyAsync(LobbyData lobby)
        {
            return await RefreshLobbyAsync(lobby.Id);
        }

        /// <summary>Refresh the current data for the Lobby with the supplied id.</summary>
        /// <param name="lobbyId">Lobby ID you want to refresh the data of.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        public void RefreshLobby(string lobbyId, Action<RequestResponse<LobbyData>> onRequestFinished)
        {
            var pathParams = $"/{lobbyId}";

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "GET", null,
                null, $"{nameof(LobbiesService)}.{nameof(RefreshLobby)}", authClient.SessionToken, response =>
                {
                    var requestResponse = RequestResponse<LobbyData>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    try
                    {
                        LobbyData updatedLobby = DeserializeLobbyData(response.Result);

                        logger.Trace("RefreshLobby - end", ("lobbyId", updatedLobby.Id));

                        requestResponse.Result = updatedLobby;

                        CreateActiveLobbySessionIfPlayerIsInLobby(updatedLobby);
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                        logger.Error(Error.RuntimeCloudDeserializationException,
                            ("Request", nameof(RefreshLobby)),
                            ("Response", response.Result),
                            ("exception", exception));
                    }
                    finally
                    {
                        onRequestFinished?.Invoke(requestResponse);
                    }
                });
        }

        /// <summary>Refresh the current data for the Lobby with the supplied id.</summary>
        /// <param name="lobbyId">Lobby ID you want to refresh the data of.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <exception cref="TaskCanceledException">
        /// Thrown when the operation is canceled using the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <exception cref="ResponseDeserializationException">
        /// Thrown when deserializing the response from the server fails.
        /// </exception>
        public async Task<LobbyData> RefreshLobbyAsync(string lobbyId, CancellationToken cancellationToken = default)
        {
            const string method = "GET";
            var pathParams = $"/{lobbyId}";

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, null, null, $"{nameof(LobbiesService)}.{nameof(RefreshLobbyAsync)}", authClient.SessionToken);

            try
            {
                LobbyData updatedLobby = DeserializeLobbyData(textResponse);

                logger.Trace("RefreshLobbyAsync - end", ("lobbyId", updatedLobby.Id));

                CreateActiveLobbySessionIfPlayerIsInLobby(updatedLobby);

                return updatedLobby;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(RefreshLobbyAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <summary>Refresh the current data for the lobbies with the supplied ids.</summary>
        /// <param name="lobbyIds">IDs of the lobbies you want to refresh the data of.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <exception cref="TaskCanceledException">
        /// Thrown when the operation is canceled using the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <exception cref="ResponseDeserializationException">
        /// Thrown when deserializing the response from the server fails.
        /// </exception>
        public async Task<IReadOnlyList<LobbyData>> RefreshLobbiesAsync(string[] lobbyIds, CancellationToken cancellationToken = default)
        {
            const string method = "GET";
            var idsString = string.Join(",", lobbyIds);
            var pathParams = string.Format(refreshLobbiesAsyncPathParams, idsString);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, null, null, $"{nameof(LobbiesService)}.{nameof(RefreshLobbyAsync)}", authClient.SessionToken);

            try
            {
                var updatedLobbies = DeserializeLobbiesData(textResponse);

                logger.Trace("RefreshLobbyAsync - end", ("lobbyIds", idsString));

                foreach (var updatedLobby in updatedLobbies)
                {
                    CreateActiveLobbySessionIfPlayerIsInLobby(updatedLobby);
                }

                return updatedLobbies;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(RefreshLobbyAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <summary>Get stats for the usage of Lobbies for your current coherence Project.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="tags">Optional list of tags to filter the fetched stats.</param>
        /// <param name="regions">Optional list of regions to filter the fetched stats.</param>
        public void FetchLobbyStats(Action<RequestResponse<LobbyStats>> onRequestFinished, List<string> tags = null, List<string> regions = null)
        {
            var pathParams = "/stats";

            var requestBody = StatsRequest.GetRequestBody(tags, regions);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBody,
                null, $"{nameof(LobbiesService)}.{nameof(FetchLobbyStats)}", authClient.SessionToken, response =>
                {
                    var requestResponse = RequestResponse<LobbyStats>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    try
                    {
                        var stats = Utils.CoherenceJson.DeserializeObject<LobbyStats>(response.Result);

                        requestResponse.Result = stats;
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                        logger.Error(Error.RuntimeCloudDeserializationException,
                            ("Request", nameof(FetchLobbyStats)),
                            ("Response", response.Result),
                            ("exception", exception));
                    }
                    finally
                    {
                        onRequestFinished?.Invoke(requestResponse);
                    }
                });
        }

        /// <summary>Get stats for the usage of Lobbies for your current coherence Project.</summary>
        /// <param name="tags">Optional list of tags to filter the fetched stats.</param>
        /// <param name="regions">Optional list of regions to filter the fetched stats.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<LobbyStats> FetchLobbyStatsAsync(List<string> tags = null, List<string> regions = null, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            var pathParams = "/stats";

            var requestBody = StatsRequest.GetRequestBody(tags, regions);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, requestBody, null, $"{nameof(LobbiesService)}.{nameof(FetchLobbyStats)}", authClient.SessionToken);

            try
            {
                var stats = Utils.CoherenceJson.DeserializeObject<LobbyStats>(textResponse);

                return stats;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(FetchLobbyStats)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <summary>Get a LobbySession instance for a Lobby that you have joined and you're a part of.</summary>
        /// <param name="lobbyId">Id of the lobby you want to get the <see cref="LobbySession"/> for.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <exception cref="TaskCanceledException">
        /// Thrown when the operation is canceled using the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <exception cref="ResponseDeserializationException">
        /// Thrown when the response from the server cannot be deserialized into a <see cref="LobbySession"/>.
        /// </exception>
        /// <returns>
        /// A task that represents the asynchronous operation. When the task completes successfully, it will contain
        /// a <see cref="LobbySession"/> if this player account is still in the lobby with the specified <paramref name="lobbyId"/>;
        /// otherwise, it will contain <see langword="null"/>.
        /// </returns>
        public async Task<LobbySession> GetActiveLobbySessionForLobbyId(string lobbyId, CancellationToken cancellationToken = default)
        {
            if (lobbySessions.TryGetValue(lobbyId, out var lobbySession) && !lobbySession.IsDisposed)
            {
                return lobbySession;
            }

            var lobbyData = await RefreshLobbyAsync(lobbyId, cancellationToken);
            return CreateActiveLobbySessionIfPlayerIsInLobby(lobbyData);
        }

        /// <summary>Iterate all active LobbySession instances.</summary>
        /// <remarks>A LobbySession instance is used to interface with a Lobby that you are a part of.</remarks>
        [Obsolete("This method is deprecated and will be removed in a future version. Use GetLobbySessionIds or GetLobbySessionsAsync() instead.")]
        [Deprecated("08/2025", 2, 1, 0, Reason = "Replaced by GetLobbySessionIds and GetLobbySessionsAsync because GetLobbySessions could not provide reliable results after logging out.")]
        public IEnumerable<LobbySession> GetLobbySessions()
        {
            var disposedSessionIds = new List<string>(0);

            foreach (var lobbySession in lobbySessions.Values)
            {
                if (!lobbySession.IsDisposed)
                {
                    yield return lobbySession;
                }
                else
                {
                    disposedSessionIds.Add(lobbySession.LobbyData.Id);
                }
            }

            RemoveSessions(disposedSessionIds);
        }

        /// <summary>
        /// Retrieves identifiers of all <see cref="LobbySession">lobby sessions</see> that this player account is currently a part of.
        /// </summary>
        public IEnumerable<string> GetLobbySessionIds()
        {
            var disposedSessionIds = new List<string>(0);

            foreach (var lobbySessionId in lobbySessionIds)
            {
                if (!lobbySessions.TryGetValue(lobbySessionId, out var lobbySession) || !lobbySession.IsDisposed)
                {
                    yield return lobbySessionId;
                }
                else
                {
                    disposedSessionIds.Add(lobbySessionId);
                }
            }

            RemoveSessions(disposedSessionIds);
        }

        /// <summary>
        /// Retrieves all <see cref="LobbySession">lobby sessions</see> that this player account is currently part of asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. When the task completes successfully, it will contain
        /// a list of <see cref="LobbySession">lobby sessions</see>.
        /// </returns>
        /// <exception cref="TaskCanceledException">
        /// Thrown when the operation is canceled using the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <exception cref="ResponseDeserializationException">
        /// Thrown when the response from the server cannot be deserialized into a <see cref="LobbySession"/>.
        /// </exception>
        /// <example>
        /// <code source="Cloud/LobbiesService/GetLobbySessionsAsync.cs" language="csharp"/>
        /// </example>
        public async Task<IReadOnlyList<LobbySession>> GetLobbySessionsAsync(CancellationToken cancellationToken = default)
        {
            if (lobbySessionIds.Count is 0)
            {
                return Array.Empty<LobbySession>();
            }

            var results = new List<LobbySession>(lobbySessionIds.Count);
            var disposedSessionIds = new List<string>(0);
            foreach (var lobbySessionId in lobbySessionIds)
            {
                if (lobbySessions.TryGetValue(lobbySessionId, out var lobbySession))
                {
                    if (lobbySession.IsDisposed)
                    {
                        disposedSessionIds.Add(lobbySession.LobbyData.Id);
                    }
                    else
                    {
                        results.Add(lobbySession);
                    }
                }
            }
            RemoveSessions(disposedSessionIds);

            if (results.Count == lobbySessionIds.Count)
            {
                return results;
            }

            var fetchDataFor = lobbySessionIds.Except(results.Select(session => session.LobbyData.Id)).ToArray();
            if(fetchDataFor.Length == 0)
            {
                return results;
            }

            var lobbies = await RefreshLobbiesAsync(fetchDataFor, cancellationToken);
            foreach(var lobby in lobbies)
            {
                if (lobbySessions.TryGetValue(lobby.Id, out var lobbySession) && !lobbySession.IsDisposed)
                {
                    results.Add(lobbySession);
                }
            }

            disposedSessionIds.Clear();
            foreach (var result in results)
            {
                if (lobbySessions.TryGetValue(result.LobbyData.Id, out var lobbySession))
                {
                    if (lobbySession.IsDisposed)
                    {
                        disposedSessionIds.Add(lobbySession.LobbyData.Id);
                    }
                }
            }
            RemoveSessions(disposedSessionIds);

            return results;
        }

        public void Dispose()
        {
            foreach (var lobbySession in lobbySessions)
            {
                lobbySession.Value.Dispose();
            }

            ClearSessions();

            authClient.OnLogin -= OnLogin;
            authClient.OnLogout -= OnLogout;

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                CloudCredentialsPair.Dispose(authClient, requestFactory);
            }

            logger?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var lobbySession in lobbySessions)
            {
                lobbySession.Value.Dispose();
            }

            ClearSessions();

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                await CloudCredentialsPair.DisposeAsync(authClient, requestFactory);
            }

            logger?.Dispose();
        }

        private LobbyData DeserializeLobbyData(string response)
        {
            var lobby = Utils.CoherenceJson.DeserializeObject<LobbyData>(response);

            if (!lobby.RoomData.HasValue)
            {
                return lobby;
            }

            var room = lobby.RoomData.Value;
            AddTokenToRoom(ref room);
            lobby.RoomData = room;

            return lobby;
        }

        private LobbyData[] DeserializeLobbiesData(string response)
        {
            LobbyData[] lobbies;
            try
            {
                lobbies = Utils.CoherenceJson.DeserializeObject<LobbiesData>(response).Lobbies ?? Array.Empty<LobbyData>();
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(RefreshLobbiesAsync)),
                    ("Response", response),
                    ("exception", exception));
                throw exception;
            }

            for (var i = 0; i < lobbies.Length; i++)
            {
                var lobby = lobbies[i];
                if (!lobby.RoomData.HasValue)
                {
                    continue;
                }

                var room = lobby.RoomData.Value;
                AddTokenToRoom(ref room);
                lobby.RoomData = room;
                lobbies[i] = lobby;
            }

            return lobbies;
        }

        private List<LobbyData> OnFetch(string text) => Utils.CoherenceJson.DeserializeObject<List<LobbyData>>(text);

        [return: MaybeNull]
        private LobbySession CreateActiveLobbySessionIfPlayerIsInLobby(LobbyData updatedLobby)
        {
            foreach (var player in updatedLobby.Players)
            {
                if (player.Id.Equals(authClient.PlayerAccountId))
                {
                    return AddOrUpdateSession(updatedLobby);
                }
            }

            RemoveSession(updatedLobby.Id);
            return null;
        }

        private bool WaitForOngoingRequest(Action<RequestResponse<IReadOnlyList<LobbyData>>> onRequestFinished)
        {
            fetchLobbiesCallbackList.Add(onRequestFinished);
            return fetchLobbiesCallbackList.Count > 1;
        }

        private void OnPlayStarted(string responseBody)
        {
            PlayCallbackResponse response = default;

            try
            {
                response = Utils.CoherenceJson.DeserializeObject<PlayCallbackResponse>(responseBody);

                AddTokenToRoom(ref response.Room);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(ConnectionClosedResponse)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            if (OnPlaySessionStarted != null)
            {
                OnPlaySessionStarted.Invoke(response.LobbyId, response.Room);
            }
            else
            {
                OnPlaySessionStartedInternal?.Invoke(response.Room);
            }
        }

        private void AddTokenToRoom(ref RoomData room)
        {
            if (runtimeSettings.IsWebGL)
            {
                room.Host.Ip = runtimeSettings.ApiEndpoint;
                room.Host.Port = runtimeSettings.RemoteWebPort;
            }

            room.AuthToken = authClient.SessionToken;
        }

        private LobbySession AddOrUpdateSession(LobbyData lobby)
        {
            var lobbySession = new LobbySession(this, lobby, authClient, requestFactory);
            lobbySessions[lobby.Id] = lobbySession;
            lobbySessionIds.Add(lobby.Id);
            return lobbySession;
        }

        private void RemoveSessions(List<string> lobbyIds)
        {
            foreach (var lobbyId in lobbyIds)
            {
                RemoveSession(lobbyId);
            }
        }

        private void RemoveSession(string lobbyId)
        {
            lobbySessions.Remove(lobbyId);
            lobbySessionIds.Remove(lobbyId);
        }

        private void ClearSessions()
        {
            lobbySessions.Clear();
            lobbySessionIds.Clear();
        }

        private void OnLogin(LoginResponse response)
        {
            ClearSessions();

            if (response.LobbyIds is not null)
            {
                foreach (var lobbyId in response.LobbyIds)
                {
                    lobbySessionIds.Add(lobbyId);
                }
            }
        }

        private void OnLogout() => ClearSessions();
    }
}
