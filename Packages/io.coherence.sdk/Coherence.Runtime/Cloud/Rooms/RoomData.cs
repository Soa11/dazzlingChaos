// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#pragma warning disable 649

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
    using System.Collections.Generic;
    using System.Linq;
    using Connection;
    using Newtonsoft.Json;
#if !UNITY
    using Coherence.Headless;
#endif

    /// <summary>
    /// Represents a room that has been created in coherence Cloud.
    /// </summary>
    public struct RoomData : IEquatable<RoomData>
    {
        /// <summary>
        /// The key used to store the name of a room in <see cref="RoomCreationOptions"/>.
        /// </summary>
        public const string RoomNameKey = "name";

        /// <summary>
        /// The unique identifier for the room within a session.
        /// </summary>
        /// <remarks>
        /// This ID is unique within the current session but may be reused across different sessions.
        /// For a globally unique identifier, use <see cref="UniqueId"/>.
        /// </remarks>
        [JsonProperty("room_id")]
        public ushort Id;

        /// <summary>
        /// The globally unique identifier for the room.
        /// </summary>
        /// <remarks>
        /// This ID is globally unique and persists across all sessions. Use this for room identification
        /// when you need a permanent reference to the room.
        /// </remarks>
        [JsonProperty("unique_id")]
        public ulong UniqueId;

        /// <summary>
        /// The host information for the room.
        /// </summary>
        /// <remarks>
        /// Contains the IP address, port, and region information needed to connect to the room's host server.
        /// </remarks>
        [JsonProperty("host")]
        public RoomHostData Host;

        /// <summary>
        /// The maximum number of players allowed in the room.
        /// </summary>
        [JsonProperty("max_players")]
        public int MaxPlayers;

        /// <summary>
        /// The current number of players connected to the room.
        /// </summary>
        [JsonProperty("connected_players")]
        public int ConnectedPlayers;

        /// <summary>
        /// Key-value pairs of custom room data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This dictionary can store arbitrary room metadata that can be queried and filtered when searching for rooms.
        /// </para>
        /// <para>
        /// The room name is typically stored here using the <see cref="RoomNameKey"/> constant.
        /// </para>
        /// </remarks>
        [JsonProperty("kv")]
        public Dictionary<string, string> KV;

        /// <summary>
        /// Array of tags associated with the room.
        /// </summary>
        /// <remarks>
        /// Tags are used for filtering and categorizing rooms when searching. They provide a simple way
        /// to group rooms by type, game mode, or other characteristics.
        /// </remarks>
        [JsonProperty("tags")]
        public string[] Tags;

        /// <summary>
        /// The simulator slug identifying the type of simulator running the room.
        /// </summary>
        /// <remarks>
        /// This identifies which simulator configuration is being used for the room, allowing different
        /// room types to run different game logic or configurations.
        /// </remarks>
        [JsonProperty("sim_slug")]
        public string SimSlug;

        /// <summary>
        /// The secret token used for room authentication and management.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This token is required for certain room operations like deletion or modification.
        /// </para>
        /// <para>
        /// Keep this secret secure as it grants administrative access to the room.
        /// </para>
        /// </remarks>
        [JsonProperty("secret")]
        public string Secret;

        /// <summary>
        /// The timestamp when the room was created.
        /// </summary>
        [JsonProperty("created_at")]
        public string CreatedAt;

        /// <summary>
        /// The authentication token for connecting to the room.
        /// </summary>
        /// <remarks>
        /// This token is used to authenticate the client when connecting to the room's game session.
        /// </remarks>
        public string AuthToken;

        private string roomName;

        /// <summary>
        /// The display name of the room.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property extracts the room name from the <see cref="KV"/> dictionary using the <see cref="RoomNameKey"/>.
        /// </para>
        /// <para>
        /// If no name is found in the KV data, this returns an empty string.
        /// </para>
        /// </remarks>
        public string RoomName
        {
            get
            {
                if (String.IsNullOrEmpty(roomName))
                {
                    roomName = ExtractRoomName();
                }

                return roomName;
            }
        }

        /// <summary>
        /// Creates endpoint data for connecting to the room.
        /// </summary>
        /// <param name="room">The room data to create endpoint information for.</param>
        /// <returns>
        /// A tuple containing the endpoint data, validation status, and any validation error message.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method prepares the connection information needed to join the room, including
        /// handling platform-specific port configurations for WebGL and local development.
        /// </para>
        /// <para>
        /// The method validates the endpoint data and returns appropriate error messages if validation fails.
        /// </para>
        /// </remarks>
        public static (EndpointData, bool isValid, string validationErrorMessage) GetRoomEndpointData(RoomData room)
        {
#if UNITY
            var simAuthToken = SimulatorUtility.AuthToken;
            if (!string.IsNullOrEmpty(simAuthToken))
            {
                room.AuthToken = simAuthToken;
            }
#endif

            var roomEndpoint = new EndpointData
            {
                host = room.Host.Ip,
                port = room.Host.Port,
                roomId = room.Id,
                uniqueRoomId = room.UniqueId,

                runtimeKey = RuntimeSettings.Instance.RuntimeKey,
                schemaId = RuntimeSettings.Instance.SchemaID,
                region = room.Host.Region,
                authToken = room.AuthToken,
                roomSecret = room.Secret,
                simulatorType = nameof(EndpointData.SimulatorType.room),
            };

            var local = roomEndpoint.region == "local";

            var (valid, validationErrorMessage) = roomEndpoint.Validate();
            if (!valid)
            {
                return (roomEndpoint, false, validationErrorMessage);
            }

#if UNITY
            //Check for special addresses
            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WebGLPlayer)
            {
                roomEndpoint.port =
                    local ? RuntimeSettings.Instance.LocalRoomsWebPort : RuntimeSettings.Instance.RemoteWebPort;
            }
            else if (local)
            {
                roomEndpoint.port = RuntimeSettings.Instance.LocalRoomsUDPPort;
            }
#endif

            return (roomEndpoint, true, null);
        }

        /// <summary>
        /// Returns a string representation of the room data.
        /// </summary>
        /// <returns>A formatted string containing host, port, room ID, and player count information.</returns>
        public override string ToString() => $"{Host.Ip}:{Host.Port}:{Id} ({ConnectedPlayers}/{MaxPlayers})";

        private string ExtractRoomName()
        {
            if (KV != null && KV.TryGetValue(RoomNameKey, out var name))
            {
                return roomName = name;
            }

            return roomName = String.Empty;
        }

        /// <summary>
        /// Determines whether the specified <see cref="RoomData"/> is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="RoomData"/> to compare with this instance.</param>
        /// <returns><see langword="true"/> if the specified <see cref="RoomData"/> is equal to this instance; otherwise, <see langword="false"/>.</returns>
        public bool Equals(RoomData other)
        {
            return Id == other.Id
                   && UniqueId == other.UniqueId
                   && Host.Equals(other.Host)
                   && MaxPlayers == other.MaxPlayers
                   && ConnectedPlayers == other.ConnectedPlayers
                   && DictionaryEqual(KV, other.KV)
                   && ArrayEqual(Tags, other.Tags)
                   && string.Equals(SimSlug, other.SimSlug)
                   && string.Equals(Secret, other.Secret)
                   && string.Equals(CreatedAt, other.CreatedAt)
                   && string.Equals(AuthToken, other.AuthToken)
                   && string.Equals(roomName, other.roomName);

            bool ArrayEqual(string[] a, string[] b) => a is null ? b is null : b is not null && a.SequenceEqual(b);
            bool DictionaryEqual(Dictionary<string, string> a, Dictionary<string, string> b) => a is null ? b is null : b is not null && a.SequenceEqual(b);
        }

        /// <summary>
        /// Determines whether the specified object is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the specified object is equal to this instance; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object obj) => obj is RoomData other && Equals(other);

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Id);
            hashCode.Add(UniqueId);
            hashCode.Add(Host);
            hashCode.Add(MaxPlayers);
            hashCode.Add(ConnectedPlayers);
            hashCode.Add(KV);
            hashCode.Add(Tags);
            hashCode.Add(SimSlug);
            hashCode.Add(Secret);
            hashCode.Add(CreatedAt);
            hashCode.Add(AuthToken);
            hashCode.Add(roomName);
            return hashCode.ToHashCode();
        }
    }
}
