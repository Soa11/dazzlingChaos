// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

using System;

namespace Coherence.PingClient
{
    /// <summary>
    /// Represents the result of a ping operation to a server.
    /// </summary>
    public struct PingResult
    {
        public string Region;
        public long RoundTripMs;
        public string Error;
        public PingProtocol Protocol;

        /// <summary>
        /// Gets whether the ping was successful (no error).
        /// </summary>
        public readonly bool Success => string.IsNullOrEmpty(Error);

        public override string ToString() => Success ? $"{Protocol}:{Region}:{RoundTripMs}ms" : $"{Protocol}:{Region}:{Error}";
    }

    /// <summary>
    /// The protocol used for pinging.
    /// </summary>
    public enum PingProtocol
    {
        TCP,
        UDP,
    }
}
