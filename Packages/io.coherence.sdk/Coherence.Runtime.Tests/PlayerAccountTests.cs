// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using Log;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using Utils;

    /// <summary>
    /// Unit tests for <see cref="PlayerAccount"/>.
    /// </summary>
    public sealed class PlayerAccountTests : CoherenceTest
    {
        private const string ProjectId = "ProjectId";
        private static CloudUniqueId UniqueId => new("CloudUniqueId");

        [TestCase(true), TestCase(false)]
        public async Task GetMainAsync_Can_Be_Canceled(bool waitUntilLoggedIn)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancelledOperation = PlayerAccount.GetMainAsync(waitUntilLoggedIn, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();
            await cancelledOperation;
            Assert.That(cancelledOperation.IsCanceled, Is.True);
        }

        [Test]
        public void Main_Defaults_To_First_Registered_PlayerAccount()
        {
            using var playerAccountBuilder1 = CreateFakePlayerAccount(out var playerAccount1, uniqueId: "UniqueId1", authClientType: AuthClientType.RealAutoLoginAsGuest);
            PlayerAccount.Register(playerAccount1);
            using var playerAccountBuilder2 = CreateFakePlayerAccount(out var playerAccount2, uniqueId: "UniqueId2", authClientType: AuthClientType.MockLoggedIn);
            PlayerAccount.Register(playerAccount2);
            Assert.That(PlayerAccount.Main, Is.EqualTo(playerAccount1));
            PlayerAccount.Unregister(playerAccount1);
            Assert.That(PlayerAccount.Main, Is.EqualTo(playerAccount2));
            PlayerAccount.Unregister(playerAccount2);
            Assert.That(PlayerAccount.Main, Is.Null);
        }

        [Test]
        public void SetAsMain_Sets_PlayerAccount_As_Main_PlayerAccount()
        {
            using var playerAccountBuilder1 = CreateFakePlayerAccount(out var playerAccount1, uniqueId: "UniqueId1", authClientType: AuthClientType.MockLoggedIn);
            using var playerAccountBuilder2 = CreateFakePlayerAccount(out var playerAccount2, uniqueId: "UniqueId2", authClientType: AuthClientType.MockLoggedIn);

            playerAccount1.SetAsMain();
            Assert.That(playerAccount1.IsMain, Is.True);
            Assert.That(playerAccount2.IsMain, Is.False);

            playerAccount2.SetAsMain();
            Assert.That(playerAccount1.IsMain, Is.False);
            Assert.That(playerAccount2.IsMain, Is.True);
        }

        [Test]
        public void Register_Adds_PlayerAccount_To_All()
        {
            using var playerAccount = new PlayerAccount(LoginInfo.ForGuest(ProjectId, UniqueId, false), UniqueId, ProjectId, null);
            PlayerAccount.Register(playerAccount);
            Assert.That(PlayerAccount.All, Is.EquivalentTo(new[] { playerAccount }));
            PlayerAccount.Unregister(playerAccount);
        }

        [Test]
        public void Unregister_Removes_PlayerAccount_From_All()
        {
            using var playerAccount = new PlayerAccount(LoginInfo.ForGuest(ProjectId, UniqueId, false), UniqueId, ProjectId, null);
            PlayerAccount.Register(playerAccount);
            PlayerAccount.Unregister(playerAccount);
            Assert.That(PlayerAccount.All, Has.Length.Zero);
        }

        [Test]
        public async Task GetInfo_Request_Contains_Expected_Info()
        {
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.GetInfo();

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var expected = IPlayerAccountOperationRequest.GetAccountInfo();
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.pathParams, Is.EqualTo(expected.PathParams));
        }

        [TestCase(true), TestCase(false)]
        public async Task LinkSteam_Request_Contains_Provided_Info(bool force)
        {
            const string ticket = "ticket";
            const string identity = "identity";
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.LinkSteam(ticket, identity, force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var request = CoherenceJson.DeserializeObject<LinkSteamRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            var expected = IPlayerAccountOperationRequest.LinkSteam(ticket, identity, force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(request.Ticket, Is.EqualTo(ticket));
            Assert.That(request.Identity, Is.EqualTo(identity));
            Assert.That(request.Force, Is.EqualTo(force));
        }

        [TestCase(true), TestCase(false)]
        public async Task LinkEpicGames_Request_Contains_Provided_Info(bool force)
        {
            const string token = "token";
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.LinkEpicGames(token, force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var request = CoherenceJson.DeserializeObject<LinkEpicGamesRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            var expected = IPlayerAccountOperationRequest.LinkEpicGames(token, force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(request.Token, Is.EqualTo(token));
            Assert.That(request.Force, Is.EqualTo(force));
        }

        [TestCase(true), TestCase(false)]
        public async Task UnlinkSteam_Request_Contains_Expected_Info(bool force)
        {
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.UnlinkSteam(force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var expected = IPlayerAccountOperationRequest.UnlinkSteam(force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.pathParams, Is.EqualTo(expected.PathParams));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.body, Is.Null.Or.Empty);
        }

        [TestCase(true), TestCase(false)]
        public async Task LinkPlayStation_Request_Contains_Provided_Info(bool force)
        {
            const string token = "token";
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.LinkPlayStation(token, force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var request = CoherenceJson.DeserializeObject<LinkPlayStationRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            var expected = IPlayerAccountOperationRequest.LinkPlayStation(token, force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(request.Token, Is.EqualTo(token));
            Assert.That(request.Force, Is.EqualTo(force));
        }

        [TestCase(true), TestCase(false)]
        public async Task UnlinkPlayStation_Request_Contains_Expected_Info(bool force)
        {
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.UnlinkPlayStation(force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var expected = IPlayerAccountOperationRequest.UnlinkPlayStation(force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.pathParams, Is.EqualTo(expected.PathParams));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.body, Is.Null.Or.Empty);
        }

        [TestCase(true), TestCase(false)]
        public async Task LinkXbox_Request_Contains_Provided_Info(bool force)
        {
            const string token = "token";
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.LinkXbox(token, force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var request = CoherenceJson.DeserializeObject<LinkXboxRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            var expected = IPlayerAccountOperationRequest.LinkXbox(token, force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(request.Token, Is.EqualTo(token));
            Assert.That(request.Force, Is.EqualTo(force));
        }

        [TestCase(true), TestCase(false)]
        public async Task UnlinkXbox_Request_Contains_Expected_Info(bool force)
        {
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.UnlinkXbox(force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var expected = IPlayerAccountOperationRequest.UnlinkXbox(force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.pathParams, Is.EqualTo(expected.PathParams));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.body, Is.Null.Or.Empty);
        }

        [TestCase(true), TestCase(false)]
        public async Task LinkNintendo_Request_Contains_Provided_Info(bool force)
        {
            const string token = "token";
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.LinkNintendo(token, force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var request = CoherenceJson.DeserializeObject<LinkNintendoRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            var expected = IPlayerAccountOperationRequest.LinkNintendo(token, force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(request.Token, Is.EqualTo(token));
            Assert.That(request.Force, Is.EqualTo(force));
        }

        [TestCase(true), TestCase(false)]
        public async Task UnlinkNintendo_Request_Contains_Expected_Info(bool force)
        {
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.UnlinkNintendo(force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var expected = IPlayerAccountOperationRequest.UnlinkNintendo(force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.pathParams, Is.EqualTo(expected.PathParams));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.body, Is.Null.Or.Empty);

        }

        [TestCase(true), TestCase(false)]
        public async Task LinkJwt_Request_Contains_Provided_Info(bool force)
        {
            const string token = "token";
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.LinkJwt(token, force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var request = CoherenceJson.DeserializeObject<LinkJwtRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            var expected = IPlayerAccountOperationRequest.LinkJwt(token, force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(request.Token, Is.EqualTo(token));
            Assert.That(request.Force, Is.EqualTo(force));
        }

        [TestCase(true), TestCase(false)]
        public async Task UnlinkJwt_Request_Contains_Expected_Info(bool force)
        {
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.UnlinkJwt(force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var expected = IPlayerAccountOperationRequest.UnlinkJwt(force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.pathParams, Is.EqualTo(expected.PathParams));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.body, Is.Null.Or.Empty);
        }

        [TestCase(true), TestCase(false)]
        public async Task LinkGuest_Request_Contains_Provided_Info(bool force)
        {
            const string guestId = "guestId";
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);

            await playerAccount.LinkGuest(guestId, force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var request = CoherenceJson.DeserializeObject<LinkGuestRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            var expected = IPlayerAccountOperationRequest.LinkGuest(guestId, force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(request.GuestId, Is.EqualTo(guestId));
            Assert.That(request.Force, Is.EqualTo(force));
        }

        [TestCase(true), TestCase(false)]
        public async Task UnlinkGuest_Request_Contains_Expected_Info(bool force)
        {
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);
            var guestId = playerAccount.GuestId;

            await playerAccount.UnlinkGuest(force);

            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var expected = IPlayerAccountOperationRequest.UnlinkGuest(guestId, force);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(expected.BasePath));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.method, Is.EqualTo(expected.Method));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.pathParams, Is.EqualTo(expected.PathParams));
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.body, Is.Null.Or.Empty);
        }

        [Test]
        public async Task LoginAsGuest_Handles_LegacyLoginData_Properly()
        {
            const string username = "username";
            const string password = "password";
            GuestId.Delete(ProjectId, UniqueId);
            LegacyLoginData.SetCredentials(ProjectId, UniqueId, username, password);
            using var playerAccountBuilder = CreateFakePlayerAccount(out _, authClientType: AuthClientType.RealNotLoggedIn);
            var authClient = playerAccountBuilder.CloudServiceBuilder.AuthClient;
            var requestFactory = playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder;
            var expectedGuestId = GuestId.FromLegacyLoginData(username, password);

            var loginOperation = authClient.LoginAsGuest();

            // First should send LoginRequest containing legacy guest username and password
            var firstRequest = CoherenceJson.DeserializeObject<PasswordLoginRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(LoginType.LegacyGuest.GetBasePath()));
            Assert.That(firstRequest.Username, Is.EqualTo(username));
            Assert.That(firstRequest.Password, Is.EqualTo(password));

            await loginOperation;

            // Eventually should send LoginRequest containing guest id generated based on legacy login data and auto-signup set to true
            var secondRequest = CoherenceJson.DeserializeObject<LinkGuestRequest>(requestFactory.SendRequestAsyncWasCalledWith.body);
            Assert.That(requestFactory.SendRequestAsyncWasCalledWith.basePath, Is.EqualTo(IPlayerAccountOperationRequest.LinkGuest(expectedGuestId, true).BasePath));
            Assert.That(secondRequest.GuestId, Is.EqualTo(expectedGuestId.ToString()));
            Assert.That(secondRequest.Force, Is.True);

            var returnedPlayerAccount = loginOperation.Result.PlayerAccount;
            Assert.That(returnedPlayerAccount.IsGuest, Is.True);

            var loginInfos = returnedPlayerAccount.LoginInfos;
            Assert.That(loginInfos.Count, Is.EqualTo(2));

            var legacyGuestLoginInfo = loginInfos.Single(x => x.LoginType == LoginType.LegacyGuest);
            Assert.That(legacyGuestLoginInfo.LoginType, Is.EqualTo(LoginType.LegacyGuest));
            Assert.That(legacyGuestLoginInfo.Username, Is.EqualTo(username));
            Assert.That(legacyGuestLoginInfo.Password, Is.EqualTo(password));

            var guestLoginInfo = loginInfos.Single(x => x.LoginType == LoginType.Guest);
            Assert.That(returnedPlayerAccount.IsGuest, Is.True);
            Assert.That(guestLoginInfo.LoginType, Is.EqualTo(LoginType.Guest));
            Assert.That(guestLoginInfo.GuestId, Is.EqualTo(expectedGuestId));
        }

        [Test]
        public async Task LinkGuest_Fails_If_Not_Logged_In()
        {
            const string guestId = "guestId";
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealNotLoggedIn);
            playerAccountBuilder.CloudServiceBuilder.AuthClient.Logout();

            var loginOperation = await playerAccount.LinkGuest(guestId);

            Assert.That(loginOperation.HasFailed);
            Assert.That(loginOperation.Error, Is.Not.Null);
            Assert.That(loginOperation.Error.Type, Is.EqualTo(PlayerAccountErrorType.NotLoggedIn));
        }

        [Test]
        public async Task LinkGuest_Fails_Gracefully_If_Request_Factory_Throws_Exception()
        {
            var expectedException = new RequestException(HttpStatusCode.NotFound, "Not Found");
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.RealAutoLoginAsGuest);
            playerAccountBuilder.CloudServiceBuilder.RequestFactoryBuilder.OnSendRequestAsyncCalled(expectedException);

            var loginOperation = playerAccount.LinkGuest(null);
            await Task.WhenAny(loginOperation, Watchdog());

            Assert.That(loginOperation.HasFailed);
            Assert.That(loginOperation.Error, Is.Not.Null);

            async Task Watchdog()
            {
                await TimeSpan.FromSeconds(1f);
                if (!loginOperation.IsCompleted)
                {
                    throw new("LoginOperation did not fail as expected.");
                }
            }
        }

        [Test]
        public void OnMainChanged_Exception_Is_Caught_And_Logged()
        {
            using var playerAccount = new PlayerAccount(LoginInfo.ForGuest(ProjectId, UniqueId, false), UniqueId, ProjectId, null);
            const string exceptionMessage = "Test exception";
            void ThrowException(PlayerAccount _) => throw new(exceptionMessage);
            PlayerAccount.OnMainChanged += ThrowException;

            Exception caughtException = null;
            try
            {
                PlayerAccount.Register(playerAccount);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
            finally
            {
                PlayerAccount.OnMainChanged -= ThrowException;
                PlayerAccount.Unregister(playerAccount);
            }

            LogAssert.Expect(LogType.Error, new Regex(".*" + Regex.Escape(exceptionMessage) + ".*"));
            Assert.That(caughtException, Is.Null, "Exception should be caught internally and not propagate.");
        }

        [Test]
        public void OnMainLoggedIn_Exception_Is_Caught_And_Logged()
        {
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.MockLoggedIn);
            const string exceptionMessage = "Test exception";
            void ThrowException(PlayerAccount _) => throw new(exceptionMessage);
            PlayerAccount.OnMainLoggedIn += ThrowException;

            Exception caughtException = null;
            try
            {
                PlayerAccount.Register(playerAccount);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
            finally
            {
                PlayerAccount.OnMainLoggedIn -= ThrowException;
                PlayerAccount.Unregister(playerAccount);
            }

            LogAssert.Expect(LogType.Error, new Regex(".*" + Regex.Escape(exceptionMessage) + ".*"));
            Assert.That(caughtException, Is.Null, "Exception should be caught internally and not propagate.");
        }

        [TestCase(TaskStatus.Canceled, TaskStatus.Canceled, Result.Success, true)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Faulted, Result.Success, true)]
        [TestCase(TaskStatus.Canceled, TaskStatus.RanToCompletion, Result.Success, true)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Created, Result.Success, true)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Canceled, Result.Success, false)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Faulted, Result.Success, false)]
        [TestCase(TaskStatus.Canceled, TaskStatus.RanToCompletion, Result.Success, false)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Created, Result.Success, false)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Canceled, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Faulted, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Canceled, TaskStatus.RanToCompletion, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Created, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Canceled, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Faulted, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Canceled, TaskStatus.RanToCompletion, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Canceled, TaskStatus.Created, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Canceled, Result.Success, true)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Faulted, Result.Success, true)]
        [TestCase(TaskStatus.Faulted, TaskStatus.RanToCompletion, Result.Success, true)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Created, Result.Success, true)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Canceled, Result.Success, false)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Faulted, Result.Success, false)]
        [TestCase(TaskStatus.Faulted, TaskStatus.RanToCompletion, Result.Success, false)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Created, Result.Success, false)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Canceled, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Faulted, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Faulted, TaskStatus.RanToCompletion, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Created, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Canceled, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Faulted, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Faulted, TaskStatus.RanToCompletion, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Faulted, TaskStatus.Created, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Canceled, Result.Success, true)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Faulted, Result.Success, true)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.RanToCompletion, Result.Success, true)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Created, Result.Success, true)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Canceled, Result.Success, false)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Faulted, Result.Success, false)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.RanToCompletion, Result.Success, false)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Created, Result.Success, false)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Canceled, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Faulted, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.RanToCompletion, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Created, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Canceled, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Faulted, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.RanToCompletion, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.RanToCompletion, TaskStatus.Created, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Created, TaskStatus.Canceled, Result.Success, true)]
        [TestCase(TaskStatus.Created, TaskStatus.Faulted, Result.Success, true)]
        [TestCase(TaskStatus.Created, TaskStatus.RanToCompletion, Result.Success, true)]
        [TestCase(TaskStatus.Created, TaskStatus.Created, Result.Success, true)]
        [TestCase(TaskStatus.Created, TaskStatus.Canceled, Result.Success, false)]
        [TestCase(TaskStatus.Created, TaskStatus.Faulted, Result.Success, false)]
        [TestCase(TaskStatus.Created, TaskStatus.RanToCompletion, Result.Success, false)]
        [TestCase(TaskStatus.Created, TaskStatus.Created, Result.Success, false)]
        [TestCase(TaskStatus.Created, TaskStatus.Canceled, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Created, TaskStatus.Faulted, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Created, TaskStatus.RanToCompletion, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Created, TaskStatus.Created, Result.AlreadyLoggedIn, true)]
        [TestCase(TaskStatus.Created, TaskStatus.Canceled, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Created, TaskStatus.Faulted, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Created, TaskStatus.RanToCompletion, Result.AlreadyLoggedIn, false)]
        [TestCase(TaskStatus.Created, TaskStatus.Created, Result.AlreadyLoggedIn, false)]
        public void OnLoginAttemptCompleted_Raises_OnLoggingInFailed_Once_If_Logging_In_Does_Not_Complete_Successfully(TaskStatus playerAccountTaskStatus, TaskStatus loginTaskStatus, Result loginResultType, bool isCancellationRequested)
        {
            var onLoggingInFailedRaisedTimes = 0;
            CoherenceCloud.OnLoggingInFailed += OnLoggingInFailed;
            using var playerAccountBuilder = CreateFakePlayerAccount(out var playerAccount, authClientType: AuthClientType.MockNotLoggedIn);
            try
            {
                var cancellationToken = CreateCancellationToken();
                var playerAccountTaskCompletionSource = CreateTaskCompletionSource(playerAccountTaskStatus, playerAccount);
                var loginResult = loginResultType is Result.Success ? LoginResult.Success(playerAccount, new()) : LoginResult.Failure(loginResultType, new(ErrorType.AlreadyLoggedIn, LoginErrorType.AlreadyLoggedIn, Error.RuntimeAlreadyLoggedIn));
                var loginResultTaskCompletionSource = CreateTaskCompletionSource(loginTaskStatus, loginResult);
                var loginTask = loginResultTaskCompletionSource.Task;
                using var services = new FakeCloudServiceBuilder().Build();

                var result = PlayerAccount.OnLoginAttemptCompleted(playerAccountTaskCompletionSource, services, cancellationToken);
                result(loginTask);

                if (isCancellationRequested
                    || loginTaskStatus is not TaskStatus.RanToCompletion
                    || loginResultType is not Result.Success)
                {
                    Assert.That(onLoggingInFailedRaisedTimes, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(onLoggingInFailedRaisedTimes, Is.Zero);
                }
            }
            finally
            {
                CoherenceCloud.OnLoggingInFailed -= OnLoggingInFailed;
            }

            void OnLoggingInFailed(LoginOperationError error)
            {
                onLoggingInFailedRaisedTimes++;
                error.Ignore();
            }

            CancellationToken CreateCancellationToken()
            {
                var cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;
                if (isCancellationRequested)
                {
                    cancellationTokenSource.Cancel();
                }

                return cancellationToken;
            }

            TaskCompletionSource<T> CreateTaskCompletionSource<T>(TaskStatus status, T result)
            {
                var taskCompletionSource = new TaskCompletionSource<T>();
                switch (status)
                {
                    case TaskStatus.Canceled:
                        taskCompletionSource.SetCanceled();
                        break;
                    case TaskStatus.Faulted:
                        taskCompletionSource.SetException(new Exception($"{typeof(T).Name} Exception"));
                        break;
                    case TaskStatus.RanToCompletion:
                        taskCompletionSource.SetResult(result);
                        break;
                }

                return taskCompletionSource;
            }
        }

        public override void TearDown()
        {
            foreach (var playerAccount in PlayerAccount.All)
            {
                Debug.LogError($"Test did not unregister playerAccount: {playerAccount}.");
                PlayerAccount.Unregister(playerAccount);
            }

            base.TearDown();
        }

        private static FakePlayerAccountBuilder CreateFakePlayerAccount(out PlayerAccount playerAccount, AuthClientType authClientType, string uniqueId = null)
        {
            var builder = new FakePlayerAccountBuilder();

            builder.SetProjectId(ProjectId)
                   .SetUniqueId(!string.IsNullOrEmpty(uniqueId) ? new(uniqueId) : UniqueId)
                       .SetupCloudService(x => x
                       .SetupRequestFactory(x => x.SetIsReady()));

            var shouldMockAuthClient = authClientType is AuthClientType.MockNotLoggedIn or AuthClientType.MockLoggedIn;
            if(shouldMockAuthClient)
            {
                builder.SetupCloudService(x => x
                           .SetupAuthClient(x => x.SetIsLoggedIn(authClientType is AuthClientType.MockLoggedIn))
                           .SetAutoLoginAsGuest(false)
                           .SetShouldMockAuthClient(true));
            }
            else
            {
                builder.SetupCloudService(x => x
                           .SetAutoLoginAsGuest(authClientType is AuthClientType.RealAutoLoginAsGuest)
                           .SetShouldMockAuthClient(false));
            }

            return builder.Build(out playerAccount);
        }

        private enum AuthClientType
        {
            MockNotLoggedIn,
            MockLoggedIn,
            RealNotLoggedIn,
            RealAutoLoginAsGuest
        }
    }
}
