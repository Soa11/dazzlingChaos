namespace Coherence.Toolkit.Tests
{
    using System;
    using Moq;

    public sealed class MockNetworkObjectInstantiatorBuilder : IDisposable
    {
        private Func<ICoherenceSync> onInstantiate;
        private Action<ICoherenceSync> onDestroy;
        private MockSyncBuilder syncBuilder;
        private bool buildExecuted;
        private Mock<INetworkObjectInstantiator> mock;
        private INetworkObjectInstantiator instantiator;
        private bool disposed;

        private Mock<INetworkObjectInstantiator> Mock
        {
            get
            {
                if (mock is null)
                {
                    Build();
                }

                return mock;
            }
        }

        public MockSyncBuilder SyncBuilder => syncBuilder ??= (new MockSyncBuilder().SetNetworkObjectInstantiatorBuilder(this));

        public MockNetworkObjectInstantiatorBuilder()
        {
            onInstantiate = () => SyncBuilder.Build().mockSync.Object;
            onDestroy = _ => { };
        }

        public MockNetworkObjectInstantiatorBuilder SetSyncBuilder(MockSyncBuilder builder)
        {
            syncBuilder = builder;
            return this;
        }

        public MockNetworkObjectInstantiatorBuilder SetupSyncBuilder(Action<MockSyncBuilder> setup)
        {
            setup(SyncBuilder);
            return this;
        }

        public MockNetworkObjectInstantiatorBuilder SetInstantiateResult(Func<ICoherenceSync> result)
        {
            onInstantiate = result;
            return this;
        }

        public MockNetworkObjectInstantiatorBuilder SetDestroyCallback(Action<ICoherenceSync> action)
        {
            onDestroy = action;
            return this;
        }

        public INetworkObjectInstantiator Build()
        {
            if (buildExecuted)
            {
                return instantiator ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;
            mock = new(MockBehavior.Strict);
            mock.Setup(x => x.Instantiate(It.IsAny<SpawnInfo>())).Returns(onInstantiate);
            mock.Setup(x => x.Destroy(It.IsAny<ICoherenceSync>())).Callback(onDestroy);
            instantiator = mock.Object;
            return instantiator;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            syncBuilder?.Dispose();
        }
    }
}
