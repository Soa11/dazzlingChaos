// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Cloud
{
    using Common;
    using Log;
    using Runtime;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Logger = Log.Logger;

    public class WorldsService : IAsyncDisposable, IDisposable
    {
        private IRequestFactory requestFactory;
        internal IAuthClientInternal authClient;

        private readonly string worldsResolveEndpoint = "/worlds";

        private readonly IRuntimeSettings runtimeSettings;
        private readonly Logger logger = Log.GetLogger<WorldsService>();

        private bool isFetchingWorlds;
        private bool shouldDisposeRequestFactoryAndAuthClient;

        /// <summary>
        ///     Returns true when the Web Socket is connected and when we are logged in to coherence Cloud.
        /// </summary>
        public bool IsLoggedIn
        {
            get
            {
                var requestFactoryReady = requestFactory.IsReady;

                var authClientReady = authClient.LoggedIn;

                return requestFactoryReady && authClientReady;
            }
        }

        internal WorldsService() { } // for test doubles

        public WorldsService(CloudCredentialsPair credentialsPair, IRuntimeSettings runtimeSettings) : this(credentialsPair, runtimeSettings, null) { }

        internal WorldsService([MaybeNull] CloudCredentialsPair credentialsPair, [MaybeNull] IRuntimeSettings runtimeSettings, [MaybeNull] IPlayerAccountProvider playerAccountProvider)
        {
#if UNITY
            runtimeSettings ??= RuntimeSettings.Instance;
#endif
            this.runtimeSettings = runtimeSettings;

            if (credentialsPair is null)
            {
                shouldDisposeRequestFactoryAndAuthClient = true;
                credentialsPair = CloudCredentialsFactory.ForClient(runtimeSettings, playerAccountProvider);
                credentialsPair.authClient.LoginAsGuest().Then(task => logger.Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception.ToString()), TaskContinuationOptions.OnlyOnFaulted);
            }

            this.requestFactory = credentialsPair.RequestFactory;
            this.authClient = credentialsPair.authClient;
        }

        /// <returns>Returns the internal cooldown for the Fetch Worlds endpoint.</returns>
        public TimeSpan GetFetchWorldsCooldown()
        {
            return requestFactory.GetRequestCooldown(worldsResolveEndpoint, "GET");
        }

        /// <summary>Get the list of available/online worlds that you have created in the Online Dashboard.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished, with the list of worlds.</param>
        /// <param name="region">Filter the available worlds by a specific region.</param>
        /// <param name="simSlug">Filter the available worlds by a specific Simulator slug.</param>
        public void FetchWorlds(Action<RequestResponse<IReadOnlyList<WorldData>>> onRequestFinished, string region = "", string simSlug = "")
            => FetchWorldsAsync(region, simSlug).Then(task =>
            {
                if (!task.IsCompletedSuccessfully)
                {
                    onRequestFinished?.Invoke(new()
                    {
                        Status = RequestStatus.Fail,
                        Exception = task.IsFaulted ? (task.Exception?.InnerExceptions.Count is 1 ? task.Exception.InnerException : task.Exception) : new OperationCanceledException(),
                        Result = Array.Empty<WorldData>()
                    });
                    return;
                }

                onRequestFinished?.Invoke(new()
                {
                    Status = RequestStatus.Success,
                    Result = task.Result
                });
            });

        /// <summary>Get the list of available/online worlds that you have created in the Online Dashboard asynchronously.</summary>
        /// <param name="region">Filter the available worlds by a specific region.</param>
        /// <param name="simSlug">Filter the available worlds by a specific Simulator slug.</param>
        public async Task<IReadOnlyList<WorldData>> FetchWorldsAsync(string region = "", string simSlug = "")
        {
            while (isFetchingWorlds)
            {
                await Task.Yield();
            }

            isFetchingWorlds = true;

            var filters = new List<string>
            {
                $"rs_version={Uri.EscapeDataString(runtimeSettings.RsVersion)}",
                $"schema_id={Uri.EscapeDataString(runtimeSettings.SchemaID)}",
                $"region={Uri.EscapeDataString(region)}",
                $"sim_slug={Uri.EscapeDataString(simSlug)}"
            };

            var pathParams = $"?{String.Join("&", filters)}";

            var response = await requestFactory.SendRequestAsync(worldsResolveEndpoint, pathParams, "GET", string.Empty, null, $"{nameof(WorldsService)}.{nameof(FetchWorldsAsync)}", authClient.SessionToken);

            try
            {
                var worldList = Utils.CoherenceJson.DeserializeObject<WorldData[]>(response) ?? Array.Empty<WorldData>();
                PostProcessWorldData(worldList);
                return worldList;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(FetchWorldsAsync)),
                    ("Response", response),
                    ("Exception", exception));

                throw new WorldsResolverException(Result.InvalidResponse, exception.Message);
            }
            finally
            {
                isFetchingWorlds = false;
            }

            void PostProcessWorldData(WorldData[] worldList)
            {
                for (var i = 0; i < worldList.Length; i++)
                {
                    if (runtimeSettings.IsWebGL)
                    {
                        worldList[i].Host.Ip = runtimeSettings.ApiEndpoint;
                        worldList[i].Host.SigPort = runtimeSettings.RemoteWebPort;
                    }

                    worldList[i].AuthToken = authClient.SessionToken;
                }
            }
        }

        public void Dispose()
        {
            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                CloudCredentialsPair.Dispose(authClient, requestFactory);
            }

            logger?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                await CloudCredentialsPair.DisposeAsync(authClient, requestFactory);
            }

            logger?.Dispose();
        }
    }
}
