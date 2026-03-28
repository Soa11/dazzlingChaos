// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System.Net;
    using System.Net.Sockets;
    using Newtonsoft.Json;

    /// <summary>
    /// Information about a ping server for latency testing.
    /// </summary>
    public struct PingServer
    {
        /// <summary>
        /// Region name of the ping server.
        /// </summary>
        public string Region;

        /// <summary>
        /// IP of the ping server.
        /// </summary>
        [JsonProperty("ip")]
        public string Ip;

        /// <summary>
        /// Port at which ping server listens.
        /// </summary>
        [JsonProperty("port")]
        public int Port;

        public override string ToString() => $"{Region}:{Ip}:{Port}";

        internal readonly bool Validate(out string errorMessage)
        {
            if (string.IsNullOrEmpty(Ip))
            {
                errorMessage = "Empty ip address";
                return false;
            }

            if (Port <= 0)
            {
                errorMessage = $"Invalid port: {Port}";
                return false;
            }

            if (!IPAddress.TryParse(Ip, out IPAddress ip))
            {
                errorMessage = $"Invalid ip address: {Ip}";
                return false;
            }

            if (ip.AddressFamily != AddressFamily.InterNetwork)
            {
                errorMessage = $"Non IPv4 address: {Ip}";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
