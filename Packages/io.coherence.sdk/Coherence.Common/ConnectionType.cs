// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Connection
{
    using System;

    public enum ConnectionType : byte
    {
        Client = 0x00,
        Simulator = 0x01,
        Replicator = 0x02,
        [Obsolete("Persistence client is deprecated.")]
        [Deprecated("08/2025", 2, 1, 0, Reason = "Persistence client is deprecated.")]
        Persistence = 0x03,
    };
}
