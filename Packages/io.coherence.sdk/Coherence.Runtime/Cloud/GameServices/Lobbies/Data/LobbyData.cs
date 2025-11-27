// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Linq;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    public struct LobbyData : IEquatable<LobbyData>
    {
        [JsonIgnore] public IReadOnlyList<CloudAttribute> Attributes => lobbyAttributes;
        [JsonIgnore] public IReadOnlyList<LobbyPlayer> Players => players;

        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("region")]
        public string Region;

        [JsonProperty("tag")]
        public string Tag;

        [JsonProperty("max_players")]
        public int MaxPlayers;

        [JsonProperty("closed")]
        public bool Closed;

        [JsonProperty("unlisted")]
        public bool Unlisted;

        [JsonProperty("private")]
        public bool IsPrivate;

        [JsonIgnore]
        public PlayerAccountId OwnerId => ownerId;
        [JsonProperty("owner_id")]
        internal string ownerId;

        [JsonProperty("sim_slug")]
        public string SimulatorSlug;

        [JsonProperty("room_id")]
        public long RoomId;

        [JsonProperty("room")]
        public RoomData? RoomData;

        [JsonProperty("players")]
        internal List<LobbyPlayer> players;

        [JsonProperty("attributes")]
        internal List<CloudAttribute> lobbyAttributes;

        public CloudAttribute? GetAttribute(string key)
        {
            if (lobbyAttributes == null)
            {
                return null;
            }

            foreach (var attribute in lobbyAttributes)
            {
                if (attribute.Key.Equals(key))
                {
                    return attribute;
                }
            }

            return null;
        }

        public override string ToString() => $"LobbySession(Name:\"{Name}\", Id:\"{Id}\")";

        public bool Equals(LobbyData other)
        {
            return Id == other.Id
                && string.Equals(Name, other.Name)
                && string.Equals(Region, other.Region)
                && string.Equals(Tag, other.Tag)
                && MaxPlayers == other.MaxPlayers
                && Closed == other.Closed
                && Unlisted == other.Unlisted
                && IsPrivate == other.IsPrivate
                && string.Equals(ownerId, other.ownerId)
                && string.Equals(SimulatorSlug, other.SimulatorSlug)
                && RoomId == other.RoomId
                && Nullable.Equals(RoomData, other.RoomData)
                && ListEqual(players, other.players)
                && ListEqual(lobbyAttributes, other.lobbyAttributes);

            bool ListEqual<T>(List<T> a, List<T> b) => a is null ? b is null : b is not null && a.SequenceEqual(b);
        }

        public override bool Equals(object obj) => obj is LobbyData other && Equals(other);

        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
    }
}
