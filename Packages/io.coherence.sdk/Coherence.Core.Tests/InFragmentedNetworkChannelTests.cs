// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common;
    using Coherence.Core.Channels;
    using Coherence.Log;
    using Coherence.Serializer;
    using Coherence.Serializer.Fragmentation;
    using Coherence.SimulationFrame;
    using Coherence.Tests;
    using Moq;
    using NUnit.Framework;

    public class InFragmentedNetworkChannelTests : CoherenceTest
    {
        private readonly ChannelPacketID channelPacket1 = new ChannelPacketID(1, 15);
        private readonly ChannelPacketID channelPacket2 = new ChannelPacketID(2, 15);
        private readonly FragmentSection fragmentSection1 = new FragmentSection(0, 15);
        private readonly FragmentSection fragmentSection2 = new FragmentSection(15, 5);

        private InFragmentedNetworkChannel channel;

        private Mock<IInNetworkChannel> wrappedChannelMock;
        private FragmentationSerializerMock fragmentationSerializerMock;

        private Vector3d currentFloatingOrigin;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            this.wrappedChannelMock = new Mock<IInNetworkChannel>(MockBehavior.Strict);
            _ = this.wrappedChannelMock.Setup(c => c.Deserialize(It.IsAny<IInBitStream>(), It.IsAny<AbsoluteSimulationFrame>(),
                It.IsAny<Vector3d>())).Returns(false);

            this.fragmentationSerializerMock = new FragmentationSerializerMock();

            this.channel = new InFragmentedNetworkChannel(this.wrappedChannelMock.Object, fragmentationSerializerMock, logger);

            this.currentFloatingOrigin = Vector3d.zero;
        }

        private void Deserialize(bool expectGotEntityUpdate = false, AbsoluteSimulationFrame? simFrame = null, Vector3d? floatingOrigin = null)
        {
            var stream = new InOctetStream(new byte[1000]);
            var bitStream = new InBitStream(stream, (int)stream.Length * 8);

            var gotEntityUpdate = channel.Deserialize(bitStream, simFrame ?? (AbsoluteSimulationFrame)0, floatingOrigin ?? Vector3d.zero);

            Assert.That(gotEntityUpdate, Is.EqualTo(expectGotEntityUpdate));
        }

        private void VerifyPushed(AbsoluteSimulationFrame? simFrame = null, Vector3d? floatingOrigin = null, Times? times = null)
        {
            wrappedChannelMock.Verify(x => x.Deserialize(It.IsAny<IInBitStream>(),
                simFrame ?? (AbsoluteSimulationFrame)0, floatingOrigin ?? Vector3d.zero), times ?? Times.Once());

            wrappedChannelMock.Invocations.Clear();
        }

        [Test]
        [Description("Verifies that getting all fragments at once pushes the completed packet to the channel.")]
        public void Test_GettingFullPacket_PushesToChannel()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: true);

            Deserialize();

            VerifyPushed();
        }

        [Test]
        [Description("Verifies that getting all fragments of multiple packets at once pushes all packets to the channel.")]
        public void Test_GettingMultipleFullPackets_PushesToChannel()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: true);
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket2, fragmentSection1, isLast: true);

            Deserialize();

            VerifyPushed(times: Times.Exactly(2));
        }

        [Test]
        [Description("Verifies that getting only some fragments does not push to the channel until all fragments are received.")]
        public void Test_GettingPartialPacket_DoesNotPushToChannel()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: false);

            Deserialize();

            VerifyPushed(times: Times.Never());

            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection2, isLast: true);

            Deserialize();

            VerifyPushed();
        }

        [Test]
        [Description("Verifies that getting all fragments separately pushes the complete packet to the channel.")]
        public void Test_GettingAllPartials_PushesToChannel()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: false);
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection2, isLast: true);

            Deserialize();

            VerifyPushed();
        }

        [Test]
        [Description("Verifies that getting multiple partial packets (only some fragments) does not push until all fragments are received.")]
        public void Test_GettingMultiplePartialPackets_DoesNotPushUntilComplete()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: false);
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket2, fragmentSection1, isLast: false);

            Deserialize();

            VerifyPushed(times: Times.Never());

            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection2, isLast: true);
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket2, fragmentSection2, isLast: true);

            Deserialize();

            VerifyPushed(times: Times.Exactly(2));
        }

        [Test]
        [Description("Verifies that getting a complete packet after a partial packet pushes the complete packet to the channel, " +
            "and drops the older pending packet.")]
        public void Test_GettingCompleteNewer_DropsOlderPacket()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: false);
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket2, fragmentSection1, isLast: false);

            Deserialize();

            VerifyPushed(times: Times.Never());

            fragmentationSerializerMock.AddToReceiveQueue(channelPacket2, fragmentSection2, isLast: true);

            Deserialize();
            VerifyPushed();

            // The first packet was dropped, so this data should be ignored since it's not a valid successor anymore
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: true);

            Deserialize();

            VerifyPushed(times: Times.Never());
        }

        [Test]
        [Description("Verifies that getting all fragments in the wrong order pushes the complete packet to the channel")]
        public void Test_GettingAllPartialsInWrongOrder_PushesToChannel()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection2, isLast: true);

            Deserialize();

            VerifyPushed(times: Times.Never());

            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: false);

            Deserialize();

            VerifyPushed();
        }

        [Test]
        [Description("Verifies that getting the simulationFrame and floatingOrigin with the first fragment " +
            "correctly pushes the received simFrame and FO.")]
        public void Test_GettingSimFrameAndFOWithFirstFragment_ShouldPushToChannel()
        {
            var simFrame = (AbsoluteSimulationFrame)123;
            var floatingOrigin = new Vector3d(4, 4, 4);

            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: true, simFrame, floatingOrigin);

            Deserialize();

            VerifyPushed(simFrame, floatingOrigin);
        }

        [Test]
        [Description("Verifies that getting the channel packets out of order, and the older channel packet gets " +
            "fully acked first, that the newer channel packet can get fully acked and pushed too.")]
        public void Test_OutOfOrder_BothReceived()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket2, fragmentSection1, isLast: false);

            Deserialize();
            VerifyPushed(times: Times.Never());

            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: true);

            Deserialize();
            VerifyPushed(times: Times.Once());

            fragmentationSerializerMock.AddToReceiveQueue(channelPacket2, fragmentSection2, isLast: true);

            Deserialize();
            VerifyPushed(times: Times.Once());
        }

        [Test]
        [Description("Verifies that getting the channel packets out of order, and the newer channel packet gets " +
            "fully acked before the older packet is received, that the older channel packet is dropped.")]
        public void Test_OutOfOrder_NewerReceived_OlderDropped()
        {
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket2, fragmentSection1, isLast: true);

            Deserialize();
            VerifyPushed(times: Times.Once());

            // This one should be dropped, since the newer channel packet was fully acked already
            fragmentationSerializerMock.AddToReceiveQueue(channelPacket1, fragmentSection1, isLast: true);

            Deserialize();
            VerifyPushed(times: Times.Never());
        }

        private class FragmentationSerializerMock : IFragmentationSerializer
        {
            public Queue<(ChannelPacketID id, FragmentSection fragmentSection,
                bool isLast, AbsoluteSimulationFrame? simFrame, Vector3d? floatingOrigin)> ReceiveQueue = new();

            public uint FragmentSizeInBytes => throw new NotImplementedException();
            public uint MaxChannelPacketID => 15;
            public uint MaxInFlightChannelPackets => throw new NotImplementedException();

            public void AddToReceiveQueue(
                ChannelPacketID id, FragmentSection fragmentSection, bool isLast,
                AbsoluteSimulationFrame? simFrame = null, Vector3d? floatingOrigin = null
            )
            {
                ReceiveQueue.Enqueue((id, fragmentSection, isLast, simFrame, floatingOrigin));
            }

            public ChannelPacketID ReadChannelPacketID(IInBitStream stream)
            {
                if (ReceiveQueue.Count == 0)
                {
                    return new ChannelPacketID(0, 15);
                }

                return ReceiveQueue.Peek().id;
            }

            public FragmentSection DeserializeChannelPacketFragments(IInBitStream stream, IOutOctetStream channelPacket,
                Logger logger, out bool receivedLast, out bool receivedFirst,
                out AbsoluteSimulationFrame? referenceSimulationFrame, out Vector3d? floatingOrigin)
            {
                if (ReceiveQueue.Count == 0)
                {
                    throw new InvalidOperationException("No fragments to deserialize.");
                }

                var next = ReceiveQueue.Dequeue();

                receivedLast = next.isLast;
                receivedFirst = next.fragmentSection.Index == 0;

                referenceSimulationFrame = next.simFrame;
                floatingOrigin = next.floatingOrigin;

                return next.fragmentSection;
            }

            public uint GetHeaderSizeInBits(uint fragmentIndex, uint fragmentCount, bool includesLastFragment, bool includesSimFrame, bool includesFO) => throw new NotImplementedException();

            public SerializerContext<IOutBitStream>.ReservationScope NewEndOfChannelPacketsReservationScope(SerializerContext<IOutBitStream> ctx) => throw new NotImplementedException();
            public bool SerializeChannelPacketFragments(SerializerContext<IOutBitStream> ctx, AbsoluteSimulationFrame? referenceSimulationFrame, Vector3d? floatingOrigin, ChannelPacketID packetID, IOctetWriter channelPacket, IReadOnlyList<FragmentSection> pendingFragments, List<FragmentSection> serializedSections) => throw new NotImplementedException();

            public void WriteEndOfChannelPackets(SerializerContext<IOutBitStream> ctx) => throw new NotImplementedException();
        }
    }
}
