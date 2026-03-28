// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Payload consisting of the data for multiple lobbies.
    /// </summary>
    internal struct LobbiesData : IEquatable<LobbiesData>
    {
        [JsonProperty("lobbies")]
        public LobbyData[] Lobbies;

        public bool Equals(LobbiesData other) => Lobbies is null ? other.Lobbies is null : other.Lobbies is not null && Lobbies.SequenceEqual(other.Lobbies);
        public override bool Equals(object obj) => obj is LobbiesData other && Equals(other);
        public override int GetHashCode() => Lobbies?.GetHashCode() ?? 0;
    }
}
