// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using Coherence.Tests;
    using NUnit.Framework;
    using Portal;

    /// <summary>
    /// Tests related to <see cref="Schemas.state"/>.
    /// </summary>
    public sealed class SchemasStateTests : CoherenceTest
    {
        private Schemas.SyncState stateWas;

        [TestCase(Schemas.SyncState.Unknown, Schemas.SyncState.InProgress)]
        [TestCase(Schemas.SyncState.Unknown, Schemas.SyncState.InSync)]
        [TestCase(Schemas.SyncState.Unknown, Schemas.SyncState.OutOfSync)]
        [TestCase(Schemas.SyncState.InProgress, Schemas.SyncState.Unknown)]
        [TestCase(Schemas.SyncState.InProgress, Schemas.SyncState.InSync)]
        [TestCase(Schemas.SyncState.InProgress, Schemas.SyncState.OutOfSync)]
        [TestCase(Schemas.SyncState.InSync, Schemas.SyncState.Unknown)]
        [TestCase(Schemas.SyncState.InSync, Schemas.SyncState.InProgress)]
        [TestCase(Schemas.SyncState.InSync, Schemas.SyncState.OutOfSync)]
        [TestCase(Schemas.SyncState.OutOfSync, Schemas.SyncState.Unknown)]
        [TestCase(Schemas.SyncState.OutOfSync, Schemas.SyncState.InProgress)]
        [TestCase(Schemas.SyncState.OutOfSync, Schemas.SyncState.InSync)]
        public void OnSchemaStateUpdate_Is_Raised_Once_When_State_Changes(Schemas.SyncState from, Schemas.SyncState to)
        {
            var eventRaisedTimes = 0;

            try
            {
                Schemas.state = from;
                Schemas.OnSchemaStateUpdate += OnSchemaStateUpdate;

                Schemas.state = to;

                Assert.That(eventRaisedTimes, Is.EqualTo(1));
            }
            finally
            {
                Schemas.OnSchemaStateUpdate -= OnSchemaStateUpdate;
            }

            void OnSchemaStateUpdate() => eventRaisedTimes++;
        }

        [TestCase(Schemas.SyncState.InProgress, Schemas.SyncState.InProgress)]
        [TestCase(Schemas.SyncState.InSync, Schemas.SyncState.InSync)]
        [TestCase(Schemas.SyncState.OutOfSync, Schemas.SyncState.OutOfSync)]
        [TestCase(Schemas.SyncState.Unknown, Schemas.SyncState.Unknown)]
        public void OnSchemaStateUpdate_Is_Not_Raised_Once_When_State_Does_Not_Change(Schemas.SyncState from, Schemas.SyncState to)
        {
            var eventRaisedTimes = 0;

            try
            {
                Schemas.state = from;
                Schemas.OnSchemaStateUpdate += OnSchemaStateUpdate;

                Schemas.state = to;

                Assert.That(eventRaisedTimes, Is.Zero);
            }
            finally
            {
                Schemas.OnSchemaStateUpdate -= OnSchemaStateUpdate;
            }

            void OnSchemaStateUpdate() => eventRaisedTimes++;
        }

        public override void SetUp()
        {
            base.SetUp();
            stateWas = Schemas.state;
        }

        public override void TearDown()
        {
            Schemas.state = stateWas;
            base.TearDown();
        }
    }
}
