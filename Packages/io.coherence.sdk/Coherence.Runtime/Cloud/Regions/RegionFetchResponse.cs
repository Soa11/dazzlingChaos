// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
// Any changes to the Unity version of the request should be reflected
// in the HttpClient version.
// TODO: Separate Http client impl. with common options/policy layer (coherence/unity#1764)
#define UNITY
#endif

namespace Coherence.Cloud
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Response data returned when fetching regions.
    /// </summary>
    [Serializable]
    internal struct RegionFetchResponse
    {
        /// <summary>
        /// List of regions available in a project.
        /// </summary>
        [JsonProperty("regions")]
        public string[] Regions;

        /// <summary>
        /// List of servers that can be pinged using <see cref="Coherence.PingClient.PingClient"/> to determine latency.
        /// Indices correspond to the <see cref="Regions"/> list.
        /// </summary>
        [JsonProperty("ping_servers")]
        internal PingServer[] PingServers;

        public override string ToString() => Regions is null ? "Regions: None" : "Regions: " + string.Join(", ", Regions);

        public static implicit operator Region[](RegionFetchResponse response)
        {
            var regionCount = response.Regions?.Length ?? 0;
            if (regionCount is 0)
            {
                return Array.Empty<Region>();
            }

            var regions = new Region[regionCount];
            var pingServers = response.PingServers;

            for (var i = 0; i < regionCount; i++)
            {
                var name = response.Regions[i];
                var pingServer = FindPingServerForRegion(name, pingServers);
                regions[i] = new Region(name, pingServer);
            }

            return regions;
        }

        public static implicit operator RegionFetchResponse(Region[] regions) => new()
        {
            Regions = regions is null
                ? Array.Empty<string>()
                : Array.ConvertAll(regions, r => r.Name),
            PingServers = GetPingServersFromRegions(regions)
        };

        internal static PingServer[] GetPingServersFromRegions(Region[] regions)
        {
            if (regions == null || regions.Length == 0)
            {
                return Array.Empty<PingServer>();
            }

            var validPingServerCount = 0;
            foreach (var region in regions)
            {
                if (region.PingServer.HasValue)
                {
                    validPingServerCount++;
                }
            }

            var pingServers = new PingServer[validPingServerCount];
            var index = 0;
            foreach (var region in regions)
            {
                if (region.PingServer.HasValue)
                {
                    pingServers[index++] = region.PingServer.Value;
                }
            }
            return pingServers;
        }

        private static PingServer? FindPingServerForRegion(string regionName, PingServer[] pingServers)
        {
            if (pingServers is null)
            {
                return null;
            }

            for (var j = 0; j < pingServers.Length; j++)
            {
                if (pingServers[j].Region == regionName)
                {
                    return pingServers[j];
                }
            }
            return null;
        }
    }
}
