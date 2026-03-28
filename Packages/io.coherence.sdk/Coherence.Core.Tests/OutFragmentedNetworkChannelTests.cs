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
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Coherence.Serializer.Fragmentation;
    using Coherence.SimulationFrame;
    using Coherence.Tests;
    using Moq;
    using NUnit.Framework;

    public class OutFragmentedNetworkChannelTests : CoherenceTest
    {
        private OutFragmentedNetworkChannel channel;

        private WrappedChannelMock wrappedChannelMock;
        private Mock<IFragmentationSerializer> fragmentationSerializerMock;

        private uint nextSendPacketSequenceId;
        private uint nextAckPacketSequenceId;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            this.wrappedChannelMock = new WrappedChannelMock();

            this.fragmentationSerializerMock = new Mock<IFragmentationSerializer>(MockBehavior.Strict);
            _ = this.fragmentationSerializerMock.Setup(f => f.FragmentSizeInBytes).Returns(1);
            _ = this.fragmentationSerializerMock.Setup(f => f.MaxChannelPacketID).Returns(7);
            _ = this.fragmentationSerializerMock.Setup(f => f.MaxInFlightChannelPackets).Returns(3);
            _ = this.fragmentationSerializerMock.Setup(f => f.NewEndOfChannelPacketsReservationScope(It.IsAny<SerializerContext<IOutBitStream>>()))
                .Returns((SerializerContext<IOutBitStream> ctx) => ctx.NewReservationScope(0));
            _ = this.fragmentationSerializerMock
                .Setup(f => f.WriteEndOfChannelPackets(It.IsAny<SerializerContext<IOutBitStream>>()));
            _ = this.fragmentationSerializerMock
                .Setup(f => f.GetHeaderSizeInBits(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns((uint index, uint count, bool includesLast, bool includesFO, bool includesSimFrame) => 0);

            this.channel = new OutFragmentedNetworkChannel(this.wrappedChannelMock, fragmentationSerializerMock.Object, logger);

            this.nextSendPacketSequenceId = 1;
            this.nextAckPacketSequenceId = 1;
        }

        private void Serialize(
            bool expectAnythingToSerialize = true,
            AbsoluteSimulationFrame? simulationFrame = null,
            Vector3d? floatingOrigin = null,
            int packetSizeBytes = 1000)
        {
            simulationFrame ??= 0;
            floatingOrigin ??= Vector3d.zero;

            var stream = new OutOctetStream(packetSizeBytes);
            var bitStream = new OutBitStream(stream);
            var context = new SerializerContext<IOutBitStream>(bitStream, false, logger);

            var serializedAnything = this.channel.Serialize(context, simulationFrame.Value, floatingOrigin.Value, false, null);

            Assert.That(serializedAnything, Is.EqualTo(expectAnythingToSerialize));
        }

        private void MarkAsSent()
        {
            _ = this.channel.MarkAsSent(nextSendPacketSequenceId++);
        }

        private void Delivery(bool acked)
        {
            var ackedEntities = new HashSet<Entity>();
            var ackedComponentsPerEntity = new Dictionary<Entity, HashSet<uint>>();

            this.channel.OnDeliveryInfo(nextAckPacketSequenceId++, acked, ref ackedEntities, ref ackedComponentsPerEntity);
        }

        private void SetupSerializeChannelPacketFragments(bool hasMoreSpace, bool serializeFully = true)
        {
            _ = this.fragmentationSerializerMock
                .Setup(f => f.SerializeChannelPacketFragments(It.IsAny<SerializerContext<IOutBitStream>>(), It.IsAny<AbsoluteSimulationFrame?>(),
                    It.IsAny<Vector3d?>(), It.IsAny<ChannelPacketID>(), It.IsAny<IOctetWriter>(),
                    It.IsAny<IReadOnlyList<FragmentSection>>(), It.IsAny<List<FragmentSection>>()))
                .Returns((SerializerContext<IOutBitStream> ctx, AbsoluteSimulationFrame? referenceSimulationFrame, Vector3d? floatingOrigin,
                    ChannelPacketID packetID, IOctetWriter channelPacket, IReadOnlyList<FragmentSection> pendingFragments, List<FragmentSection> serializedSections) =>
                    {
                        if (serializeFully)
                        {
                            serializedSections.AddRange(pendingFragments);
                        }
                        else
                        {
                            serializedSections.Add(new FragmentSection(pendingFragments[0].Index, 1));
                        }

                        return hasMoreSpace;
                    }
                );
        }

        private void VerifySerializeChannelPacketFragments(AbsoluteSimulationFrame? simFrame, Vector3d? floatingOrigin)
        {
            this.fragmentationSerializerMock
                .Verify(f => f.SerializeChannelPacketFragments(It.IsAny<SerializerContext<IOutBitStream>>(), simFrame,
                    floatingOrigin, It.IsAny<ChannelPacketID>(), It.IsAny<IOctetWriter>(),
                    It.IsAny<IReadOnlyList<FragmentSection>>(), It.IsAny<List<FragmentSection>>()), Times.Once);
        }

        [Test]
        [Description("Verifies that Reset fully resets the state of the channel and the wrapped channel.")]
        public void Test_Reset()
        {
            SetupSerializeChannelPacketFragments(hasMoreSpace: false, serializeFully: false);

            wrappedChannelMock.PendingDataBits = 100;

            Serialize(); // serialize only some fragments
            wrappedChannelMock.AssertSentSequenceQueue(1);

            channel.Reset();
            wrappedChannelMock.AssertSentSequenceQueue();

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(1); // after reset, the channel packet IDs start from 1 again
        }

        [Test]
        [Description("Verifies that nothing is serialized when there are no changes in the wrapped channel.")]
        public void Test_Serialize_NoChanges()
        {
            wrappedChannelMock.PendingDataBits = 0;

            Serialize(expectAnythingToSerialize: false);
        }

        [Test]
        [Description("Verifies that a new channel packet is created if the wrapped channel has pending data.")]
        public void Test_Serialize_NewChannelPacket()
        {
            wrappedChannelMock.PendingDataBits = 2100;

            SetupSerializeChannelPacketFragments(hasMoreSpace: false);

            Serialize();

            wrappedChannelMock.AssertSentSequenceQueue(1);
        }

        [Test]
        [Description("Verify that a new channel packet is created only if there is more space after serializing all fragments, " +
            "or if there is no pending channel packets at all.")]
        public void Test_SerializeAnother_WhenHasMoreSpace()
        {
            SetupSerializeChannelPacketFragments(hasMoreSpace: true);

            wrappedChannelMock.PendingDataBits = 1;

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(1);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(1, 2);

            MarkAsSent();
            Delivery(acked: true);
            wrappedChannelMock.AssertAcked(1, 2);
            wrappedChannelMock.AssertSentSequenceQueue();

            SetupSerializeChannelPacketFragments(hasMoreSpace: false);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(3); // no pending channel packets, so a new one is created

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(3); // the fragmentationSerializer says no more space, so no new channel packet is created
        }

        [Test]
        [Description("Verifies that the wrapped channel gets an ack only when all fragments of a channel packet are acked.")]
        public void Test_AckWhenAllFragmentsAcked()
        {
            SetupSerializeChannelPacketFragments(hasMoreSpace: false, serializeFully: false);

            wrappedChannelMock.PendingDataBits = 100;

            Serialize(); // serialize only some fragments
            wrappedChannelMock.AssertSentSequenceQueue(1);

            MarkAsSent();
            Delivery(acked: true);

            wrappedChannelMock.AssertAcked(); // not acked yet since we didn't serialize and ack all fragments
            wrappedChannelMock.AssertSentSequenceQueue(1);

            Serialize(); // serialize again only some fragments
            MarkAsSent();

            SetupSerializeChannelPacketFragments(hasMoreSpace: false, serializeFully: true);

            Serialize(); // serialize the rest of the fragments
            MarkAsSent();

            wrappedChannelMock.PendingDataBits = 0;

            Serialize(expectAnythingToSerialize: false); // wrapped channel has no more data to serialize and all fragments were sent

            Delivery(acked: false); // drop fragments from previous serialization
            Delivery(acked: true); // ack fragments from last serialization

            wrappedChannelMock.AssertAcked(); // not acked yet since we didn't serialize and ack all fragments
            wrappedChannelMock.AssertSentSequenceQueue(1);

            Serialize();
            MarkAsSent();
            Delivery(acked: true);

            wrappedChannelMock.AssertAcked(1); // now all fragments are acked
            wrappedChannelMock.AssertSentSequenceQueue();
        }

        [Test]
        [Description("Verifies that new channel packets are created only when there are less " +
            "than the maximum number of pending channel packets.")]
        public void Test_ChannelPacketIDs()
        {
            wrappedChannelMock.PendingDataBits = 1;

            SetupSerializeChannelPacketFragments(hasMoreSpace: true);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(1);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(1, 2);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(1, 2, 3);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(1, 2, 3); // no more space for new channel packets

            MarkAsSent();
            Delivery(acked: true);
            wrappedChannelMock.AssertSentSequenceQueue();
            wrappedChannelMock.AssertAcked(1, 2, 3);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(4);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(4, 5);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(4, 5, 6);

            Serialize();
            wrappedChannelMock.AssertSentSequenceQueue(4, 5, 6); // no more space for new channel packets

            MarkAsSent();
            Delivery(acked: false);
            wrappedChannelMock.AssertSentSequenceQueue(4, 5, 6); // the channel packets don't drop, the fragments will be resent
            wrappedChannelMock.AssertAcked(1, 2, 3);
            wrappedChannelMock.AssertDropped();
        }

        [Test]
        [Description("Verifies that the channel packet fragments are serialized with the correct simulation frame and floating origin.")]
        public void Test_SimFrameAndFloatingOrigin()
        {
            var simFrame1 = (AbsoluteSimulationFrame)1000;
            var floatingOrigin1 = new Vector3d(1, 2, 3);
            var simFrame2 = (AbsoluteSimulationFrame)2000;
            var floatingOrigin2 = new Vector3d(4, 5, 6);

            wrappedChannelMock.PendingDataBits = 100;

            SetupSerializeChannelPacketFragments(hasMoreSpace: false, serializeFully: false);

            Serialize(simulationFrame: simFrame1, floatingOrigin: floatingOrigin1); // this should not serialize simFrame nor FO
            VerifySerializeChannelPacketFragments(null, null);

            Serialize(simulationFrame: simFrame2, floatingOrigin: floatingOrigin1); // this should serialize only simFrame
            VerifySerializeChannelPacketFragments(simFrame1, null);

            Serialize(simulationFrame: simFrame2, floatingOrigin: floatingOrigin2); // this should serialize simFrame and FO
            VerifySerializeChannelPacketFragments(simFrame1, floatingOrigin1);
        }

        [Test]
        [Description("Verifies that old pending channel packets are dropped after fully acking a newer one.")]
        public void Test_DropOldAfterAcking()
        {
            wrappedChannelMock.PendingDataBits = 100;

            SetupSerializeChannelPacketFragments(hasMoreSpace: true);

            Serialize(); // fully serialize new channel packet
            MarkAsSent();

            Serialize(); // serialize another channel packet
            MarkAsSent();

            Serialize(); // serialize another channel packet
            MarkAsSent();

            wrappedChannelMock.AssertSentSequenceQueue(1, 2, 3);

            Delivery(acked: false); // drop first channel packet fragments

            wrappedChannelMock.AssertSentSequenceQueue(1, 2, 3); // still not marked as dropped as it will be resent

            Delivery(acked: true); // ack second channel packet fragments, this should also drop the first channel packet

            wrappedChannelMock.AssertAcked(2); // only the second channel packet is acked
            wrappedChannelMock.AssertDropped(1); // the first channel packet is dropped
            wrappedChannelMock.AssertSentSequenceQueue(3);
        }

        [Test]
        [Description("Verifies that the preferred max bit count is set correctly based on the packet and header size.")]
        public void Test_PreferredMaxBitCount()
        {
            // Arrange
            var packetSizeBytes = 600;
            var headerSizeBits = 50u;
            var fragmentCount = (packetSizeBytes * 8 - headerSizeBits) / 8;
            var expectedPreferredMaxBitCount = fragmentCount * 8;

            wrappedChannelMock.PendingDataBits = 1;
            SetupSerializeChannelPacketFragments(hasMoreSpace: false);

            _ = fragmentationSerializerMock.Setup(f => f.GetHeaderSizeInBits(0, It.IsAny<uint>(), true, false, false))
                .Returns<uint, uint, bool, bool, bool>((index, count, includesLast, includesFO, includesSimFrame) => headerSizeBits);

            // Act
            Serialize(packetSizeBytes: packetSizeBytes);

            // Assert
            Assert.That(wrappedChannelMock.LastPreferredMaxBitCount, Is.EqualTo(expectedPreferredMaxBitCount));
        }

        private class WrappedChannelMock : IOutNetworkChannel
        {
            public uint PendingDataBits { get; set; } = 0;
            public LinkedList<uint> SentSequenceQueue = new();
            public List<uint> AckedSequenceIDs = new();
            public List<uint> DroppedSequenceIDs = new();

            public uint LastPreferredMaxBitCount = 0;

            public event Action<Entity> OnEntityAcked { add { } remove { } }

            public bool HasChanges(IReadOnlyCollection<Entity> entities) => PendingDataBits > 0;

            public bool Serialize(SerializerContext<IOutBitStream> serializerCtx, AbsoluteSimulationFrame referenceSimulationFrame,
                Vector3d floatingOrigin, bool holdOnToCommands, IReadOnlyCollection<Entity> ackedEntities)
            {
                if (PendingDataBits == 0)
                {
                    return false;
                }

                LastPreferredMaxBitCount = serializerCtx.PreferredMaxBitCount;

                serializerCtx.BitStream.WriteBytesUnaligned(new byte[(PendingDataBits + 7) / 8], (int)PendingDataBits);

                return true;
            }

            public Dictionary<Entity, OutgoingEntityUpdate> MarkAsSent(uint sequenceId)
            {
                _ = SentSequenceQueue.AddLast(sequenceId);

                return null;
            }

            public void OnDeliveryInfo(uint sequenceId, bool wasDelivered, ref HashSet<Entity> ackedEntities, ref Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity)
            {
                var seq = SentSequenceQueue.First.Value;
                SentSequenceQueue.RemoveFirst();

                Assert.That(seq, Is.EqualTo(sequenceId));

                if (wasDelivered)
                {
                    AckedSequenceIDs.Add(sequenceId);
                }
                else
                {
                    DroppedSequenceIDs.Add(sequenceId);
                }
            }

            public void Reset()
            {
                SentSequenceQueue.Clear();
                AckedSequenceIDs.Clear();
                DroppedSequenceIDs.Clear();
            }

            public void AssertSentSequenceQueue(params uint[] sequenceIds)
            {
                var sentSequenceNode = SentSequenceQueue.First;
                foreach (var sequenceId in sequenceIds)
                {
                    Assert.That(sentSequenceNode.Value, Is.EqualTo(sequenceId));
                    sentSequenceNode = sentSequenceNode.Next;
                }

                Assert.That(sentSequenceNode, Is.Null);
            }

            public void AssertAcked(params uint[] sequenceIds)
            {
                Assert.That(AckedSequenceIDs, Is.EquivalentTo(sequenceIds));
            }

            public void AssertDropped(params uint[] sequenceIds)
            {
                Assert.That(DroppedSequenceIDs, Is.EquivalentTo(sequenceIds));
            }

            public void CreateEntity(Entity id, ICoherenceComponentData[] data) => throw new NotImplementedException();
            public void UpdateComponents(Entity id, ICoherenceComponentData[] data) => throw new NotImplementedException();
            public void RemoveComponents(Entity id, uint[] componentTypes, Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity) => throw new NotImplementedException();
            public void DestroyEntity(Entity id, IReadOnlyCollection<Entity> ackedEntities) => throw new NotImplementedException();
            public void PushCommand(IEntityCommand command, bool useDebugStreams) => throw new NotImplementedException();
            public void PushInput(IEntityInput message, bool useDebugStreams) => throw new NotImplementedException();
            public bool HasChangesForEntity(Entity entity) => throw new NotImplementedException();
            public void ClearAllChangesForEntity(Entity entity) => throw new NotImplementedException();

            public void ClearLastSerializationResult() => throw new NotImplementedException();
        }
    }
}
