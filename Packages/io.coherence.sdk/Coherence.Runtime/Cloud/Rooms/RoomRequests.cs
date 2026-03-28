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
    using Common;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

#pragma warning disable 649
    internal struct RoomCreationRequest
    {
        [JsonProperty("tags")]
        public string[] Tags;

        [JsonProperty("kv")]
        public Dictionary<string, string> KV;

        [JsonProperty("region")]
        public string Region;

        [JsonProperty("sim_slug")]
        public string SimSlug;

        [JsonProperty("sim_payload")]
        public string SimPayload;

        [JsonProperty("max_players")]
        public int MaxClients;

        [JsonProperty("find_or_create")]
        public bool FindOrCreate;
    }

    internal struct RoomMatchRequest
    {
        [JsonProperty("tags")]
        public string[] Tags;

        [JsonProperty("region")]
        public string Region;

        [JsonProperty("sim_slug")]
        public string SimSlug;
    }

    internal struct RoomUnlistRequest
    {
        [JsonProperty("secret")] public string Secret;
    }

    internal struct RoomFetchResponse
    {
        [JsonProperty("rooms")]
        public RoomData[] Rooms;
    }

    /// <summary>
    /// Response data returned when matching for an available room.
    /// </summary>
    /// <seealso cref="CloudRoomsService.MatchRoomAsync"/>
    public struct RoomMatchResponse
    {
        /// <summary>
        /// The matched room, if a match was found; otherwise, <see langword="null"/>.
        /// </summary>
        [JsonProperty("room")]
        public RoomData? Room;
    }

    internal struct LocalRoomCreationRequest
    {
        [JsonProperty("UniqueID")]
        public int UniqueID;

        [JsonProperty("MaxClients")]
        public int MaxClients;

        [JsonProperty("MaxEntities")]
        public uint MaxEntities;

        [JsonProperty("OutStatsFreq")]
        public int OutStatsFreq;

        [JsonProperty("LogStatsFreq")]
        public int LogStatsFreq;

        [JsonProperty("SchemaName")]
        public string SchemaName; //Should be empty or "local"

        [JsonProperty("SchemaTimeout")]
        public int SchemaTimeout;

        [JsonProperty("SchemaUrls")]
        public string[] SchemaUrls;

        [JsonProperty("Schemas")]
        public string[] Schemas;

        [JsonProperty("DisconnectTimeout")]
        public int DisconnectTimeout;

        [JsonProperty("DebugStreams")]
        public bool DebugStreams;

        [JsonProperty("Frequency")]
        public int Frequency; //Should be 0

        [JsonProperty("MinQueryDistance")]
        public float MinQueryDistance;

        [JsonProperty("WebSupport")]
        public bool WebSupport;

        [JsonProperty("CleanupTimeout")]
        public int CleanupTimeout;

        [JsonProperty("ProjectID")]
        public string ProjectID;

        [JsonProperty("KVP")]
        public Dictionary<string, string> KeyValues;

        [JsonProperty("Tags")]
        public string[] Tags;

        [JsonProperty("Secret")]
        public string Secret;

        [JsonProperty("HostAuthority")]
        public string HostAuthority;
    }

    internal struct RemoveRoomRequest
    {
        [JsonProperty("RoomID")]
        public ushort RoomId;
    }

    /// <summary>
    /// Represents a room in a locally running Replication Server.
    /// </summary>
    /// <seealso cref="ReplicationServerRoomsService"/>
    public struct LocalRoomData
    {
        /// <summary>
        /// The unique identifier for the local room.
        /// </summary>
        [JsonProperty("RoomID")]
        public ushort RoomID;

        /// <summary>
        /// The secret token for managing the local room.
        /// </summary>
        /// <remarks>
        /// This secret is required for administrative operations on the room such as deletion or modification.
        /// </remarks>
        [JsonProperty("Secret")]
        public string Secret;
    }

    /// <summary>
    /// Represents a single room item in a <see cref="LocalRoomsResponse"/>.
    /// </summary>
    /// <seealso cref="ReplicationServerRoomsService.FetchRoomsAsync"/>
    public struct LocalRoomsListItem
    {
        /// <summary>
        /// The globally unique identifier for the room.
        /// </summary>
        /// <remarks>
        /// This ID is globally unique and persists across all sessions. Use this for room identification
        /// when you need a permanent reference to the room.
        /// </remarks>
        public ulong UniqueID;

        /// <summary>
        /// The unique identifier for the room within a session.
        /// </summary>
        /// <remarks>
        /// This ID is unique within the current session but may be reused across different sessions.
        /// For a globally unique identifier, use <see cref="UniqueID"/>.
        /// </remarks>
        public ushort ID;

        /// <summary>
        /// The maximum number of players allowed in the room.
        /// </summary>
        public int MaxClients;

        /// <summary>
        /// The name of the schema being used by this room.
        /// </summary>
        public string SchemaName;

        /// <summary>
        /// The current number of active connections to this room.
        /// </summary>
        public int ConnectionCount;

        /// <summary>
        /// The timestamp of the last health check for this room.
        /// </summary>
        public string LastCheckTime;

        /// <summary>
        /// Identifier of the project to which the room belongs.
        /// </summary>
        public string ProjectID;

        /// <summary>
        /// Key-value pairs of custom room metadata.
        /// </summary>
        /// <remarks>
        /// This dictionary can store arbitrary room metadata for filtering and identification purposes.
        /// </remarks>
        public Dictionary<string, string> KVP;

        /// <summary>
        /// Collection of tags associated with the room for categorization and filtering.
        /// </summary>
        public string[] Tags;
    }

    /// <summary>
    /// Response containing a list of rooms in a locally running Replication Server.
    /// </summary>
    /// <seealso cref="ReplicationServerRoomsService.FetchRoomsAsync"/>
    public struct LocalRoomsResponse
    {
        /// <summary>
        /// Collection of local rooms.
        /// </summary>
        [JsonProperty("Rooms")]
        public LocalRoomsListItem[] Rooms;
    }

    /// <summary>
    /// Contains host connection information for a <see cref="RoomData">room</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides all the necessary network details to connect to a room's host server,
    /// including IP address, port, region, and server version information.
    /// </para>
    /// <para>
    /// This structure is used within <see cref="RoomData"/> to specify where the room is hosted.
    /// </para>
    /// </remarks>
    public struct RoomHostData : IEquatable<RoomHostData>
    {
        /// <summary>
        /// The IP address of the room's host server.
        /// </summary>
        [JsonProperty("ip")]
        public string Ip;

        /// <summary>
        /// The port number for connecting to the room's host server.
        /// </summary>
        [JsonProperty("port")]
        public int Port;

        /// <summary>
        /// The geographic region where the room is hosted.
        /// </summary>
        /// <remarks>
        /// This can be used for regional filtering of rooms.
        /// </remarks>
        [JsonProperty("region")]
        public string Region;

        /// <summary>
        /// The version of the Replication Server hosting the room.
        /// </summary>
        [JsonProperty("rs_version")]
        public string RSVersion;

        public override string ToString()
            => $"{nameof(Ip)}: {Ip}," +
            $"{nameof(Port)}: {Port}," +
            $"{nameof(Region)}: {Region}" +
            $"{nameof(RSVersion)}: {RSVersion}";

        public bool Equals(RoomHostData other)
            => string.Equals(Ip, other.Ip)
            && Port == other.Port
            && string.Equals(Region, other.Region)
            && string.Equals(RSVersion, other.RSVersion);

        public override bool Equals(object obj) => obj is RoomHostData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Ip, Port, Region, RSVersion);
    }

    /// <summary>
    /// Configuration options for creating rooms in coherence Cloud.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides all the settings needed to customize room creation including capacity,
    /// metadata, tags, and simulator configuration.
    /// </para>
    /// </remarks>
    /// <see cref="CloudRoomsService.CreateRoomAsync"/>
    public class RoomCreationOptions
    {
        /// <summary>
        /// The display name of the room.
        /// </summary>
        public string Name
        {
            get => KeyValues.TryGetValue(RoomData.RoomNameKey, out var name) ? name : "";
            set => KeyValues[RoomData.RoomNameKey] = value;
        }

        /// <summary>
        /// The maximum number of clients that can connect to the room.
        /// </summary>
        /// <remarks>
        /// Defaults to 10 clients. This limit helps manage server resources and game balance.
        /// </remarks>
        public int MaxClients = 10;

        /// <summary>
        /// Array of tags for categorizing and filtering the room.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Tags allow players to find rooms of specific types or game modes when searching.
        /// </para>
        /// <para>
        /// Defaults to an empty array if not specified.
        /// </para>
        /// </remarks>
        public string[] Tags = new string[] { };

        /// <summary>
        /// Dictionary of custom key-value metadata for the room.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This can store arbitrary room information that can be queried and filtered when searching for rooms.
        /// </para>
        /// <para>
        /// The room name is automatically managed through the <see cref="Name"/> property using the <see cref="RoomData.RoomNameKey"/>.
        /// </para>
        /// </remarks>
        public Dictionary<string, string> KeyValues = new Dictionary<string, string>();

        /// <summary>
        /// Custom payload data to send to the simulator when the room is created.
        /// </summary>
        /// <remarks>
        /// This allows passing initialization parameters to the room's simulator instance.
        /// </remarks>
        public string SimPayload = string.Empty;

        /// <summary>
        /// Specifies whether to find an existing suitable room or create a new one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, the system will first attempt to find an existing room with all the same <see cref="Tags">tags</see>.
        /// </para>
        /// <para>
        /// If no room with the same tags is found, a new room will be created with the specified options.
        /// </para>
        /// </remarks>
        public bool FindOrCreate;

        /// <summary>
        /// Gets a default instance of room creation options with standard settings.
        /// </summary>
        public static RoomCreationOptions Default => new RoomCreationOptions();
    }

    /// <summary>
    /// Extended configuration options for creating rooms on self-hosted Replication Servers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inherits from <see cref="RoomCreationOptions"/> and adds additional settings specific to
    /// self-hosted environments, including server configuration and entity limits.
    /// </para>
    /// <para>
    /// Use this class when creating rooms on your own Replication Server infrastructure.
    /// </para>
    /// </remarks>
    public class SelfHostedRoomCreationOptions : RoomCreationOptions
    {
        /// <summary>
        /// A unique identifier for the room within the self-hosted environment.
        /// </summary>
        /// <remarks>
        /// Defaults to a random integer between 1 and the maximum integer value to ensure uniqueness.
        /// </remarks>
        public int UniqueId = new Random().Next(1, int.MaxValue);

        /// <summary>
        /// The timeout in seconds before an empty room is automatically cleaned up.
        /// </summary>
        /// <remarks>
        /// Defaults to 60 seconds. This helps manage server resources by removing unused rooms.
        /// </remarks>
        public int CleanupTimeout = 60;

        /// <summary>
        /// The maximum number of entities that can exist in the room.
        /// </summary>
        /// <remarks>
        /// Defaults to 1000 entities. This limit helps manage memory and processing resources.
        /// </remarks>
        public uint MaxEntities = 10 * 100;

        /// <summary>
        /// The secret token for administrative access to the room.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The secret is used to prevent the room from being closed by anyone except its owner.
        /// </para>
        /// <para>
        /// Defaults to "devSecret" for development purposes.
        /// In production environments it is recommended to use a secure, randomly generated secret.
        /// </para>
        /// </remarks>
        public string Secret = "devSecret";

        /// <summary>
        /// The identifier of the project to which the room belongs.
        /// </summary>
        /// <remarks>
        /// Defaults to "local" for self-hosted development environments.
        /// </remarks>
        public string ProjectId = "local";

        /// <summary>
        /// Array of schemas for the room.
        /// </summary>
        /// <remarks>
        /// Schemas define the structure of the game world from the network's point of view.
        /// They define what, how much, how fast and how precisely data is being exchanged between clients and the Replication Server.
        /// </remarks>
        public string[] Schemas = new string[] { };

        /// <summary>
        /// Configuration for host authority features in the room.
        /// </summary>
        /// <remarks>
        /// Determines which aspects of the simulation are controlled by the host versus the server.
        /// </remarks>
        public HostAuthority HostAuthority;

        /// <summary>
        /// Specifies whether to enable debug streaming features.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Debug streams provide additional diagnostic information during development.
        /// </para>
        /// <para>
        /// The default value is determined by the runtime settings when available.
        /// </para>
        /// </remarks>
        public bool UseDebugStreams
#if UNITY
            = RuntimeSettings.Instance.UseDebugStreams;
#else
            = false;
#endif

        /// <summary>
        /// Gets a default instance of self-hosted room creation options with standard settings.
        /// </summary>
        public new static SelfHostedRoomCreationOptions Default => new SelfHostedRoomCreationOptions();

        internal static SelfHostedRoomCreationOptions FromRoomCreationOptions(RoomCreationOptions roomCreationOptions)
        {
            return new SelfHostedRoomCreationOptions()
            {
                MaxClients = roomCreationOptions.MaxClients,
                KeyValues = roomCreationOptions.KeyValues,
                Tags = roomCreationOptions.Tags,
            };
        }

        internal LocalRoomCreationRequest ToRequest()
        {
            return new LocalRoomCreationRequest()
            {
                UniqueID = UniqueId,
                MaxClients = MaxClients,
                MaxEntities = MaxEntities,
                OutStatsFreq = 1,
                LogStatsFreq = 1,
                SchemaName = string.Empty,
                SchemaTimeout = 60,
                SchemaUrls = new string[0],
                Schemas = Schemas,
                DisconnectTimeout = 6000,
                DebugStreams = UseDebugStreams,
                Frequency = 0,
                MinQueryDistance = 0.1f,
                WebSupport = true,
                CleanupTimeout = CleanupTimeout,
                ProjectID = ProjectId,
                KeyValues = KeyValues,
                Tags = Tags,
                Secret = Secret,
                HostAuthority = HostAuthority.GetCommaSeparatedFeatures(),
            };
        }
    }
#pragma warning restore 649
}
