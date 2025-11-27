namespace Coherence.Serializer
{
    using System.Collections.Generic;
    using System.IO;
    using Coherence.Brook;
    using Coherence.Connection;
    using Coherence.Log;
    using Coherence.ProtocolDef;

    public interface IMessageDeserializer
    {
        List<(MessageID, IEntityMessage)> ReadOrderedCommands(IInBitStream bitStream);
        IEntityCommand[] ReadCommands(IInBitStream bitStream);
        IEntityInput[] ReadInputs(IInBitStream bitStream);
    }

    public class MessageDeserializer : IMessageDeserializer
    {
        private readonly ISchemaSpecificComponentDeserialize schemaDeserializer;
        private readonly Logger logger;

        public MessageDeserializer(ISchemaSpecificComponentDeserialize schemaDeserializer, Logger logger)
        {
            this.schemaDeserializer = schemaDeserializer;
            this.logger = logger.With<MessageDeserializer>();
        }

        public List<(MessageID, IEntityMessage)> ReadOrderedCommands(IInBitStream bitStream)
        {
            List<(MessageID, IEntityMessage)> res = new(32);

            ushort lastMessageID = 0;
            while (DeserializeCommands.DeserializeCommand(bitStream, out var messageType))
            {
                if (messageType != MessageType.Command)
                {
                    throw new InvalidDataException($"Unexpected message type '{messageType}'");
                }

                var id = ReadNextMessageID(bitStream, lastMessageID);
                var command = ReadCommand(bitStream);

                res.Add((id, command));
                lastMessageID = id.Value;
            }

            return res;
        }

        public IEntityCommand[] ReadCommands(IInBitStream bitStream)
        {
            var numMessages = bitStream.ReadUint8();

            var commandData = new IEntityCommand[numMessages];

            for (var i = 0; i < numMessages; i++)
            {
                commandData[i] = ReadCommand(bitStream);
            }

            return commandData;
        }

        public IEntityCommand ReadCommand(IInBitStream bitStream)
        {
            var entityID = DeserializerTools.DeserializeEntity(bitStream);
            var messageTarget = DeserializeMessageTarget(bitStream);
            var componentType = DeserializerTools.DeserializeComponentTypeID(bitStream);
            var meta = DeserializeCommandMeta(bitStream);

            var inBitStream = new InProtocolBitStream(bitStream);

            var command = schemaDeserializer.ReadCommand(componentType, entityID, messageTarget, inBitStream, logger);

            if (meta.HasValue)
            {
                command.UsesMeta = true;
                command.SenderClientID = new ClientID(meta.Value.SenderID);
                command.Frame = meta.Value.Frame;
            }

            return command;
        }

        public IEntityInput[] ReadInputs(IInBitStream bitStream)
        {
            var numMessages = bitStream.ReadUint8();

            var inputData = new IEntityInput[numMessages];

            for (var i = 0; i < numMessages; i++)
            {
                inputData[i] = ReadInput(bitStream);
            }

            return inputData;
        }

        public IEntityInput ReadInput(IInBitStream bitStream)
        {
            var entityID = DeserializerTools.DeserializeEntity(bitStream);
            var routing = DeserializeMessageTarget(bitStream);
            var componentType = DeserializerTools.DeserializeComponentTypeID(bitStream);

            var inBitStream = new InProtocolBitStream(bitStream);
            var frame = (long)bitStream.ReadUint64();

            var input = schemaDeserializer.ReadInput(componentType, entityID, frame, inBitStream, logger);

            input.Routing = routing;

            return input;
        }

        // ReadNextMessageID reads and uses VarInt decoding to get the next message id in the sequence based in the last read value.
        private MessageID ReadNextMessageID(IInBitStream bitstream, ushort lastID)
        {
            var delta = DeserializerTools.ReadShortVarInt(bitstream);
            var sign = bitstream.ReadBits(1);
            var index = (uint)(lastID + (delta * (sign == 1 ? 1 : -1)));

            return new MessageID((ushort)index);
        }

        private RawCommandMeta? DeserializeCommandMeta(IInBitStream inBitStream)
        {
            uint senderID = 0;
            long frame = 0;

            var hasMeta = inBitStream.ReadBits(1) == 1;
            if (!hasMeta)
            {
                return null;
            }

            var isSenderIDPresent = inBitStream.ReadBits(1) == 1;
            if (isSenderIDPresent)
            {
                senderID = inBitStream.ReadUint32();
            }

            frame = (long)inBitStream.ReadUint64();

            return new RawCommandMeta
            {
                SenderID = senderID,
                Frame = frame
            };
        }

        private MessageTarget DeserializeMessageTarget(IInBitStream inBitStream)
        {
            return (MessageTarget)inBitStream.ReadBits(2);
        }
    }
}
