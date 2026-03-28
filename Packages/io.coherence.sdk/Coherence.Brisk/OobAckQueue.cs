// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brisk
{
    using Tend;
    using Models;
    using System.Collections.Generic;

    internal enum PacketStatus
    {
        Unknown,
        Acked,
        Lost,
    }

    internal class OobAckQueue
    {
        private readonly Queue<Packet> queue = new Queue<Packet>();

        public void Clear()
        {
            queue.Clear();
        }

        public void Enqueue(IOobMessage oobMessage, SequenceID packetSequenceId)
        {
            queue.Enqueue(new Packet(oobMessage, packetSequenceId));
        }

        public PacketStatus Ack(DeliveryInfo info, out IOobMessage message)
        {
            message = null;

            if (queue.Count == 0)
            {
                return PacketStatus.Unknown;
            }

            bool sequenceIdMatches = queue.Peek().SequenceID.Equals(info.PacketSequenceId);
            if (!sequenceIdMatches)
            {
                return PacketStatus.Unknown;
            }

            if (info.WasDelivered)
            {
                queue.Dequeue();
                return PacketStatus.Acked;
            }

            Packet oobToResend = queue.Dequeue();
            message = oobToResend.Message;

            return PacketStatus.Lost;
        }

        private struct Packet
        {
            public readonly IOobMessage Message;
            public readonly SequenceID SequenceID;

            public Packet(IOobMessage message, SequenceID sequenceID)
            {
                Message = message;
                SequenceID = sequenceID;
            }
        }
    }
}
