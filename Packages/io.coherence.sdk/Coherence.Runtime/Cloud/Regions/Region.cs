// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    /// <summary>
    /// Represents a region available in a project.
    /// </summary>
    public readonly struct Region
    {
        /// <summary>
        /// The name of the region.
        /// </summary>
        public string Name { get; }


        /// <summary>
        /// The server endpoint associated with the region that can be pinged to measure latency.
        /// </summary>
        public PingServer? PingServer { get; }

        public Region(string name, PingServer? pingServer)
        {
            Name = name;
            PingServer = pingServer;
        }

        public static implicit operator string(Region region) => region.Name;
        public override string ToString() => Name ?? "";
    }
}
