// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend
{
    using Brook;
    using Coherence.Brook.Octet;
    using System;

    using SeqID = System.UInt16;

    public struct TendHeader
    {
        public const int BitsForReliable = 1;
        public const int BitsForSequenceID = 11;
        public const int BitsForReceiveMask = 32;

        // Do an integer rounding of the total octets required to fit
        // the packed bits for the header. Uses 7 to round to the next whole octet.
        public const int TotalOctets = (BitsForReliable + BitsForSequenceID + BitsForSequenceID + BitsForReceiveMask + 7) / 8;

        public bool isReliable;
        public SequenceID packetID;
        public SequenceID receivedID;
        public ReceiveMask receiveMask;

        public override string ToString()
        {
            return $"{nameof(isReliable)}: {isReliable}, {nameof(packetID)}: {packetID}, {nameof(receivedID)}: {receivedID}, {nameof(receiveMask)}: {receiveMask}";
        }

        public static void SerializeHeader(IOutOctetStream stream, TendHeader tendHeader)
        {
            var bitStream = OutBitStream.Shared.Reset(stream);

            bitStream.WriteBits(tendHeader.isReliable ? 1u : 0u, BitsForReliable);

            if (tendHeader.isReliable)
            {
                bitStream.WriteBits(tendHeader.packetID.Value, BitsForSequenceID);
                bitStream.WriteBits(tendHeader.receivedID.Value, BitsForSequenceID);
                bitStream.WriteBits(tendHeader.receiveMask.Bits, BitsForReceiveMask);
            }

            // If not reliable this will flush one octet.
            bitStream.Flush();
        }

        public static TendHeader DeserializeHeader(IInOctetStream stream)
        {
            var header = new TendHeader();

            var bitStream = InBitStream.Shared.Reset(stream, 1);

            header.isReliable = bitStream.ReadBits(BitsForReliable) != 0;

            if (header.isReliable)
            {
                bitStream.GrowSize(TotalOctets - 1);

                header.packetID = new SequenceID((SeqID)bitStream.ReadBits(BitsForSequenceID));
                header.receivedID = new SequenceID((SeqID)bitStream.ReadBits(BitsForSequenceID));
                header.receiveMask = new ReceiveMask(bitStream.ReadBits(BitsForReceiveMask));
            }

            return header;
        }
    }
}
