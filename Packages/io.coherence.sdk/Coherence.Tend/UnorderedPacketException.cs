// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend
{
    using System;
    public class UnorderedPacketException : Exception
    {
        public UnorderedPacketException()
        {
        }

        public UnorderedPacketException(string message, SequenceID last, SequenceID received)
            : base($"last: {last} received:{received} msg:{message}")
        {
        }

        public UnorderedPacketException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
