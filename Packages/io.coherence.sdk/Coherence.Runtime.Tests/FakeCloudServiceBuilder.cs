// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using Coherence.Cloud;
    using Common;
    using Moq;

    /// <summary>
    /// Can be used to <see cref="Build"/> a fake <see cref="CloudService"/> object for use in a test.
    /// </summary>
    internal sealed class FakeCloudServiceBuilder : IDisposable
    {
        private CloudCredentialsPair cloudCredentialsPair;
        private CloudService cloudService;
        private bool cloudServiceCanBeNull;
        private MockPlayerAccountProviderBuilder playerAccountProviderBuilder;
        private bool shouldMockAuthClient = true;
        private IAuthClientInternal authClient;
        private MockAuthClientBuilder authClientBuilder;
        private bool autoLoginAsGuest;
        private bool buildExecuted;

        public MockAuthClientBuilder AuthClientBuilder => authClientBuilder ??= new();
        public MockRequestFactoryBuilder RequestFactoryBuilder { get; } = new();
        public MockRuntimeSettingsBuilder RuntimeSettingsBuilder { get; } = new();
        public MockPlayerAccountProviderBuilder PlayerAccountProviderBuilder => playerAccountProviderBuilder ??= new MockPlayerAccountProviderBuilder().SetServicesBuilder(this);

        public IAuthClientInternal AuthClient
        {
            get
            {
                if (authClient is not null)
                {
                    return authClient;
                }

                if (shouldMockAuthClient)
                {
                    return authClient = AuthClientBuilder.AuthClient;
                }

                authClient = Coherence.Cloud.AuthClient.ForPlayer(RequestFactory, PlayerAccountProvider);
                return authClient;
            }
        }

        public IRequestFactoryInternal RequestFactory => RequestFactoryBuilder.RequestFactory;
        public IRuntimeSettings RuntimeSettings => RuntimeSettingsBuilder.RuntimeSettings;
        public IPlayerAccountProvider PlayerAccountProvider => PlayerAccountProviderBuilder.PlayerAccountProvider;

        public CloudCredentialsPair CloudCredentialsPair => cloudCredentialsPair ??= new(AuthClient, RequestFactory);

        public CloudService CloudService
        {
            get => Build();

            set
            {
                if (buildExecuted)
                {
                    Dispose();
                }

                buildExecuted = true;
                cloudService = value;
                if (value is null)
                {
                    cloudServiceCanBeNull = true;
                }
            }
        }

        public FakeCloudServiceBuilder SetShouldMockAuthClient(bool shouldMockAuthClient)
        {
            this.shouldMockAuthClient = shouldMockAuthClient;
            return this;
        }

        public FakeCloudServiceBuilder SetAuthClient(IAuthClientInternal authClient)
        {
            this.authClient = authClient;
            this.shouldMockAuthClient = authClient is null;
            return this;
        }

        public FakeCloudServiceBuilder SetUniqueId(CloudUniqueId uniqueId)
        {
            PlayerAccountProviderBuilder.SetUniqueId(uniqueId);
            return this;
        }

        public FakeCloudServiceBuilder SetProjectId(string projectId)
        {
            PlayerAccountProviderBuilder.SetProjectId(projectId);
            RuntimeSettingsBuilder.SetProjectID(projectId);
            return this;
        }

        public FakeCloudServiceBuilder SetupRuntimeSettings(Action<MockRuntimeSettingsBuilder> setupRuntimeSettingsBuilder)
        {
            setupRuntimeSettingsBuilder(RuntimeSettingsBuilder);
            return this;
        }

        public FakeCloudServiceBuilder SetupAuthClient(Action<MockAuthClientBuilder> setupAuthClientBuilder)
        {
            setupAuthClientBuilder(AuthClientBuilder);
            return this;
        }

        public FakeCloudServiceBuilder SetupRequestFactory(Action<MockRequestFactoryBuilder> setupRequestFactoryBuilder)
        {
            setupRequestFactoryBuilder(RequestFactoryBuilder);
            return this;
        }

        public FakeCloudServiceBuilder SetupPlayerAccountProvider(Action<MockPlayerAccountProviderBuilder> setupPlayerAccountProviderBuilder)
        {
            setupPlayerAccountProviderBuilder(PlayerAccountProviderBuilder);
            return this;
        }

        public FakeCloudServiceBuilder SetAutoLoginAsGuest(bool autoLoginAsGuest)
        {
            if (autoLoginAsGuest)
            {
                shouldMockAuthClient = false;
            }

            this.autoLoginAsGuest = autoLoginAsGuest;
            return this;
        }

        public CloudService Build()
        {
            if (buildExecuted)
            {
                if (cloudService is null && !cloudServiceCanBeNull)
                {
                    throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
                }

                return cloudService;
            }

            buildExecuted = true;
            var rooms = new CloudRooms(CloudCredentialsPair, RuntimeSettings, PlayerAccountProvider);
            var worlds = new Mock<WorldsService>().Object;
            var analyticsClient = new Mock<AnalyticsClient>().Object;
            var gameServers = new Mock<GameServersService>().Object;
            var gameServices = new GameServices(CloudCredentialsPair);
            cloudService = new(CloudCredentialsPair, RuntimeSettings, PlayerAccountProvider, gameServices, rooms, worlds, analyticsClient, gameServers);

            if (autoLoginAsGuest)
            {
                AuthClient.LoginAsGuest();
            }

            return cloudService;
        }

        public void Dispose()
        {
            // Calling cloudService.Dispose() will result in Dispose also being called on all its dependencies
            // (GameServices, Rooms, Worlds, AuthClient, RequestFactory, PlayerAccountProvider), so there's no need to
            // dispose them separately.
            cloudService?.Dispose();

            if (!shouldMockAuthClient && authClient is IDisposable disposableAuthClient)
            {
                authClient = null;
                disposableAuthClient.Dispose();
            }

            cloudService = null;
            buildExecuted = false;
        }
    }
}
