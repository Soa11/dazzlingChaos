// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Transport
{
    using System;

    /// <summary>
    /// Defines the transport to be used when connecting using the default transport factory.
    /// </summary>
    public enum TransportType : byte
    {
        /// <summary>
        /// UDP will be used as a primary transport, falling back to TCP if connection over UDP times out.
        /// </summary>
        UDPWithTCPFallback,

        /// <summary>
        /// Only UDP transport will be used.
        /// </summary>
        UDPOnly,

        /// <summary>
        /// Only TCP transport will be used.
        /// </summary>
        TCPOnly,

        /// <summary>
        /// Only memory transport.
        /// </summary>
        MemoryOnly,
    }
}
