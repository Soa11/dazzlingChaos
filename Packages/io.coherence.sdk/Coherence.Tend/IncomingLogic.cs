// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend
{
    using System;
    using Log;

    public class IncomingLogic : IIncomingLogic
    {
        public SequenceID LastReceivedToUs => lastReceivedToUs;
        public ReceiveMask ReceiveMask => new ReceiveMask(receiveMask);

        private SequenceID lastReceivedToUs = SequenceID.Max;
        private uint receiveMask;

        private Logger logger;

        public IncomingLogic(Logger logger)
        {
            this.logger = logger.With<IncomingLogic>();
        }

        public bool ReceivedToUs(SequenceID nextID)
        {
            if (!lastReceivedToUs.IsValidSuccessor(nextID))
            {
                return false;
            }

            int distance = lastReceivedToUs.Distance(nextID);
            if (distance > ReceiveMask.Range)
            {
                throw new Exception("too big gap in sequence." + distance);
            }

            logger.Trace("received", ("last", lastReceivedToUs), ("next", nextID), ("distance", distance));

            for (int i = 0; i < distance - 1; ++i)
            {
                Append(false);
            }

            Append(true);
            lastReceivedToUs = nextID;

            return true;
        }

        private void Append(bool bit)
        {
            receiveMask <<= 1;
            receiveMask |= bit ? 0x1 : (uint)0x0;
        }
    }
}
