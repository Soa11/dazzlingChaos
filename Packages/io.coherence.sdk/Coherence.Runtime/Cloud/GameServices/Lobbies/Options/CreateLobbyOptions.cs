// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Options for <see cref="LobbiesService.CreateLobby">creating a lobby</see>
    /// in the coherence Cloud.
    /// </summary>
    public sealed class CreateLobbyOptions
    {
        public static CreateLobbyOptions Default => new();

        /// <summary>
        /// The name for the lobby.
        /// </summary>
        public string Name;

        /// <summary>
        /// Tag for the lobby.
        /// </summary>
        public string Tag;

        /// <summary>
        /// Maximum number of players that the lobby can hold.
        /// </summary>
        public int MaxPlayers;

        /// <summary>
        /// <para>
        /// Specifies whether the lobby should be unlisted or not.
        /// </para>
        /// <para>
        /// Public Lobbies can be matched/searched and then joined by anybody.
        /// Unlisted Lobbies can be joined only if the player knows the Lobby ID.
        /// </para>
        /// </summary>
        public bool Unlisted;

        /// <summary>
        /// (Optional) Password needed to access the lobby.
        /// </summary>
        /// <remarks>
        /// If <see langword="null"/> or empty, the lobby is accessible without a password.
        /// </remarks>
        public string Password;

        /// <summary>
        /// Custom attributes associated with the Lobby.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Public attributes will be returned and visible to all Players.
        /// </para>
        /// <para>
        /// Only the owner of the lobby can change Lobby attributes.
        /// </para>
        /// </remarks>
        public List<CloudAttribute> LobbyAttributes;

        /// <summary>
        /// Player attributes.
        /// </summary>
        /// <remarks>
        /// Public attributes will be returned and visible to all Players.
        /// </remarks>
        public List<CloudAttribute> PlayerAttributes;

        /// <summary>
        /// The region for the lobby.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This can be used to <see cref="LobbyFilter.WithRegion">filter lobbies by region</see>.
        /// </para>
        /// <para>
        /// If the lobby is specific to a <see cref="RoomData">room</see>,
        /// this will default to the region of the room's host.
        /// </para>
        /// </remarks>
        [MaybeNull]
        public string Region;

        /// <summary>
        /// The Simulator Slug associated with the lobby.
        /// </summary>
        /// <remarks>
        /// If the lobby is not specific to a <see cref="RoomData">room</see>,
        /// or the room does not have a Simulator associated with it,
        /// this may be left <see langword="null"/> or empty.
        /// </remarks>
        [MaybeNull]
        public string SimulatorSlug;

        [Obsolete("CreateLobbyOptions.Secret will be removed in a future version. Use CreateLobbyOptions.Password instead.")]
        [Deprecated("08/2025", 2, 1, 0, Reason = "Renamed to Password to differentiate from RoomData.Secret.")]
        public string Secret
        {
            get => Password;
            set => Password = value;
        }

        public CreateLobbyOptions() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateLobbyOptions"/> class.
        /// </summary>
        /// <param name="name"> The name for the lobby. </param>
        /// <param name="region">
        /// <para>
        /// The region for the lobby.
        /// </para>
        /// <para>
        /// This can be used to <see cref="LobbyFilter.WithRegion">filter lobbies by region</see>.
        /// </para>
        /// <para>
        /// If the lobby is associated with a <paramref name="room"/>, this will default to the region of the room's host.
        /// </para>
        /// </param>
        /// <param name="tag"> Tag for the lobby. </param>
        /// <param name="maxPlayers"> Maximum number of players that the lobby can hold. </param>
        /// <param name="unlisted">
        /// <para>
        /// Specifies whether the lobby should be unlisted or not.
        /// </para>
        /// <para>
        /// Public Lobbies can be matched/searched and then joined by anybody.
        /// Unlisted Lobbies can be joined only if the player knows the Lobby ID.
        /// </para>
        /// </param>
        /// <param name="password">
        /// <para>
        /// (Optional) Password needed to access the lobby.
        /// </para>
        /// <para>
        /// If <see langword="null"/> or empty, the lobby is accessible without a password.
        /// </para>
        /// </param>
        /// <param name="lobbyAttributes">
        /// <para>
        /// Attributes for the Lobby.
        /// </para>
        /// <para>
        /// Only the owner of the lobby can change Lobby attributes.
        /// </para>
        /// </param>
        /// <param name="playerAttributes"> Player attributes. </param>
        /// <param name="room">
        /// The room for which the lobby is created, if any; otherwise, <see langword="null"/>.
        /// </param>
        public CreateLobbyOptions(string name, string region, string tag = null, int maxPlayers = 0, bool unlisted = false, string password = null, List<CloudAttribute> lobbyAttributes = null, List<CloudAttribute> playerAttributes = null, RoomData? room = null)
        {
            Name = name;
            Tag = tag;
            MaxPlayers = maxPlayers;
            Unlisted = unlisted;
            Password = password;
            Region = region;
            if (room is { } roomData)
            {
                SimulatorSlug = roomData.SimSlug;
                Region ??= roomData.Host.Region;
            }

            LobbyAttributes = lobbyAttributes;
            PlayerAttributes = playerAttributes;
        }

        /// <summary>
        /// Creates options for a lobby associated with a <see cref="RoomData">room</see>.
        /// </summary>
        /// <param name="room"> The room for which the lobby is created. </param>
        /// <param name="name"> The name for the lobby. If none is provided defaults to the name of the room. </param>
        /// <param name="tag"> Tag for the lobby. </param>
        /// <param name="maxPlayers"> Maximum number of players that the lobby can hold. If none is provided defaults to the maximum players of the room. </param>
        /// <param name="unlisted">
        /// <para>
        /// Specifies whether the lobby should be unlisted or not.
        /// </para>
        /// <para>
        /// Public Lobbies can be matched/searched and then joined by anybody.
        /// Unlisted Lobbies can be joined only if the player knows the Lobby ID.
        /// </para>
        /// </param>
        /// <param name="password">
        /// <para>
        /// (Optional) Password needed to access the lobby.
        /// </para>
        /// <para>
        /// If <see langword="null"/> or empty, the lobby is accessible without a password.
        /// </para>
        /// </param>
        /// <param name="lobbyAttributes">
        /// <para>
        /// Attributes for the Lobby.
        /// </para>
        /// <para>
        /// Only the owner can change Lobby attributes
        /// </para>
        /// </param>
        /// <param name="playerAttributes"> Player attributes. </param>
        /// <returns>
        /// A new instance of <see cref="CreateLobbyOptions"/> initialized with the provided room data and other parameters.
        /// </returns>
        public static CreateLobbyOptions ForRoom(RoomData room, string name = null, string tag = null, int maxPlayers = 0, bool unlisted = false, string password = null, List<CloudAttribute> lobbyAttributes = null, List<CloudAttribute> playerAttributes = null)
            => new(name: name ?? room.RoomName, region: room.Host.Region, tag: tag, maxPlayers: maxPlayers > 0 ? maxPlayers: room.MaxPlayers, unlisted, password: password, lobbyAttributes: lobbyAttributes, playerAttributes: playerAttributes, room);
    }
}
