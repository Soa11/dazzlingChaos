// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using Common;
    using NUnit.Framework;

    /// <summary>
    /// Edit Mode unit tests for <see cref="LobbiesService"/>.
    /// </summary>
    public class LobbiesServiceTests : CoherenceTest
    {
        private MockAuthClientBuilder authClientBuilder;
        private MockRequestFactoryBuilder requestFactoryBuilder;
        private MockRuntimeSettingsBuilder runtimeSettingsBuilder;

        private CloudCredentialsPair CloudCredentialsPair => new(AuthClient, RequestFactory);
        private IAuthClientInternal AuthClient => authClientBuilder.AuthClient;
        private IRequestFactoryInternal RequestFactory => requestFactoryBuilder.RequestFactory;
        private IRuntimeSettings RuntimeSettings => runtimeSettingsBuilder.RuntimeSettings;

        public override void SetUp()
        {
            base.SetUp();
            authClientBuilder = new();
            requestFactoryBuilder = new();
            runtimeSettingsBuilder = new();
        }

        [Test]
        public async Task FindOrCreateLobbyAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.FindOrCreateLobbyAsync(new(), new(), cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task CreateLobbyAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.CreateLobbyAsync(new(), cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FindLobbiesAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.FindLobbiesAsync(new(), cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task JoinLobbyAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.JoinLobbyAsync(new LobbyData(), null, null, cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task RefreshLobbyAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.RefreshLobbyAsync("lobbyId", cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FetchLobbyStatsAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.FetchLobbyStatsAsync(null, null, cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FindOrCreateLobbyAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.FindOrCreateLobbyAsync(new(), new());

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task CreateLobbyAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.CreateLobbyAsync(new());

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FindLobbiesAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.FindLobbiesAsync();

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task JoinLobbyAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.JoinLobbyAsync(new LobbyData(), null, null);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task RefreshLobbyAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.RefreshLobbyAsync("lobbyId");

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FetchLobbyStatsAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.FetchLobbyStatsAsync(null, null);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public void GetLobbySessionIds_Returns_LobbyIds_From_LoginResponse()
        {
            using var lobbiesService = CreateLobbiesService();
            var expectedLobbyIds = new List<string> { "lobby-id" };
            var loginResponse = new LoginResponse { LobbyIds = expectedLobbyIds };

            authClientBuilder.RaiseOnLogin(loginResponse);

            Assert.That(lobbiesService.GetLobbySessionIds(), Is.EquivalentTo(expectedLobbyIds));
        }

        [Test]
        public void GetLobbySessionIds_Returns_Empty_Collection_After_Logout()
        {
            using var lobbiesService = CreateLobbiesService();
            var loginResponseLobbyIds = new List<string> { "lobby-id" };
            var loginResponse = new LoginResponse { LobbyIds = loginResponseLobbyIds };
            authClientBuilder.RaiseOnLogin(loginResponse);

            authClientBuilder.RaiseOnLogout();

            Assert.That(lobbiesService.GetLobbySessionIds(), Is.Empty);
        }

        [Test]
        public async Task GetLobbySessionsAsync_Returns_Lobbies_Listed_In_LoginResponse()
        {
            requestFactoryBuilder.SendRequestAsyncReturns((_, pathParams, _, _, _, _, _) =>
            {
                var calledWithId = ExtractId(pathParams);
                return Task.FromResult(CoherenceJson.SerializeObject(new LobbiesData { Lobbies = CreateLobbyData(calledWithId) } ));
                static string ExtractId(string pathParams)
                {
                    var i = LobbiesService.refreshLobbiesAsyncPathParams.IndexOf("{0}", StringComparison.Ordinal);
                    return pathParams.Substring(i, pathParams.Length - (LobbiesService.refreshLobbiesAsyncPathParams.Length - "{0}".Length));
                }
            });
            using var lobbiesService = CreateLobbiesService();

            const string lobbyId = "lobby-id";
            authClientBuilder.RaiseOnLogin(new() { LobbyIds = new() { lobbyId } });

            var lobbySessions = await lobbiesService.GetLobbySessionsAsync();

            var expectedLobbyData = CreateLobbyData(lobbyId);
            Assert.That(lobbySessions.Select(x => x.LobbyData), Is.EquivalentTo(expectedLobbyData));

            LobbyData[] CreateLobbyData(string id) => new LobbyData[] { new() { Id = id, players = new() { new(authClientBuilder.PlayerAccountId, "", new()) }, lobbyAttributes = new() } };
        }

        [Test]
        public async Task GetLobbySessionsAsync_Returns_Empty_Collection_After_Logout()
        {
            using var lobbiesService = CreateLobbiesService();
            var expectedLobbyIds = new List<string> { "lobby-id" };
            var loginResponse = new LoginResponse { LobbyIds = expectedLobbyIds };
            authClientBuilder.RaiseOnLogin(loginResponse);

            authClientBuilder.RaiseOnLogout();

            var sessionIds = await lobbiesService.GetLobbySessionsAsync();
            Assert.That(sessionIds, Is.Empty);
        }

        private LobbiesService CreateLobbiesService(TaskCompletionSource<bool> cooldownCompletionSource = null)
        {
            if (cooldownCompletionSource is null)
            {
                cooldownCompletionSource = new();
                cooldownCompletionSource.SetResult(true);
            }

            return CreateLobbiesService(new FakeRequestThrottle(cooldownCompletionSource));
        }

        private LobbiesService CreateLobbiesService(RequestThrottle throttle)
            => new(CloudCredentialsPair, RuntimeSettings, throttle);
    }
}
