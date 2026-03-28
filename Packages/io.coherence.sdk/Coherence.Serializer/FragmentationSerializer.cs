// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer.Fragmentation
{
    using System;
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Common;
    using Coherence.Log;
    using Coherence.SimulationFrame;

    public interface IFragmentationSerializer
    {
        uint FragmentSizeInBytes { get; }
        uint MaxChannelPacketID { get; }
        uint MaxInFlightChannelPackets { get; }

        bool SerializeChannelPacketFragments(
            SerializerContext<IOutBitStream> ctx,
            AbsoluteSimulationFrame? referenceSimulationFrame,
            Vector3d? floatingOrigin,
            ChannelPacketID packetID,
            IOctetWriter channelPacket,
            IReadOnlyList<FragmentSection> pendingFragments,
            List<FragmentSection> serializedSections);

        uint GetHeaderSizeInBits(uint fragmentIndex, uint fragmentCount, bool includesLastFragment, bool includesSimFrame, bool includesFO);

        SerializerContext<IOutBitStream>.ReservationScope NewEndOfChannelPacketsReservationScope(SerializerContext<IOutBitStream> ctx);
        void WriteEndOfChannelPackets(SerializerContext<IOutBitStream> ctx);

        ChannelPacketID ReadChannelPacketID(IInBitStream stream);
        FragmentSection DeserializeChannelPacketFragments(
            IInBitStream stream,
            IOutOctetStream channelPacket,
            Logger logger,
            out bool receivedLast,
            out bool receivedFirst,
            out AbsoluteSimulationFrame? referenceSimulationFrame,
            out Vector3d? floatingOrigin);
    }

    public class FragmentationSerializer : IFragmentationSerializer
    {
        // Use 0 bits to support index value 0 - by far the most common value - especially if the whole channel packet fits in a single packet.
        // Use 10 bits to support up to 1023 fragments, which is possible when a relatively small channel packet splits into multiple packets.
        // Use 3 more bits to support up to 8191 fragments, which is a reasonably sized channel packet.
        // Use 3 more bits to support up to 65535 fragments, which is a very large channel packet.
        // Fill the remaining 16 bits, to support full uint32.
        public static readonly VarIntDefinition FragmentIndexVarIntDefinition = new(new byte[] { 0, 10, 3, 3, 16 }, false);

        // Use 9 bits to support up to 512 fragments at once, which is the most common case of partially filling a packet with fragments.
        // Use 2 more bits to support up to 2047 fragments at once, which covers the default MTU of 1280 bytes (fragments).
        // Use 2 more bits to support up to 8191 fragments at once, in case a larger MTU is used.
        // Use 2 more bits to support up to 32767 fragments at once, which covers the max MTU.
        // Fill the remaining 17 bits, to support full uint32.
        public static readonly VarIntDefinition FragmentCountVarIntDefinition = new(new byte[] { 9, 2, 2, 2, 17 }, false);

        public uint FragmentSizeInBytes => fragmentSizeInBytes;
        public uint MaxChannelPacketID => (uint)(1 << (int)channelPacketIDSizeInBits) - 1;
        public uint MaxInFlightChannelPackets => (uint)(1 << ((int)channelPacketIDSizeInBits - 1) - 1);

        private readonly uint fragmentSizeInBytes;
        private readonly uint channelPacketIDSizeInBits;
        private readonly uint lastFragmentSizeBits; // Number of bits required to serialize the last fragment size.

        private byte[] bufferCache;

        /// <param name="fragmentSizeInBytes">The number of bytes each fragment contains. Since consecutive fragments share a single header,
        /// having each fragment be a single byte doesn't increase bandwidth significantly, but increases the chance of fitting more fragments into a packet.</param>
        /// <param name="channelPacketIDSizeInBits">The number of bits used for identifying which in-flight channel packet is serialized.
        /// The maximum number of in-flight channel packets is 2^(channelPacketIDSizeInBits-1)-1, because the in-flight window
        /// must be smaller than the maximum value of the channel packet ID.
        /// When 2^(channelPacketIDSizeInBits-1)-1 channel packets are in-flight, the fragmented channel waits for a channel packet to be acked before
        /// serializing another channel packet from the wrappedChannel.</param>
        public FragmentationSerializer(uint fragmentSizeInBytes = 1, uint channelPacketIDSizeInBits = 5)
        {
            this.fragmentSizeInBytes = fragmentSizeInBytes;
            this.channelPacketIDSizeInBits = channelPacketIDSizeInBits;
            this.lastFragmentSizeBits = (uint)Math.Ceiling(Math.Log(fragmentSizeInBytes, 2));
        }

        public bool SerializeChannelPacketFragments(
            SerializerContext<IOutBitStream> ctx,
            AbsoluteSimulationFrame? referenceSimulationFrame,
            Vector3d? floatingOrigin,
            ChannelPacketID channelPacketID,
            IOctetWriter channelPacket,
            IReadOnlyList<FragmentSection> pendingFragments,
            List<FragmentSection> serializedSections)
        {
            ctx.StartSection("ChannelPacketFragments");

            foreach (var fragmentSection in pendingFragments)
            {
                var totalFragmentCount = (uint)Math.Ceiling((double)channelPacket.Position / fragmentSizeInBytes);

                var fragmentCountToSerialize = GetNumberOfFragmentsToFit(
                    fragmentSection.Index,
                    fragmentSection.Count,
                    ctx.RemainingBitCount,
                    totalFragmentCount,
                    referenceSimulationFrame != null,
                    referenceSimulationFrame != null);

                if (fragmentCountToSerialize > 0)
                {
                    ctx.BitStream.WriteBits(channelPacketID, (int)channelPacketIDSizeInBits);
                    _ = SerializeTools.WriteVarInt(ctx.BitStream, fragmentSection.Index, FragmentIndexVarIntDefinition);
                    _ = SerializeTools.WriteVarInt(ctx.BitStream, fragmentCountToSerialize, FragmentCountVarIntDefinition);

                    var isLastFragment = DoesContainLastFragment(fragmentSection.Index, fragmentCountToSerialize, totalFragmentCount);
                    ctx.BitStream.WriteBits(isLastFragment ? 1u : 0u, 1);

                    var startOctet = fragmentSection.Index * fragmentSizeInBytes;
                    var octetCount = fragmentCountToSerialize * fragmentSizeInBytes;

                    if (isLastFragment && FragmentSizeInBytes > 1)
                    {
                        // Since the channel packet size maybe isn't a multiple of the fragment size,
                        // the last fragment might not be full, so we also serialize the size of the last fragment.
                        var lastFragmentSize = channelPacket.Position % fragmentSizeInBytes;
                        ctx.BitStream.WriteBits(lastFragmentSize, (int)lastFragmentSizeBits);

                        octetCount = octetCount - FragmentSizeInBytes + lastFragmentSize;
                    }

                    if (fragmentSection.Index == 0)
                    {
                        WriteSimFrameAndFloatingOrigin(ctx, referenceSimulationFrame, floatingOrigin);
                    }

                    var fragmentsData = channelPacket.Octets.Slice((int)startOctet, (int)octetCount);
                    ctx.BitStream.WriteBytesUnaligned(fragmentsData, (int)octetCount * 8);

                    serializedSections.Add(new FragmentSection(fragmentSection.Index, fragmentCountToSerialize));
                }

                // This fragment section didn't fully fit, there's no way the next one will fit either.
                if (fragmentCountToSerialize < fragmentSection.Count)
                {
                    ctx.EndSection();

                    return false;
                }
            }

            ctx.EndSection();

            return true;
        }

        public uint GetHeaderSizeInBits(uint fragmentIndex, uint fragmentCount, bool includesLastFragment, bool includesSimFrame, bool includesFO)
        {
            var size = channelPacketIDSizeInBits +
                    SerializeTools.WriteVarIntSize(fragmentIndex, FragmentIndexVarIntDefinition) +
                    SerializeTools.WriteVarIntSize(fragmentCount, FragmentCountVarIntDefinition) +
                    1; // 1 bit for last fragment flag

            if (fragmentIndex == 0)
            {
                size += SimFrameAndFloatingOriginSizeInBits(includesSimFrame, includesFO);
            }

            if (FragmentSizeInBytes > 1 && includesLastFragment)
            {
                size += lastFragmentSizeBits;
            }

            return size;
        }

        public SerializerContext<IOutBitStream>.ReservationScope NewEndOfChannelPacketsReservationScope(SerializerContext<IOutBitStream> ctx)
            => ctx.NewReservationScope(channelPacketIDSizeInBits);

        public void WriteEndOfChannelPackets(SerializerContext<IOutBitStream> ctx)
        {
            ctx.BitStream.WriteBits(ChannelPacketID.EndOfChannelPackets, (int)channelPacketIDSizeInBits);
        }

        public ChannelPacketID ReadChannelPacketID(IInBitStream stream)
        {
            var value = stream.ReadBits((int)channelPacketIDSizeInBits);

            return new ChannelPacketID(value, MaxChannelPacketID);
        }

        public FragmentSection DeserializeChannelPacketFragments(
            IInBitStream stream,
            IOutOctetStream channelPacket,
            Logger logger,
            out bool receivedLast,
            out bool receivedFirst,
            out AbsoluteSimulationFrame? referenceSimulationFrame,
            out Vector3d? floatingOrigin)
        {
            referenceSimulationFrame = null;
            floatingOrigin = null;

            var fragmentIndex = DeserializerTools.ReadVarInt(stream, FragmentIndexVarIntDefinition);
            var fragmentCount = DeserializerTools.ReadVarInt(stream, FragmentCountVarIntDefinition);
            var receivedLastValue = stream.ReadBits(1);

            receivedLast = receivedLastValue > 0;
            receivedFirst = fragmentIndex == 0;

            var lastFragmentSizeInBytes = fragmentSizeInBytes;

            if (receivedLast && FragmentSizeInBytes > 1)
            {
                lastFragmentSizeInBytes = stream.ReadBits((int)lastFragmentSizeBits);
            }

            if (receivedFirst)
            {
                (referenceSimulationFrame, floatingOrigin) = ReadSimFrameAndFloatingOrigin(stream, logger);
            }

            var startOctet = fragmentIndex * fragmentSizeInBytes;
            var octetCount = (fragmentCount - 1) * fragmentSizeInBytes + lastFragmentSizeInBytes;

            if (bufferCache == null || bufferCache.Length < octetCount)
            {
                bufferCache = new byte[octetCount];
            }

            stream.ReadBytesUnaligned(bufferCache, (int)octetCount * 8);

            if (channelPacket != null)
            {
                channelPacket.Seek(startOctet);
                channelPacket.WriteOctets(new ReadOnlySpan<byte>(bufferCache, 0, (int)octetCount));
            }

            return new FragmentSection(fragmentIndex, fragmentCount);
        }

        private void WriteSimFrameAndFloatingOrigin(SerializerContext<IOutBitStream> ctx, AbsoluteSimulationFrame? simFrame, Vector3d? floatingOrigin)
        {
            if (simFrame == null)
            {
                ctx.BitStream.WriteBits(0, 1);
            }
            else
            {
                ctx.BitStream.WriteBits(1, 1);
                ctx.BitStream.WriteUint64((ulong)simFrame.Value.Frame);
            }

            if (floatingOrigin == null)
            {
                ctx.BitStream.WriteBits(0, 1);
            }
            else
            {
                ctx.BitStream.WriteBits(1, 1);
                Serialize.WriteFloatingOrigin(floatingOrigin.Value, ctx);
            }
        }

        private uint SimFrameAndFloatingOriginSizeInBits(bool includesSimFrame, bool includesFloatingOrigin)
        {
            uint size = 0;

            size += 1; // 1 bit for presence of simFrame

            if (includesSimFrame)
            {
                size += Serialize.NUM_BITS_FOR_SIMFRAME;
            }

            size += 1; // 1 bit for presence of floatingOrigin
            if (includesFloatingOrigin)
            {
                size += Serialize.NUM_BITS_FOR_FLOATING_ORIGIN;
            }

            return size;
        }

        private (AbsoluteSimulationFrame? simFrame, Vector3d? floatingOrigin) ReadSimFrameAndFloatingOrigin(IInBitStream stream, Logger logger)
        {
            AbsoluteSimulationFrame? simFrame = null;
            Vector3d? floatingOrigin = null;

            if (stream.ReadBits(1) == 1)
            {
                simFrame = (long)stream.ReadUint64();
            }

            if (stream.ReadBits(1) == 1)
            {
                floatingOrigin = Deserialize.ReadFloatingOrigin(stream, logger);
            }

            return (simFrame, floatingOrigin);
        }

        private uint GetNumberOfFragmentsToFit(
            uint firstFragmentIndex,
            uint availableFragmentCount,
            uint remainingBitCount,
            uint totalFragmentCountInPacket,
            bool includesSimFrame,
            bool includesFO)
        {
            for (var fragmentCount = 1u; fragmentCount <= availableFragmentCount; fragmentCount++)
            {
                var containsLastFragment = DoesContainLastFragment(firstFragmentIndex, fragmentCount, totalFragmentCountInPacket);
                var headerSize = GetHeaderSizeInBits(firstFragmentIndex, fragmentCount, containsLastFragment, includesSimFrame, includesFO);
                var size = headerSize + fragmentCount * fragmentSizeInBytes * 8;

                if (size > remainingBitCount)
                {
                    return fragmentCount - 1;
                }
            }

            return availableFragmentCount;
        }

        private bool DoesContainLastFragment(uint firstFragmentIndex, uint fragmentsCount, uint totalFragmentCountInPacket)
        {
            var lastFragmentIndex = firstFragmentIndex + fragmentsCount - 1;

            return totalFragmentCountInPacket - 1 == lastFragmentIndex;
        }
    }
}
