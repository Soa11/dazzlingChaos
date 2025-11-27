// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer.Tests
{
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common;
    using Coherence.Connection;
    using Coherence.Entities;
    using Coherence.Generated;
    using Coherence.ProtocolDef;
    using Coherence.Tests;
    using NUnit.Framework;

    public class MesssageSerializerTests : CoherenceTest
    {
        private Definition definition;
        private MessageSerializer messageSerializer;
        private MessageDeserializer messageDeserializer;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            definition = new Definition();
            messageSerializer = new MessageSerializer(definition, this.logger);
            messageDeserializer = new MessageDeserializer(definition, this.logger);
        }

        [Test(Description = "Serialization and deserialization of a list of ordered commands")]
        public void SerializeOrderedCommands()
        {
            var commands = new List<(MessageID, SerializedEntityMessage)>()
            {
                (new MessageID(5), CreateTestCommand()),
                (new MessageID(6), CreateTestCommand()),
                (new MessageID(9), CreateTestCommand()),
                (new MessageID(10), CreateTestCommand())
            };

            var stream = new OutOctetStream(ConnectionSettings.DEFAULT_MTU);

            // Serialize
            {
                var ctx = new SerializerContext<IOutBitStream>(new OutBitStream(stream), false, logger);

                var res = messageSerializer.WriteOrderedCommands(commands, ctx);

                ctx.BitStream.Flush();

                Assert.That(res.Count, Is.EqualTo(commands.Count));
                for (var i = 0; i < res.Count; ++i)
                {
                    Assert.That(res[i], Is.EqualTo(commands[i].Item1));
                }
            }

            // Deserialize
            {
                var octetReader = new InOctetStream(stream.Close().ToArray());
                var inBitStream = new InBitStream(octetReader, (int)octetReader.Length * 8);
                var res = messageDeserializer.ReadOrderedCommands(inBitStream);

                Assert.That(res.Count, Is.EqualTo(commands.Count));
                for (var i = 0; i < res.Count; ++i)
                {
                    Assert.That(res[i].Item1, Is.EqualTo(commands[i].Item1));
                    Assert.That(res[i].Item2, Is.TypeOf<AuthorityRequest>());
                }
            }
        }

        [Test]
        [Description("Verifies that if an ordered command exceeds the preferred budget, and there are no prior changes " +
            "that it is still serialized.")]
        public void OrderedCommands_PreferredSize_NoPriorChanges()
        {
            // Arrange
            var commands = new List<(MessageID, SerializedEntityMessage)>()
            {
                (new MessageID(5), CreateTestCommand()),
                (new MessageID(6), CreateTestCommand()),
                (new MessageID(9), CreateTestCommand()),
                (new MessageID(10), CreateTestCommand())
            };

            var stream = new OutOctetStream(10000);
            var ctx = new SerializerContext<IOutBitStream>(new OutBitStream(stream), false, logger);
            ctx.PreferredMaxBitCount = 1;

            // Act
            var res = messageSerializer.WriteOrderedCommands(commands, ctx);

            // Assert
            Assert.That(res.Count, Is.EqualTo(1));
        }

        [Test]
        [Description("Verifies that if an ordered command exceeds the preferred budget, and there are prior changes " +
            "that it is not serialized.")]
        public void OrderedCommands_PreferredSize_WithPriorChanges()
        {
            // Arrange
            var commands = new List<(MessageID, SerializedEntityMessage)>()
            {
                (new MessageID(5), CreateTestCommand()),
                (new MessageID(6), CreateTestCommand()),
                (new MessageID(9), CreateTestCommand()),
                (new MessageID(10), CreateTestCommand())
            };

            var stream = new OutOctetStream(10000);
            var ctx = new SerializerContext<IOutBitStream>(new OutBitStream(stream), false, logger);
            ctx.PreferredMaxBitCount = 1;
            ctx.HasSerializedChanges = true;

            // Act
            var res = messageSerializer.WriteOrderedCommands(commands, ctx);

            // Assert
            Assert.That(res.Count, Is.EqualTo(0));
        }

        [TestCase(MessageTarget.StateAuthorityOnly)]
        [TestCase(MessageTarget.InputAuthorityOnly)]
        [TestCase(MessageTarget.Other)]
        [TestCase(MessageTarget.All)]
        [Description("Verifies that the message target is serialized and deserialized correctly, and that there are sufficient bits for it.")]
        public void Test_SerializingMessageTarget(MessageTarget target)
        {
            _ = CreateTestCommand(target);
        }

        [Test]
        [Description("Verifies that we can serialize and deserialize a command containing big data.")]
        public void Test_SerializingBigDataCommand()
        {
            // Arrange
            var entityID = new Entity((Index)1, 0, false);
            var command = new ByteArraysCommand(entityID,
                GenerateByteArray(500),
                GenerateByteArray(454),
                GenerateByteArray(450),
                GenerateByteArray(455),
                GenerateByteArray(504),
                GenerateByteArray(505));

            // Act
            var serializedCommand = messageSerializer.SerializeCommand(command, false);

            var inStream = new InOctetStream(serializedCommand.OutOctetStream.Close());
            var inBitStream = new InBitStream(inStream, (int)inStream.Length * 8);

            var deserialziedCommand = messageDeserializer.ReadCommand(inBitStream);

            // Assert
            Assert.That(deserialziedCommand, Is.TypeOf<ByteArraysCommand>());
            var byteArraysCommand = (ByteArraysCommand)deserialziedCommand;

            Assert.That(deserialziedCommand.Entity, Is.EqualTo(command.Entity));
            Assert.That(byteArraysCommand.bytes1, Is.EqualTo(command.bytes1));
            Assert.That(byteArraysCommand.bytes2, Is.EqualTo(command.bytes2));
            Assert.That(byteArraysCommand.bytes3, Is.EqualTo(command.bytes3));
            Assert.That(byteArraysCommand.bytes4, Is.EqualTo(command.bytes4));
            Assert.That(byteArraysCommand.bytes5, Is.EqualTo(command.bytes5));
            Assert.That(byteArraysCommand.bytes6, Is.EqualTo(command.bytes6));
        }

        [Test]
        [Description("Verifies that we can serialize and deserialize an input containing big data.")]
        public void Test_SerializingBigDataInput()
        {
            // Arrange
            var entityID = new Entity((Index)1, 0, false);
            var input = new ByteArraysInput(entityID, 0,
                GenerateByteArray(500),
                GenerateByteArray(454),
                GenerateByteArray(450),
                GenerateByteArray(455),
                GenerateByteArray(504),
                GenerateByteArray(505),
                false);
            var inputData = new InputData() { Input = input };

            // Act
            var serializedInput = messageSerializer.SerializeInput(MessageTarget.All, inputData, entityID, false);

            var inStream = new InOctetStream(serializedInput.OutOctetStream.Close());
            var inBitStream = new InBitStream(inStream, (int)inStream.Length * 8);

            var deserializedInput = messageDeserializer.ReadInput(inBitStream);

            // Assert
            Assert.That(deserializedInput, Is.TypeOf<ByteArraysInput>());
            var byteArraysInput = (ByteArraysInput)deserializedInput;

            Assert.That(deserializedInput.Entity, Is.EqualTo(input.Entity));
            Assert.That(byteArraysInput.bytes1, Is.EqualTo(input.bytes1));
            Assert.That(byteArraysInput.bytes2, Is.EqualTo(input.bytes2));
            Assert.That(byteArraysInput.bytes3, Is.EqualTo(input.bytes3));
            Assert.That(byteArraysInput.bytes4, Is.EqualTo(input.bytes4));
            Assert.That(byteArraysInput.bytes5, Is.EqualTo(input.bytes5));
            Assert.That(byteArraysInput.bytes6, Is.EqualTo(input.bytes6));
        }

        [Test]
        [Description("Verifies that a warning is logged when a command is too big to fit in a packet.")]
        public void Test_GetCountFromBudget_CommandTooBig()
        {
            var command = CreateTestCommand();
            var queue = new Queue<SerializedEntityMessage>();
            queue.Enqueue(command);

            _ = messageSerializer.GetCountFromBudget(queue, 10000, 10000, false, false, command.BitCount - 1);

            Assert.That(this.logger.GetCountForWarningID(Log.Warning.SerializeMessageTooBig), Is.EqualTo(1));
        }

        [Test]
        [Description("Verifies that if a command exceeds the preferred budget, and there are no prior changes " +
            "that it is still serialized.")]
        public void Test_GetCountFromBudget_PreferredBudget_NoPriorChanges()
        {
            var command = CreateTestCommand();
            var queue = new Queue<SerializedEntityMessage>();
            queue.Enqueue(command);
            queue.Enqueue(command);
            queue.Enqueue(command);

            var (count, _) = messageSerializer.GetCountFromBudget(queue, 10000, 1, false, false, 10000);

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        [Description("Verifies that if a command exceeds the preferred budget, and there are prior changes " +
            "that it is not serialized.")]
        public void Test_GetCountFromBudget_PreferredBudget_WithPriorChanges()
        {
            var command = CreateTestCommand();
            var queue = new Queue<SerializedEntityMessage>();
            queue.Enqueue(command);
            queue.Enqueue(command);
            queue.Enqueue(command);

            var (count, _) = messageSerializer.GetCountFromBudget(queue, 10000, 1, true, false, 10000);

            Assert.That(count, Is.EqualTo(0));
        }

        private SerializedEntityMessage CreateTestCommand()
        {
            var entityID = new Entity((Index)1, 0, false);
            var cmd = definition.CreateAuthorityRequest(
                entityID,
                new ClientID(1),
                AuthorityType.State
            );

            return messageSerializer.SerializeCommand(cmd, false);
        }

        private SerializedEntityMessage CreateTestCommand(MessageTarget messageTarget)
        {
            var entityID = new Entity((Index)1, 0, false);
            var cmd = definition.CreateAuthorityRequest(
                entityID,
                new ClientID(1),
                AuthorityType.State
            );

            cmd.Target = messageTarget;

            return messageSerializer.SerializeCommand(cmd, false);
        }

        private byte[] GenerateByteArray(int length)
        {
            var byteArray = new byte[length];
            for (var i = 0; i < length; i++)
            {
                byteArray[i] = (byte)(i % 256);
            }
            return byteArray;
        }
    }
}
