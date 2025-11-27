// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Cloud
{
    using Common;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Log;

    /// <summary>
    /// Helps manage rooms in your Project hosted on the coherence Cloud.
    /// If you wish to create, delete or fetch rooms in a self-hosted Replication Server, you can instantiate
    /// a <see cref="ReplicationServerRoomsService" /> instead.
    /// </summary>
    public class CloudRooms : IAsyncDisposable, IDisposable
    {
        private IRuntimeSettings runtimeSettings;
        private IAuthClientInternal authClient;
        private IRequestFactoryInternal requestFactory;
        private RegionsService regionsService;
        private LobbiesService lobbyService;
        private Dictionary<string, CloudRoomsService> roomServices = new();

        private bool shouldDisposeRequestFactoryAndAuthClient;

        #pragma warning disable CS0618
        /// <summary>
        ///     Get the current cached regions that were fetched via the RefreshRegions method.
        /// </summary>
        [Obsolete("This property is deprecated and will be removed in a future version. Use " + nameof(CloudService) + "." + nameof(CloudService.Regions) + "." + nameof(Cloud.RegionsService.Regions) + " instead.")]
        [Deprecated("09/09/2025", 1, 8, 0, Reason = "Replaced by RegionsService.Regions.")]
        #pragma warning restore CS0618
        public IReadOnlyList<string> Regions => regionsService.RegionsLegacy;

        /// <summary>
        /// Same as <see cref="CloudService.Lobbies"/>, accessible from here as well for backwards compatibility.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public LobbiesService LobbyService => lobbyService;

        internal RegionsService RegionsService => regionsService;

        /// <summary>
        ///     Returns true when the Web Socket is connected.
        /// </summary>
        public bool IsConnectedToCloud => requestFactory.IsReady;

        internal IEnumerable<CloudRoomsService> RoomServices => roomServices.Values;

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

        internal CloudRooms() { } // for test doubles

        public CloudRooms(CloudCredentialsPair credentialsPair = null, IRuntimeSettings runtimeSettings = null) : this(credentialsPair, runtimeSettings, null) { }

        internal CloudRooms(CloudCredentialsPair credentialsPair = null, IRuntimeSettings runtimeSettings = null, IPlayerAccountProvider playerAccountProvider = null)
        {
#if UNITY
            runtimeSettings ??= RuntimeSettings.Instance;
#endif
            this.runtimeSettings = runtimeSettings;
            if (credentialsPair is null)
            {
                shouldDisposeRequestFactoryAndAuthClient = true;
                credentialsPair = CloudCredentialsFactory.ForClient(runtimeSettings, playerAccountProvider:playerAccountProvider);
                credentialsPair.authClient.LoginAsGuest().Then(task => Log.GetLogger<CloudRooms>().Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception.ToString()), TaskContinuationOptions.OnlyOnFaulted);
            }

            this.requestFactory = credentialsPair.requestFactory;
            this.authClient = credentialsPair.authClient;

            regionsService = new(this.requestFactory, this.authClient);
            lobbyService = new(credentialsPair, this.runtimeSettings);
        }

        /// <summary>Get an instance of CloudRoomsService for a given region from the CloudRoomsService dictionary.</summary>
        /// <param name="region">region of the CloudRoomsService that you want the service of.</param>
        /// <returns>Returns an instance of CloudRoomsService for a given region.</returns>
        public CloudRoomsService GetRoomServiceForRegion(string region)
        {
            if (roomServices.TryGetValue(region, out var roomsService))
            {
                return roomsService;
            }

            var roomService = new CloudRoomsService(region, new CloudCredentialsPair(authClient, requestFactory), runtimeSettings);
            roomServices.Add(region, roomService);

            return roomService;
        }

        /// <summary>Get all the available regions for Rooms. You can enable or disable regions in the Online Dashboard, in the Rooms section.</summary>
        /// <param name="callback">Callback that will be called when regions have been fetched, it includes the list of regions.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token to cancel the request.</param>
        [Obsolete("This method is deprecated and will be removed in a future version. Use " + nameof(CloudService) + "." + nameof(CloudService.Regions) + "." + nameof(Cloud.RegionsService.FetchRegions) + " instead.")]
        [Deprecated("09/09/2025", 2, 0, 0, Reason = "Replaced by RegionsService.FetchRegions.")]
        public void RefreshRegions(Action<RequestResponse<IReadOnlyList<string>>> callback, CancellationToken cancellationToken = default)
            => regionsService.FetchRegionsLegacy(callback, cancellationToken);

        /// <summary>Get all the available regions for Rooms asynchronously. You can enable or disable regions in the Online Dashboard, in the Rooms section.</summary>
        /// <param name="cancellationToken">(Optional) Cancellation token to cancel the request.</param>
        [Obsolete("This method is deprecated and will be removed in a future version. Use " + nameof(CloudService) + "." + nameof(CloudService.Regions) + "." + nameof(Cloud.RegionsService.FetchRegionsAsync) + " instead.")]
        [Deprecated("09/09/2025", 2, 0, 0, Reason = "Replaced by RegionsService.FetchRegionsAsync.")]
        public async Task<IReadOnlyList<string>> RefreshRegionsAsync(CancellationToken cancellationToken = default)
            => await regionsService.FetchRegionsLegacyAsync(cancellationToken);

        public void Dispose()
        {
            lobbyService?.Dispose();

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                CloudCredentialsPair.Dispose(authClient, requestFactory);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await lobbyService.DisposeAsync();

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                await CloudCredentialsPair.DisposeAsync(authClient, requestFactory);
            }
        }
    }
}
