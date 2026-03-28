// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using Entities;
    using Moq;

    /// <summary>
    /// Can be used to <see cref="Build"/> a mock <see cref="EntitiesManager"/> object for use in a test.
    /// </summary>
    public sealed class MockEntitiesManagerBuilder
    {
        private Mock<EntitiesManager> mock;
        private EntitiesManager entitiesManager;
        private Func<(NetworkEntityState State, ComponentUpdates? Data, uint? Lod, bool IsServerObjectOnClient)> syncNetworkEntityStateReturns;
        private Exception syncNetworkEntityStateThrows;
        private bool buildExecuted;

        public Mock<EntitiesManager> Mock
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

        public MockEntitiesManagerBuilder SetSyncNetworkEntityStateReturns(Func<(NetworkEntityState State, ComponentUpdates? Data, uint? Lod, bool IsServerObjectOnClient)> resultGetter)
        {
            syncNetworkEntityStateReturns = resultGetter;
            return this;
        }

        public MockEntitiesManagerBuilder SetSyncNetworkEntityStateThrows(Exception exception)
        {
            syncNetworkEntityStateThrows = exception;
            return this;
        }

        public EntitiesManager Build()
        {
            if (buildExecuted)
            {
                return entitiesManager ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;

            mock = new();
            if (syncNetworkEntityStateThrows is not null)
            {
                mock.Setup(m => m.SyncNetworkEntityState(It.IsAny<ICoherenceSync>()))
                    .Throws(syncNetworkEntityStateThrows);
            }

            entitiesManager = mock.Object;
            return entitiesManager;
        }
    }
}
