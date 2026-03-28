// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence
{
    using System;

    public class Channel
    {
        public static readonly Channel Voice = new(
            id: ChannelID.Voice,
            name: "Voice",
            description: "Reliable channel that guarantees eventual consistency, but does not guarantee receive order. " +
                "Handles data of any size by splitting it into fragments. " +
                "Should be used only for sending voice data, since it has top priority.",
            supportsEntitySyncing: false); // Even though it could support entity syncing in theory, we don't want to encourage it.

        public static readonly Channel Default = new(
            id: ChannelID.Default,
            name: "Default",
            description: "Default reliable channel that guarantees eventual consistency, but does not guarantee receive order.",
            supportsEntitySyncing: true);

        public static readonly Channel Ordered = new(
            id: ChannelID.Ordered,
            name: "Ordered",
            description: "Reliable channel that guarantees receive order of sent commands. " +
                "(Does not support entity syncing yet.)",
            supportsEntitySyncing: false);

        public static readonly Channel Fragmented = new(
            id: ChannelID.Fragmented,
            name: "Fragmented",
            description: "Reliable channel that guarantees eventual consistency, but does not guarantee receive order. " +
                "Handles data of any size by splitting it into fragments. " +
                "Shouldn't be used for data requiring low latency.",
            supportsEntitySyncing: true);

        public static readonly Channel FragmentedOrdered = new(
            id: ChannelID.FragmentedOrdered,
            name: "Fragmented Ordered",
            description: "Reliable channel that guarantees receive order of sent commands. " +
                "Handles data of any size by splitting it into fragments. " +
                "Shouldn't be used for data requiring low latency. " +
                "(Does not support entity syncing yet.)",
            supportsEntitySyncing: false);

        private static readonly Channel[] builtInChannels = new Channel[]
        {
            Default,
            Ordered,
            Fragmented,
            FragmentedOrdered
        };

        public ChannelID ID { get; }
        public string Name { get; }
        public string Description { get; }

        /// <summary>
        /// If false, the channel does not support full entity syncing, meaning
        /// it supports commands only.
        /// </summary>
        public bool SupportsEntitySyncing { get; }

        public Channel(
            ChannelID id,
            string name,
            string description,
            bool supportsEntitySyncing)
        {
            ID = id;
            Name = name;
            Description = description;
            SupportsEntitySyncing = supportsEntitySyncing;
        }

        public static Channel[] GetAll() => builtInChannels;

        public static Channel GetByName(string name)
        {
            if (!TryGetByName(name, out var channel))
            {
                throw new ArgumentException($"Channel with name '{name}' does not exist.", nameof(name));
            }

            return channel;
        }

        public static bool TryGetByName(string name, out Channel channel)
        {
            channel = null;

            foreach (var c in builtInChannels)
            {
                if (c.Name == name)
                {
                    if (channel != null)
                    {
                        throw new ArgumentException($"Multiple channels with name '{name}' exist.", nameof(name));
                    }

                    channel = c;
                }
            }

            if (channel == null)
            {
                return false;
            }

            return true;
        }

        public static Channel GetByID(ChannelID id)
        {
            Channel channel = null;

            foreach (var c in builtInChannels)
            {
                if (c.ID == id)
                {
                    if (channel != null)
                    {
                        throw new ArgumentException($"Multiple channels with id {id} exist.", nameof(id));
                    }

                    channel = c;
                }
            }

            if (channel == null)
            {
                throw new ArgumentException($"Channel with id {id} does not exist.", nameof(id));
            }

            return channel;
        }

        public override string ToString() => $"{Name}[{ID}]";

        public static implicit operator ChannelID(Channel channel) => channel.ID;
    }
}
