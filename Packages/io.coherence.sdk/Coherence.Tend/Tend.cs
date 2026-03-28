// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend
{
    using Brook;
    using System;
    using Log;

    public class Tend
    {
        // Only process reliable packets when connected is true. Set by the
        // layer above Tend (Brisk).
        public bool Connected;

        public event Action<DeliveryInfo> OnDeliveryInfo;

        public bool CanSend => tendOut.CanIncrementOutgoingSequence;
        public SequenceID OutgoingSequenceID => tendOut.OutgoingSequenceID;
        public SequenceID LastReceivedByRemote => tendOut.LastReceivedByRemoteSequenceID;

        private int NumberOfPacketsPending => tendOut.LastReceivedByRemoteSequenceID.Distance(tendOut.OutgoingSequenceID);

        private readonly IOutgoingLogic tendOut;
        private readonly IIncomingLogic tendIn;
        private readonly Logger logger;

        public Tend(Logger logger)
        {
            this.logger = logger.With<Tend>();

            tendOut = new OutgoingLogic(this.logger);
            tendIn = new IncomingLogic(this.logger);
        }

        public Tend(Logger logger, IOutgoingLogic tendOut, IIncomingLogic tendIn)
        {
            this.logger = logger.With<Tend>();

            this.tendOut = tendOut;
            this.tendIn = tendIn;
        }

        public bool ReadHeader(IInOctetStream stream, out TendHeader tendHeader, out bool didAck)
        {
            didAck = false;

            if (stream.RemainingOctetCount < 1)
            {
                // need at least one byte to read reliability
                logger.Warning(Warning.TendLessThan1Byte);
                tendHeader = new TendHeader();
                return false;
            }

            try
            {
                tendHeader = TendHeader.DeserializeHeader(stream);
            }
            catch (System.Exception ex)
            {
                // failed to deserialize header, invalid packet packet
                logger.Warning(Warning.TendInvalidPacket, ("exception", ex));
                tendHeader = new TendHeader();
                return false;
            }


            if (!tendHeader.isReliable)
            {
                return true; // do not care about order in this packet
            }

            // Do not process reliable packets unless connected.  This
            // prevents accidentally processing random data from other connections.
            if (!Connected)
            {
                return false;
            }

            if (!tendIn.ReceivedToUs(tendHeader.packetID))
            {
                return false; // out of order packet received, should be thrown away
            }

            try
            {
                didAck = tendOut.ReceivedByRemote(tendHeader.receivedID, tendHeader.receiveMask);
            }
            catch (System.Exception ex)
            {
                // failed to deserialize header, invalid packet packet
                logger.Warning(Warning.TendInvalidPacket, ("exception", ex));
                tendHeader = new TendHeader();
                return false;
            }

            // Trigger delivery notification callbacks
            while (tendOut.Count > 0)
            {
                DeliveryInfo deliveryInfo = tendOut.Dequeue();
                OnDeliveryInfo?.Invoke(deliveryInfo);
            }

            return true;
        }

        public TendHeader WriteHeader(IOutOctetStream stream, bool isReliable)
        {
            if (!isReliable)
            {
                TendHeader unreliableTendHeader = new TendHeader() { isReliable = false };
                TendHeader.SerializeHeader(stream, unreliableTendHeader);
                return unreliableTendHeader;
            }

            // Throw exception if there are 32 pending packets already
            if (!tendOut.CanIncrementOutgoingSequence)
            {
                throw new Exception($"Can't increment tend, outgoing: {OutgoingSequenceID} last received: {LastReceivedByRemote} number of pending {NumberOfPacketsPending} ");
            }

            var tendHeader = new TendHeader
            {
                isReliable = true,
                packetID = tendOut.OutgoingSequenceID,     // Tend packet Id
                receivedID = tendIn.LastReceivedToUs,      // The Id of the incoming packet that was last received from server
                receiveMask = tendIn.ReceiveMask           // Lost or dropped status for the 32 last incoming packets
            };

            TendHeader.SerializeHeader(stream, tendHeader);
            return tendHeader;
        }

        public void OnPacketSent(SequenceID sequenceID, bool isReliable)
        {
            if (!isReliable)
            {
                return;
            }

            // increment only if the sequence is the current one since it's
            // possible to resend an old sequence a few times.
            if (sequenceID.Equals(tendOut.OutgoingSequenceID))
            {
                _ = tendOut.IncreaseOutgoingSequenceID();
            }
        }

        public bool IsValidSeqToSend(in SequenceID sentSequenceId)
        {
            var higherThanReceived = LastReceivedByRemote.IsValidSuccessor(sentSequenceId);
            if (!higherThanReceived)
            {
                return false;
            }

            return OutgoingSequenceID.Equals(sentSequenceId);
        }
    }
}
