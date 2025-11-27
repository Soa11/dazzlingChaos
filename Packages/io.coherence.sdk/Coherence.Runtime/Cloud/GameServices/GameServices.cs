// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public class GameServices : IDisposable
    {
        /// <summary>
        /// The old service used for logging in to coherence Cloud. Superseded by <see cref="CoherenceCloud"/>.
        /// </summary>
        public IAuthClient AuthService { get; }

        /// <remarks>
        /// The old service for grouping players into teams. Superseded by <see cref="CloudService.Lobbies"/>.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use CloudService.Lobbies instead.")]
        [Deprecated("08/2025", 2, 1, 0, Reason = "Superseded by " + nameof(LobbiesService) + ".")]
        public MatchmakerClient MatchmakerService { get; }

        /// <remarks>
        /// <see cref="CloudService.CloudStorage"/> can be used instead.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use CloudService.CloudStorage instead.")]
        [Deprecated("08/2025", 2, 1, 0, Reason = "Migrated to " + nameof(CloudService) + ".")]
        public CloudStorage CloudStorage { get; }

        /// <summary>
        /// The old cloud-backed key-value store service. Superseded by <see cref="CloudService.CloudStorage"/>.
        /// </summary>
        public KvStoreClient KvStoreService { get; }

        internal readonly IAuthClientInternal authService;

        internal GameServices() { } // for test doubles

        public GameServices(CloudCredentialsPair credentialsPair)
        {
            AuthService = credentialsPair.AuthClient;
            authService = credentialsPair.authClient;
#pragma warning disable CS0618 // Type or member is obsolete
            MatchmakerService = new( credentialsPair.RequestFactory, authService);
            CloudStorage = new(credentialsPair.RequestFactory, authService, credentialsPair.requestFactory.Throttle, cloudStorage => new(cloudStorage));
            KvStoreService = new( credentialsPair.RequestFactory, authService);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public void Dispose()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            KvStoreService?.Dispose();
            ((IDisposable)CloudStorage)?.Dispose();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        internal async ValueTask DisposeAsync(bool waitForOngoingOperationsToFinish)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            KvStoreService.Dispose();
            await CloudStorage.DisposeAsync(waitForOngoingOperationsToFinish);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
