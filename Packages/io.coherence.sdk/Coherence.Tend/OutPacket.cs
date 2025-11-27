// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend
{
    using Common;
    using Log;
    using Brook;
    using Brook.Octet;

    using Logger = Log.Logger;

    public struct OutPacket
    {
        public readonly IOutOctetStream Stream;
        public readonly SequenceID SequenceID;
        public readonly bool IsReliable;
        public readonly bool IsOob;

        public OutPacket(IOutOctetStream stream, SequenceID sequenceID, bool isReliable, bool isOob, Logger logger)
        {
            if (stream == null)
            {
                // Trying to catch https://app.zenhub.com/workspaces/engine-group-5fb3b64dabadec002057e6f2/issues/gh/coherence/engine/2263
                // where there's a null reference possibly in the OutPacket stream.
                logger.Warning(Warning.OutPacketStreamNull);
                stream = new OutOctetStream(ConnectionSettings.DEFAULT_MTU); // just need something reasonable here.
            }

            Stream = stream;
            SequenceID = sequenceID;
            IsReliable = isReliable;
            IsOob = isOob;
        }

        public OutPacket WithStream(IOutOctetStream stream, Logger logger)
        {
            return new OutPacket(stream, SequenceID, IsReliable, IsOob, logger);
        }
    }
}
