// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using System;
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common.Pooling;
    using Coherence.Connection;
    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.ProtocolDef;
    using Microsoft.IO;

    public interface IMessageSerializer
    {
        List<MessageID> WriteOrderedCommands(List<(MessageID, SerializedEntityMessage)> messages, SerializerContext<IOutBitStream> ctx);

        void WriteMessages(
            List<SerializedEntityMessage> serializedMessagesBuffer,
            MessageType messageType,
            Queue<SerializedEntityMessage> messages,
            SerializerContext<IOutBitStream> ctx);

        SerializedEntityMessage SerializeCommand(
            IEntityCommand command,
            bool useDebugStream,
            bool includeSenderClientId = false);

        SerializedEntityMessage SerializeInput(
            MessageTarget target,
            IEntityMessage message,
            Entity id,
            bool useDebugStream);
    }

    public class MessageSerializer : IMessageSerializer
    {
        public const int NUM_BITS_FOR_MESSAGE_TARGET = 2;
        public const int OLD_MAX_SERIALIZED_MESSAGE_BYTES = 1024;

        private const uint MessageTypeBitCount = 8;
        private const uint MessageCountBitCount = 8;
        private const uint MessageHeaderBitCount = MessageTypeBitCount + MessageCountBitCount;
        private const int MaxMessageCount = (1 << (int)MessageCountBitCount) - 1;

        public static readonly RecyclableMemoryStreamManager.Options RecyclableMemoryStreamManagerOptions = new()
        {
            BlockSize = OLD_MAX_SERIALIZED_MESSAGE_BYTES,
            LargeBufferMultiple = 1024 * 4, // 4 KB
            MaximumBufferSize = 256 * 1024, // 256 KB
            MaximumSmallPoolFreeBytes = 1 * 1024 * 1024, // 1 MB
            MaximumLargePoolFreeBytes = 1 * 1024 * 1024, // 1 MB
            UseExponentialLargeBuffer = true,
            MaximumStreamCapacity = 0,
        };

        private readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager =
            new(RecyclableMemoryStreamManagerOptions);

        private readonly Pool<PooledChunkedOutOctetStream> outStreamPool;

        private readonly ISchemaSpecificComponentSerialize schemaSerializer;
        private readonly Logger logger;

        public MessageSerializer(ISchemaSpecificComponentSerialize schemaSerializer, Logger logger)
        {
            this.outStreamPool = Pool<PooledChunkedOutOctetStream>
                .Builder(pool => new PooledChunkedOutOctetStream(pool, recyclableMemoryStreamManager.GetStream()))
                .Build();

            this.schemaSerializer = schemaSerializer;
            this.logger = logger.With<MessageSerializer>();
        }

        public List<MessageID> WriteOrderedCommands(List<(MessageID, SerializedEntityMessage)> messages, SerializerContext<IOutBitStream> ctx)
        {
            ctx.StartSection("OrderedCommands");

            List<MessageID> res = new(32);
            ushort lastMessageID = 0;

            using (_ = Serialize.NewEndOfMessagesReservationScope(ctx))
            {
                foreach (var (id, message) in messages)
                {
                    var rewindPos = ctx.BitStream.Position;

                    Serialize.WriteMessageType(MessageType.Command, ctx.BitStream);

                    WriteMessageIDDelta(id.Value, lastMessageID, ctx.BitStream);
                    lastMessageID = id.Value;

                    ctx.SetEntity(message.TargetEntity);
                    ctx.BitStream.WriteBytesUnaligned(message.OutOctetStream.Octets, (int)message.BitCount);

                    if (ctx.IsStreamFull())
                    {
                        ctx.BitStream.Seek(rewindPos);
                        break;
                    }

                    if (ctx.PassedPreferredMaxBitCount() && ctx.HasSerializedChanges)
                    {
                        // Serializing this command would exceed the preferred packet size.
                        // But there are already some serialized changes in the packet,
                        // it is better to rewind this change and stop here so the packet isn't fragmented unnecessarily.
                        ctx.BitStream.Seek(rewindPos);
                        break;
                    }

                    ctx.HasSerializedChanges = true;
                    res.Add(id);
                }

                if (res.Count > 0)
                {
                    Serialize.WriteEndOfMessages(ctx);
                }
            }

            ctx.EndSection();

            return res;
        }

        public (int, uint) GetCountFromBudget(
            Queue<SerializedEntityMessage> messages,
            uint maxBudget,
            int preferredBudget,
            bool hasSerializedChanges,
            bool useDebugStreams,
            uint freeBitsInEmptyPacket)
        {
            if (messages.Count == 0)
            {
                return (0, 0);
            }

            // MessageQueue header = message type + message count
            var bitCount = MessageHeaderBitCount;
            if (useDebugStreams)
            {
                bitCount += (uint)DebugStreamTypes.DebugBitsSize(2); // Two writes
            }

            var messageCount = 0;

            foreach (var message in messages)
            {
                // Message header = entity ID + version + message target + componentID (+ simulation frame for inputs)
                // The header has already been pre-serialized and is accounted for in message.BitCount
                var messageBitCount = message.BitCount;

                if (messageBitCount > freeBitsInEmptyPacket)
                {
                    logger.Warning(Warning.SerializeMessageTooBig,
                        ("target", message.TargetEntity),
                        ("sizeBits", messageBitCount),
                        ("maxSizeBits", freeBitsInEmptyPacket));
                }

                if (bitCount + messageBitCount >= maxBudget)
                {
                    break;
                }

                if (bitCount + messageBitCount > preferredBudget && hasSerializedChanges)
                {
                    // Serializing this message would exceed the preferred packet size.
                    // But there are already some serialized changes in the packet,
                    // it is better to rewind this change and stop here so the packet isn't fragmented unnecessarily.
                    break;
                }

                hasSerializedChanges = true;
                bitCount += message.BitCount;
                messageCount++;
            }

            if (messageCount > MaxMessageCount)
            {
                messageCount = MaxMessageCount;
            }

            return (messageCount, bitCount);
        }

        public void WriteMessages(
            List<SerializedEntityMessage> serializedMessagesBuffer,
            MessageType messageType,
            Queue<SerializedEntityMessage> messages,
            SerializerContext<IOutBitStream> ctx)
        {
            ctx.StartSection(messageType.AsString());

            if (ctx.BitStream.IsFull)
            {
                return;
            }

            (var messageCount, _) = GetCountFromBudget(messages,
                maxBudget: ctx.RemainingUnreservedBitCount,
                preferredBudget: (int)ctx.PreferredMaxBitCount - (int)ctx.BitStream.Position - (int)ctx.ReservedBits,
                hasSerializedChanges: ctx.HasSerializedChanges,
                useDebugStreams: ctx.UseDebugStreams,
                freeBitsInEmptyPacket: ctx.FreeBitsInEmptyPacket - MessageHeaderBitCount);
            if (messageCount == 0)
            {
                return;
            }

            Serialize.WriteMessageType(messageType, ctx.BitStream);

            ctx.BitStream.WriteUint8((byte)messageCount);
            for (var i = 0; i < messageCount; ++i)
            {
                var message = messages.Dequeue();

                try
                {
                    ctx.SetEntity(message.TargetEntity);
                    ctx.BitStream.WriteBytesUnaligned(message.OutOctetStream.Octets, (int)message.BitCount);
                }
                catch
                {
                    // put the message back on the queue.
                    messages.Enqueue(message);

                    throw;
                }

                ctx.Logger.Trace("serialized message",
                    ("target", message.TargetEntity.ToString()),
                    ("type", message.GetType().ToString()));

                ctx.HasSerializedChanges = true;
                serializedMessagesBuffer.Add(message);
            }

            ctx.EndSection();
        }

        public SerializedEntityMessage SerializeCommand(
            IEntityCommand command,
            bool useDebugStream,
            bool includeSenderClientId = false)
        {
            var target = command.Target;
            var id = command.Entity;
            var hasMeta = command.UsesMeta;

            var outOctetStream = PrepareMessageSerialization(target, command,
                id, useDebugStream,
                out var bitStream,
                out var fieldStream);

            try
            {
                SerializeCommandMeta(command, includeSenderClientId, fieldStream, hasMeta);

                schemaSerializer.WriteCommand(command, command.GetComponentType(), fieldStream, logger);

                if (bitStream.IsFull)
                {
                    throw new Exception($"Command is too large and will not be sent to {target}.");
                }

                bitStream.Flush();
            }
            catch
            {
                outOctetStream.ReturnIfPoolable();

                throw;
            }

            return new SerializedEntityMessage(id, outOctetStream, bitStream.Position);
        }

        public SerializedEntityMessage SerializeInput(
            MessageTarget target,
            IEntityMessage message,
            Entity id,
            bool useDebugStream)
        {
            var outOctetStream = PrepareMessageSerialization(target, message,
                id, useDebugStream,
                out var bitStream,
                out var fieldStream);

            try
            {
                var input = (IEntityInput)message;
                fieldStream.WriteLong(input.Frame);

                schemaSerializer.WriteInput(input, message.GetComponentType(), fieldStream, logger);

                if (bitStream.IsFull)
                {
                    throw new Exception($"Input is too large and will not be sent to {target}.");
                }

                bitStream.Flush();
            }
            catch
            {
                outOctetStream.ReturnIfPoolable();

                throw;
            }

            return new SerializedEntityMessage(id, outOctetStream, bitStream.Position);
        }

        private IOutOctetStream PrepareMessageSerialization(MessageTarget target,
            IEntityMessage message, Entity id, bool useDebugStream,
            out IOutBitStream bitStream, out OutProtocolBitStream fieldStream)
        {
            var octetStream = outStreamPool.Rent();
            bitStream = new OutBitStream(octetStream);
            if (useDebugStream)
            {
                bitStream = new DebugOutBitStream(bitStream);
            }

            try
            {
                WriteMessageEntityId(id, bitStream);
                WriteMessageTarget(target, bitStream);
                Serialize.WriteComponentId(message.GetComponentType(), bitStream);
            }
            catch
            {
                octetStream.ReturnIfPoolable();

                throw;
            }

            fieldStream = OutProtocolBitStream.Shared.Reset(bitStream, logger);
            return octetStream;
        }

        private void SerializeCommandMeta(IEntityCommand command, bool includeSenderClientId,
            OutProtocolBitStream fieldStream, bool hasMeta)
        {
            fieldStream.WriteBool(hasMeta);

            if (hasMeta)
            {
                fieldStream.WriteBool(includeSenderClientId);
                if (includeSenderClientId)
                {
                    fieldStream.WriteBits((uint)command.SenderClientID, ClientID.SIZE_IN_BITS);
                }

                fieldStream.WriteLong(command.Frame);
            }
        }

        private void WriteMessageEntityId(Entity entityID, IOutBitStream outBitStream)
        {
            entityID.AssertRelative();

            SerializeTools.SerializeEntity(entityID, outBitStream);
        }

        private void WriteMessageTarget(MessageTarget target, IOutBitStream outBitStream)
        {
            outBitStream.WriteBits((uint)target, NUM_BITS_FOR_MESSAGE_TARGET);
        }

        private void WriteMessageIDDelta(ushort id, ushort lastId, IOutBitStream stream)
        {
            var delta = id - lastId;
            SerializeTools.WriteShortVarInt(stream, (ushort)Math.Abs(delta));
            stream.WriteBits(delta < 0 ? 0u : 1u, 1);
        }
    }
}
