// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend
{
    using Brook;
    using System.Net;

    public struct InPacket
    {
        public readonly IInOctetStream Stream;
        public readonly SequenceID SequenceID;
        public readonly bool IsReliable;
        public readonly bool IsOob;
        public readonly IPEndPoint From;

        public InPacket(IInOctetStream stream, SequenceID sequenceID, bool isReliable, bool isOob, IPEndPoint from)
        {
            Stream = stream;
            SequenceID = sequenceID;
            IsReliable = isReliable;
            IsOob = isOob;
            From = from;
        }
    }
}
