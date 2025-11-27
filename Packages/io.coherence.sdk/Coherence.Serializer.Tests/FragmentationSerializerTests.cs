// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common;
    using Coherence.Serializer.Fragmentation;
    using Coherence.SimulationFrame;
    using Coherence.Tests;
    using NUnit.Framework;

    public class FragmentationSerializerTests : CoherenceTest
    {
        private const uint channelPacketIDSizeInBits = 4;
        private FragmentationSerializer serializer;

        private ChannelPacketID defaultChannelPacketID;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            serializer = new FragmentationSerializer(1, channelPacketIDSizeInBits);
            defaultChannelPacketID = new ChannelPacketID(1, serializer.MaxChannelPacketID);
        }

        private (SerializerContext<IOutBitStream>, OutOctetStream octetStream) NewCtx(int size)
        {
            var outStream = new OutOctetStream(size);
            var outBitStream = new OutBitStream(outStream);

            return (new SerializerContext<IOutBitStream>(outBitStream, false, this.logger), outStream);
        }

        private InBitStream CloseCtx(SerializerContext<IOutBitStream> ctx, OutOctetStream outPacket)
        {
            ctx.BitStream.Flush();

            var inPacket = new InOctetStream(outPacket.Close());
            var inBitStream = new InBitStream(inPacket, (int)inPacket.Length * 8);

            return inBitStream;
        }

        private IOutOctetStream CreateChannelPacket(int size, bool fill)
        {
            var outStream = new OutOctetStream(size);

            if (fill)
            {
                for (var i = 0; i < size; i++)
                {
                    outStream.WriteOctet((byte)(i % (byte.MaxValue + 1)));
                }
            }

            return outStream;
        }

        private void AssertStreamsEqual(IOutOctetStream expected, IOutOctetStream actual, uint offset = 0, int count = -1)
        {
            if (count < 0)
            {
                count = (int)(expected.Octets.Length - offset);
            }

            if (offset >= expected.Octets.Length || offset + count > expected.Octets.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset and count must be within the bounds of the octet stream.");
            }

            for (var i = (int)offset; i < offset + count; i++)
            {
                Assert.That(actual.Octets[i], Is.EqualTo(expected.Octets[i]),
                    $"Octet at index {i} does not match: expected {expected.Octets[i]}, got {actual.Octets[i]}");
            }
        }

        private void AssertStreamsEqual(IOutOctetStream expected, IOutOctetStream actual, List<FragmentSection> sections)
        {
            foreach (var section in sections)
            {
                AssertStreamsEqual(expected, actual, section.Index, (int)section.Count);
            }
        }

        public void SerializePacketFragments(SerializerContext<IOutBitStream> ctx, IOutOctetStream channelPacket,
            List<FragmentSection> pendingFragments, List<FragmentSection> serializedSections,
            AbsoluteSimulationFrame? simFrame = null, Vector3d? floatingOrigin = null,
            ChannelPacketID? channelPacketID = null, bool? hasMoreSpace = null)
        {
            channelPacketID ??= defaultChannelPacketID;

            var serializedSectionsGot = new List<FragmentSection>();

            var hasMoreSpaceGot = serializer.SerializeChannelPacketFragments(ctx, simFrame, floatingOrigin, channelPacketID.Value, channelPacket,
                pendingFragments, serializedSectionsGot);

            if (hasMoreSpace != null)
            {
                Assert.That(hasMoreSpaceGot, Is.EqualTo(hasMoreSpace.Value));
            }

            Assert.That(serializedSectionsGot.Count, Is.EqualTo(serializedSections.Count));
            for (var i = 0; i < serializedSections.Count; i++)
            {
                Assert.That(serializedSectionsGot[i].Index, Is.EqualTo(serializedSections[i].Index),
                    $"Serialized section {i} index does not match: expected {serializedSections[i].Index}, got {serializedSectionsGot[i].Index}");
                Assert.That(serializedSectionsGot[i].Count, Is.EqualTo(serializedSections[i].Count),
                    $"Serialized section {i} count does not match: expected {serializedSections[i].Count}, got {serializedSectionsGot[i].Count}");
            }
        }

        private void ExpectChannelPacketID(InBitStream stream, ChannelPacketID? id = null)
        {
            id ??= defaultChannelPacketID;

            var gotChannelPacketID = serializer.ReadChannelPacketID(stream);

            Assert.That(gotChannelPacketID, Is.EqualTo(id.Value));
        }

        private void ExpectPacketFragments(InBitStream stream, IOutOctetStream channelPacket,
            bool? receivedLast = null, bool? receivedFirst = null,
            AbsoluteSimulationFrame? simFrame = null, Vector3d? floatingOrigin = null,
            FragmentSection? fragmentSection = null)
        {
            var deserializedFragmentSection = serializer.DeserializeChannelPacketFragments(stream, channelPacket, logger,
                out var receivedLastGot, out var receivedFirstGot, out var simFrameGot, out var floatingOriginGot);

            if (receivedLast != null)
            {
                Assert.That(receivedLastGot, Is.EqualTo(receivedLast.Value));
            }

            if (receivedFirst != null)
            {
                Assert.That(receivedFirstGot, Is.EqualTo(receivedFirst.Value));
            }

            Assert.That(simFrameGot, Is.EqualTo(simFrame));
            Assert.That(floatingOriginGot, Is.EqualTo(floatingOrigin));

            if (fragmentSection != null)
            {
                Assert.That(deserializedFragmentSection.Index, Is.EqualTo(fragmentSection.Value.Index));
                Assert.That(deserializedFragmentSection.Count, Is.EqualTo(fragmentSection.Value.Count));
            }
        }

        private uint HeaderSize(uint fragmentIndex, uint fragmentCount, bool includesSimFrameAndFO)
        {
            var firstFragmentExtraHeader = 0u;
            if (fragmentIndex == 0)
            {
                firstFragmentExtraHeader += 2;
                if (includesSimFrameAndFO)
                {
                    firstFragmentExtraHeader += Serialize.NUM_BITS_FOR_SIMFRAME;
                    firstFragmentExtraHeader += Serialize.NUM_BITS_FOR_FLOATING_ORIGIN;
                }
            }

            return channelPacketIDSizeInBits +
                    SerializeTools.WriteVarIntSize(fragmentIndex, FragmentationSerializer.FragmentIndexVarIntDefinition) +
                    SerializeTools.WriteVarIntSize(fragmentCount, FragmentationSerializer.FragmentCountVarIntDefinition) +
                    1u + // 1 bit for last fragment flag
                    firstFragmentExtraHeader;
        }

        private uint HeaderSizeBytes(uint fragmentIndex, uint fragmentCount, bool includesSimFrameAndFO)
        {
            return (HeaderSize(fragmentIndex, fragmentCount, includesSimFrameAndFO) + 7) / 8; // round up to nearest byte
        }

        private uint HeaderSizesBytes(bool includesSimFrameAndFO, params (uint fragmentIndex, uint fragmentCount)[] fragmentSections)
        {
            var result = 0u;
            foreach (var (fragmentIndex, fragmentCount) in fragmentSections)
            {
                result += HeaderSize(fragmentIndex, fragmentCount, includesSimFrameAndFO);
            }

            return (result + 7) / 8; // round up to nearest byte
        }

        [Test]
        [Description("Verifies that a whole channel packet that fully fits inside the packet is serialized correctly.")]
        public void Test_ChannelPacketFullyFits_Whole([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(64000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(0, 40000) };
            var serializedSections = pendingFragments;
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: true);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: true, receivedFirst: true,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                fragmentSection: serializedSections[0]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that the last section of a channel packet that fully fits inside the packet is serialized correctly.")]
        public void Test_ChannelPacketFullyFits_LastSection([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(20000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(39000, 1000) };
            var serializedSections = pendingFragments;
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: true);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: true, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[0]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that the first section of a channel packet that fully fits inside the packet is serialized correctly.")]
        public void Test_ChannelPacketFullyFits_FirstSection([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(20000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(0, 1000) };
            var serializedSections = pendingFragments;
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: true);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: true,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                fragmentSection: serializedSections[0]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that the first and last sections of a channel packet that fully fit inside the packet are serialized correctly.")]
        public void Test_ChannelPacketFullyFits_FirstAndLastSections([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(20000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(0, 1000), new(39000, 1000) };
            var serializedSections = pendingFragments;
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: true);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: true,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                fragmentSection: serializedSections[0]);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: true, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[1]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that multiple sections of a channel packet that fully fit inside the packet are serialized correctly.")]
        public void Test_ChannelPacketFullyFits_MultipleSections([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(20000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(0, 1000), new(3000, 100), new(4000, 151), new(18000, 69) };
            var serializedSections = pendingFragments;
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: true);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: true,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                fragmentSection: serializedSections[0]);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[1]);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[2]);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[3]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that a channel packet that partially fits inside the packet is serialized correctly.")]
        public void Test_ChannelPacketPartiallyFits_Whole([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(20000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(0, 40000) };
            var serializedSections = new List<FragmentSection>() { new(0, 20000 - HeaderSizeBytes(0, 20000, includeSimFrameAndFO)) };
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: false);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: true,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                fragmentSection: serializedSections[0]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that the first section of a channel packet that partially fits inside the packet is serialized correctly.")]
        public void Test_ChannelPacketPartiallyFits_FirstSection([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(1000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(0, 2000) };
            var serializedSections = new List<FragmentSection>() { new(0, 1000 - HeaderSizeBytes(0, 1000, includeSimFrameAndFO)) };
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: false);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: true,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                fragmentSection: serializedSections[0]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that the last section of a channel packet that partially fits inside the packet is serialized correctly.")]
        public void Test_ChannelPacketPartiallyFits_LastSection([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(1000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(38000, 2000) };
            var serializedSections = new List<FragmentSection>() { new(38000, 1000 - HeaderSizeBytes(38000, 1000, includeSimFrameAndFO)) };
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: false);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[0]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that the first and last sections of a channel packet that partially fit inside the packet are serialized correctly.")]
        public void Test_ChannelPacketPartiallyFits_FirstAndLastSection([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(2000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() {
                new(0, 1000),
                new(38000, 2000),
            };
            var serializedSections = new List<FragmentSection>() {
                new(0, 1000),
                new(38000, 1000 - HeaderSizesBytes(includeSimFrameAndFO, (0, 1000), (38000, 1000))),
            };
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: false);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: true,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                fragmentSection: serializedSections[0]);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[1]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [Test]
        [Description("Verifies that multiple sections of a channel packet that partially fit inside the packet are serialized correctly.")]
        public void Test_ChannelPacketPartiallyFits_MultipleSections([Values(true, false)] bool includeSimFrameAndFO)
        {
            var (ctx, outPacket) = NewCtx(5000);
            var outChannelPacket = CreateChannelPacket(40000, fill: true);
            var pendingFragments = new List<FragmentSection>() {
                new(0, 1000),
                new(2000, 199),
                new(6969, 3000),
                new(30000, 2500),
            };
            var lastSectionCount = 5000u - 1000 - 199 - 3000;
            var serializedSections = new List<FragmentSection>() {
                new(0, 1000),
                new(2000, 199),
                new(6969, 3000),
                new(30000, lastSectionCount - HeaderSizesBytes(includeSimFrameAndFO, (0, 1000), (2000, 199), (6969, 3000), (30000, lastSectionCount))),
            };
            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            SerializePacketFragments(ctx, outChannelPacket, pendingFragments,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                serializedSections: serializedSections, hasMoreSpace: false);

            var inBitStream = CloseCtx(ctx, outPacket);
            var inChannelPacket = CreateChannelPacket(40000, fill: false);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: true,
                simFrame: simFrame, floatingOrigin: floatingOrigin,
                fragmentSection: serializedSections[0]);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[1]);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[2]);

            ExpectChannelPacketID(inBitStream);

            ExpectPacketFragments(inBitStream, inChannelPacket,
                receivedLast: false, receivedFirst: false,
                simFrame: null, floatingOrigin: null, // we don't receive simFrame and FO for non-first fragments
                fragmentSection: serializedSections[3]);

            AssertStreamsEqual(outChannelPacket, inChannelPacket, serializedSections);
        }

        [TestCase(1000, 10, true, true)]
        [TestCase(20, 20 * 8, false, false)]
        [TestCase(20, 30 * 8, false, false)]
        [TestCase((Serialize.NUM_BITS_FOR_SIMFRAME + Serialize.NUM_BITS_FOR_FLOATING_ORIGIN + 10) / 8, 10, true, false)]
        [TestCase(1, 10, true, false)]
        [Description("Validates that the remainingBitCount calculation in SerializeChannelPacketFragments does not overflow.")]
        public void Test_ChannelPacketRemainingBitCount(int contextSize, int reservedBits, bool includeSimFrameAndFO, bool expectSerializedData)
        {
            // Arrange
            var (ctx, _) = NewCtx(contextSize);
            ctx.ReserveBits((uint)reservedBits);

            // Create a channel packet and fragments
            var outChannelPacket = CreateChannelPacket(100, fill: true);
            var pendingFragments = new List<FragmentSection>() { new(0, 100) };
            var serializedSections = new List<FragmentSection>();

            AbsoluteSimulationFrame? simFrame = includeSimFrameAndFO ? (AbsoluteSimulationFrame)1234 : null;
            Vector3d? floatingOrigin = includeSimFrameAndFO ? new Vector3d(1, 2, 3) : null;

            // Act
            var hasMoreChanges = serializer.SerializeChannelPacketFragments(ctx, simFrame, floatingOrigin,
                defaultChannelPacketID, outChannelPacket, pendingFragments, serializedSections);

            // Assert
            if (expectSerializedData)
            {
                Assert.That(serializedSections, Is.Not.Empty);
                Assert.That(hasMoreChanges, Is.True);
            }
            else
            {
                Assert.That(serializedSections, Is.Empty);
                Assert.That(hasMoreChanges, Is.False);
            }
        }

        [Test]
        [Description("Verifies that the GetHeaderSizeInBits method returns the correct result.")]
        public void Test_HeaderSizeInBits(
            [Values(0, 1, 10, 300, 6000, 80000)] int fragmentIndex,
            [Values(0, 1, 10, 300, 6000, 80000)] int fragmentCount,
            [Values(true, false)] bool includesSimFrameAndFO)
        {
            // Arrange
            var expectedSize = HeaderSize((uint)fragmentIndex, (uint)fragmentCount, includesSimFrameAndFO);

            // Act
            var actualSize = serializer.GetHeaderSizeInBits((uint)fragmentIndex, (uint)fragmentCount, false, includesSimFrameAndFO, includesSimFrameAndFO);

            // Assert
            Assert.That(actualSize, Is.EqualTo(expectedSize));
        }
    }
}
