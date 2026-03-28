// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common;
    using Coherence.Core.Channels;
    using Coherence.Generated;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Coherence.Tests;
    using NUnit.Framework;

    public class InOrderedNetworkChannelTests : CoherenceTest
    {
        private Definition definition;
        private MessageDeserializerMock messageDeserializerMock;
        private EntityRegistry entityRegistry;
        private InOrderedNetworkChannel channel;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            definition = new Definition();
            entityRegistry = new EntityRegistry(new HashSet<Entities.Entity>(), new Dictionary<Entities.Entity, AuthorityType>());
            messageDeserializerMock = new MessageDeserializerMock();

            channel = new InOrderedNetworkChannel(definition, entityRegistry, null, logger, messageDeserializerMock);
        }

        [Test]
        [Description("Verifies that FlushBuffer method does not allocate.")]
        public void FlushBuffer_ShouldNotAllocate()
        {
            // Arrange
            var iterations = 100;

            messageDeserializerMock.orderedCommands = new List<(MessageID, IEntityMessage)>()
            {
                (new MessageID(0), new ByteArraysCommand()),
                (new MessageID(1), new ByteArraysCommand()),
                (new MessageID(2), new ByteArraysCommand()),
                (new MessageID(3), new ByteArraysCommand()),
            };

            void TestAction()
            {
                for (var i = 0; i < iterations; i++)
                {
                    channel.Deserialize(null, 0, Vector3d.zero);
                    channel.FlushBuffer(null);
                    channel.Clear();
                }
            }

            // Warm up JIT
            TestAction();

            // Act
            var allocations = AllocCounter.Instrument(TestAction);

            // Assert
            Assert.That(allocations, Is.EqualTo(0));
        }

        private class MessageDeserializerMock : IMessageDeserializer
        {
            public List<(MessageID, IEntityMessage)> orderedCommands;

            public void ReadOrderedCommands(IInBitStream bitStream, List<(MessageID, IEntityMessage)> deserializedMessages)
            {
                deserializedMessages.AddRange(orderedCommands);
            }

            public IEntityCommand[] ReadCommands(IInBitStream bitStream) => throw new NotImplementedException();
            public IEntityInput[] ReadInputs(IInBitStream bitStream) => throw new NotImplementedException();
        }
    }
}
