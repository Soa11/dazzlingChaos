// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Brook;
    using Coherence.Serializer;
    using Common;
    using Entities;
    using Log;
    using ProtocolDef;
    using SimulationFrame;
    using Stats;

    internal class InOrderedNetworkChannel : IInNetworkChannel
    {
        internal static readonly int SequenceBufferSize = 512;
        internal const int MillisecondsMessageTTL = 5000;
        private static readonly TimeSpan MessageTTL = TimeSpan.FromMilliseconds(MillisecondsMessageTTL);

        public event Action<List<IncomingEntityUpdate>> OnEntityUpdate
        {
            add { }
            remove { }
        }
        public event Action<IEntityCommand> OnCommand;
        public event Action<IEntityInput> OnInput
        {
            add { }
            remove { }
        }

        private readonly IMessageDeserializer messageDeserializer;
        private readonly MessageResolver messageResolver;
        private readonly IEntityRegistry entityRegistry;
        private readonly Stats stats;
        private readonly Logger logger;

        private readonly ReceiveSequenceBuffer sequenceBuffer = new(SequenceBufferSize);
        private readonly Queue<ExpirableMessage> receivedCommands = new(32);

        private readonly IDateTimeProvider dateTimeProvider = new SystemDateTimeProvider();

        // Caches received messages each tick.
        private readonly CacheList<IEntityMessage> receivedMessages = new(32);
        private readonly CacheList<(MessageID, IEntityMessage)> deserializedMessages = new(32);

        public InOrderedNetworkChannel(
            ISchemaSpecificComponentDeserialize deserializer,
            IEntityRegistry entityRegistry,
            Stats stats,
            Logger logger,
            IMessageDeserializer messageDeserializer = null)
        {
            this.messageResolver = new MessageResolver(entityRegistry);
            this.entityRegistry = entityRegistry;
            this.stats = stats;
            this.logger = logger.With<InOrderedNetworkChannel>();
            this.messageDeserializer = messageDeserializer ?? new MessageDeserializer(deserializer, this.logger);
        }

        public bool Deserialize(IInBitStream stream, AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
        {
            using var _ = deserializedMessages;
            messageDeserializer.ReadOrderedCommands(stream, deserializedMessages);

            foreach (var (id, message) in deserializedMessages)
            {
                sequenceBuffer.InsertMessage(id, message);
            }

            return false;
        }

        public List<RefsInfo> GetRefsInfos() => default;

        public bool FlushBuffer(IReadOnlyCollection<Entity> _)
        {
            var now = dateTimeProvider.UtcNow;

            FlushSequenceBuffer();

            using var __ = receivedMessages;

            // Execute commands from queue, already in the right sequence.
            while (receivedCommands.Count > 0)
            {
                var next = receivedCommands.Peek();

                if (messageResolver.IsResolvable(next.Message))
                {
                    receivedMessages.Add(next.Message);
                    receivedCommands.Dequeue();
                    continue;
                }

                if (next.HasExpired(now))
                {
                    logger.Debug("Ordered message expired",
                        ("entity", next.Message.Entity),
                        ("type", next.Message.GetType()),
                        ("sender", next.Message.SenderParticipant),
                        ("routing", next.Message.Routing));

                    receivedCommands.Dequeue();
                    continue;
                }

                break; // Stop, cannot continue with the sequence.
            }

            foreach (var message in receivedMessages)
            {
                OnCommand?.Invoke((IEntityCommand)message);
            }

            return receivedMessages.Count > 0;
        }

        public void Clear()
        {
            receivedCommands.Clear();
            sequenceBuffer.Clear();
        }

        private void FlushSequenceBuffer()
        {
            var now = dateTimeProvider.UtcNow;

            using var _ = receivedMessages;

            sequenceBuffer.FlushMessages(receivedMessages);

            foreach (var message in receivedMessages)
            {
                receivedCommands.Enqueue(new ExpirableMessage(message, now + MessageTTL));
            }
        }
    }
}
