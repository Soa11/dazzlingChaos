// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Runtime.Tests
{
    using System;
    using System.Threading;
    using Coherence.Cloud;
    using Moq;

    /// <summary>
    /// Can be used to <see cref="Build"/> a mock <see cref="IAuthClientInternal"/>
    /// object for use in a test.
    /// </summary>
    internal sealed class MockAuthClientBuilder
    {
        private bool buildExecuted;
        private IAuthClientInternal authClient;
        private bool isLoggedIn = true;
        private Func<PlayerAccount> getPlayerAccount;
        private SessionToken sessionToken = new("Expected SessionToken");
        private readonly Mock<IAuthClientInternal> mock = new(MockBehavior.Strict);

        public IAuthClientInternal AuthClient => Build();

        private PlayerAccountId? playerAccountId;

        public PlayerAccountId PlayerAccountId
        {
            get
            {
                if (getPlayerAccount is not null)
                {
                    return getPlayerAccount.Invoke().Id;
                }

                playerAccountId ??= new("PlayerAccountId");
                return playerAccountId.Value;
            }
        }

        public MockAuthClientBuilder SetIsLoggedIn(bool isLoggedIn = true)
        {
            this.isLoggedIn = isLoggedIn;
            return this;
        }

        public MockAuthClientBuilder SetPlayerAccount(Func<PlayerAccount> getPlayerAccount)
        {
            this.getPlayerAccount = getPlayerAccount;
            return this;
        }

        public MockAuthClientBuilder SetSessionToken(SessionToken sessionToken)
        {
            this.sessionToken = sessionToken;
            return this;
        }

        public MockAuthClientBuilder OnPlayerAccountOperationAsync<TRequest, TResponse, TResult>(Action<PlayerAccountOperation<TResult>, Func<TResponse, TResult>, CancellationToken> callback) where TRequest : struct, IPlayerAccountOperationRequest where TResponse : IPlayerAccountOperationResponse
        {
            mock.Setup(x => x.PlayerAccountOperationAsync
            (
                It.IsAny<PlayerAccountOperationInfo<TRequest>>(),
                It.IsAny<Func<TResponse, TResult>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Action>())
            ).Callback(callback);
            return this;
        }

        public MockAuthClientBuilder OnPlayerAccountOperationAsync<TRequest>(Action<PlayerAccountOperationInfo<TRequest>, CancellationToken> callback) where TRequest : struct, IPlayerAccountOperationRequest
        {
            mock.Setup(x => x.PlayerAccountOperationAsync
            (
                It.IsAny<PlayerAccountOperationInfo<TRequest>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Action>())
            ).Callback(callback);
            return this;
        }

        public IAuthClientInternal Build()
        {
            if (buildExecuted)
            {
                return authClient ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;
            mock.Setup(x => x.LoggedIn).Returns(()=> isLoggedIn);
            mock.Setup(x => x.SessionToken).Returns(()=> sessionToken);
            mock.SetupGet(x => x.PlayerAccount).Returns(getPlayerAccount);
            mock.SetupSet(x => x.PlayerAccount = It.IsAny<PlayerAccount>()).Callback<PlayerAccount>(x => getPlayerAccount = () => x);
            mock.Setup(x => x.PlayerAccountId).Returns(()=> PlayerAccountId);
            authClient = mock.Object;
            return authClient;
        }

        public void RaiseOnLogin(LoginResponse loginResponse)
        {
            Build();
            mock.Raise(mock => mock.OnLogin += null, loginResponse);
        }

        public void RaiseOnLogout()
        {
            Build();
            mock.Raise(mock => mock.OnLogout += null);
        }

        public static implicit operator Mock<IAuthClientInternal>(MockAuthClientBuilder builder)
        {
            builder.Build();
            return builder.mock;
        }
    }
}
