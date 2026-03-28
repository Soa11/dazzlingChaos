// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend
{
    using System;
    using System.Collections.Generic;
    using Brook;
    using Log;

    public class OutgoingLogic : IOutgoingLogic
    {
        public int Count => receivedQueue.Count;
        // LEQ now since you can send the current outgoing sequence without incrementing it and the increment
        // doesn't happen until the packet is sent.
        public bool CanIncrementOutgoingSequence => lastReceivedByRemoteSequenceID.Distance(OutgoingSequenceID) <= ReceiveMask.Range;
        public SequenceID LastReceivedByRemoteSequenceID => lastReceivedByRemoteSequenceID;
        public SequenceID OutgoingSequenceID { get; set; } = new SequenceID(0);

        private SequenceID lastReceivedByRemoteSequenceID = SequenceID.Max;
        private readonly Queue<DeliveryInfo> receivedQueue = new Queue<DeliveryInfo>();

        private Logger logger;

        public OutgoingLogic(Logger logger)
        {
            this.logger = logger.With<OutgoingLogic>();
        }

        public bool ReceivedByRemote(SequenceID receivedByRemoteID, ReceiveMask receivedByRemoteMask)
        {
            int distance = lastReceivedByRemoteSequenceID.Distance(receivedByRemoteID);
            if (distance == 0)
            {
                return false;
            }

            if (!lastReceivedByRemoteSequenceID.IsValidSuccessor(receivedByRemoteID))
            {
                throw new UnorderedPacketException("Stale packet detected", lastReceivedByRemoteSequenceID, receivedByRemoteID);
            }

            SequenceID receivedID = new SequenceID(lastReceivedByRemoteSequenceID.Value);
            receivedID = receivedID.Next();
            MutableReceiveMask bits = new MutableReceiveMask(receivedByRemoteMask, distance);
            for (int i = 0; i < distance; ++i)
            {
                Bit wasReceived = bits.ReadNextBit();
                Append(receivedID, wasReceived.IsOn);
                receivedID = receivedID.Next();
            }
            lastReceivedByRemoteSequenceID = receivedByRemoteID;
            return true;
        }

        public SequenceID IncreaseOutgoingSequenceID()
        {
            if (!CanIncrementOutgoingSequence)
            {
                throw new Exception($"Can not increase sequence ID. outgoing: {OutgoingSequenceID} last received: {lastReceivedByRemoteSequenceID} number of pending {lastReceivedByRemoteSequenceID.Distance(OutgoingSequenceID)} ");
            }

            logger.Trace("new seq", ("last", lastReceivedByRemoteSequenceID), ("next", OutgoingSequenceID.Next()), ("distance", lastReceivedByRemoteSequenceID.Distance(OutgoingSequenceID)));

            OutgoingSequenceID = OutgoingSequenceID.Next();

            return OutgoingSequenceID;
        }

        public DeliveryInfo Dequeue()
        {
            return receivedQueue.Dequeue();
        }

        private void Append(SequenceID receivedID, bool bit)
        {
            DeliveryInfo info = new DeliveryInfo
            {
                PacketSequenceId = receivedID,
                WasDelivered = bit
            };

            receivedQueue.Enqueue(info);
        }
    }
}
