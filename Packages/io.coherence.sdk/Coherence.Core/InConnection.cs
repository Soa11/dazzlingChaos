// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using Brook;
    using Brook.Octet;
    using Serializer;
    using SimulationFrame;
    using ProtocolDef;
    using Entities;
    using Common;
    using Log;
    using Channels;

    public class InConnection
    {
        private const int FULL_PACKET_MARGIN = 128;

        public event Action<List<IncomingEntityUpdate>> OnEntityUpdate;
        public event Action<IEntityCommand> OnCommand;
        public event Action<IEntityInput> OnInput;
        public event Action<AbsoluteSimulationFrame> OnServerSimulationFrameReceived;

        internal event Action<int> OnPacketReceived;

        private readonly IEntityRegistry entityRegistry;
        private readonly SortedList<ChannelID, IInNetworkChannel> channels = new();

        private readonly RefsResolver refsResolver;

        private int octetStreamWarnThreshold;

        private readonly Logger logger;

        // Cache for FlushChangeBuffer so we don't need to re-allocate every time
        private readonly List<RefsInfo> allRefsInfos = new();

        internal InConnection(IEntityRegistry entityRegistry, Dictionary<ChannelID, IInNetworkChannel> channels, Logger logger)
        {
            this.entityRegistry = entityRegistry;
            this.logger = logger.With<InConnection>();
            this.refsResolver = new RefsResolver(this.logger);

            foreach (var (channelID, channel) in channels)
            {
                AddChannel(channelID, channel);
            }
        }

        private void AddChannel(ChannelID channelID, IInNetworkChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel), "channel must not be null");
            }

            if (!channelID.IsValid())
            {
                throw new ArgumentException($"Invalid ChannelID {channelID}, only channels {ChannelID.MinValue}-{ChannelID.MaxValue} are supported");
            }

            if (!channels.TryAdd(channelID, channel))
            {
                throw new Exception($"Failed to add channel, duplicate ChannelID {channelID}");
            }

            channel.OnEntityUpdate += update => OnEntityUpdate?.Invoke(update);
            channel.OnCommand += (command) => OnCommand?.Invoke(command);
            channel.OnInput += (input) => OnInput?.Invoke(input);
        }

        public void ProcessIncomingPacket(IInOctetStream octetStream)
        {
            var basicHeader = PacketHeaderReader.DeserializeBasicHeader(octetStream);
            var packetSimulationFrame = basicHeader.SimulationFrame;
            OnServerSimulationFrameReceived?.Invoke(packetSimulationFrame);

            var totalSize = octetStream.RemainingOctetCount;
            var decoded = PacketHeaderReader.ToPacketHeaderInfo(octetStream, basicHeader);
            var bitStream = decoded.Stream;

            var floatingOrigin = Deserialize.ReadFloatingOrigin(bitStream, this.logger);
            if (double.IsNaN(floatingOrigin.x) || double.IsNaN(floatingOrigin.y) || double.IsNaN(floatingOrigin.z))
            {
                logger.Warning(Warning.CoreInConnectionFloatingOriginNaN, ("received origin", floatingOrigin));
                floatingOrigin = Vector3d.zero;
            }

            // SDK always uses the latest protocol version, but when we transition to CommonCore this check is needed.
            var protocolVersion = ProtocolDef.Version.CurrentVersion;
            var gotEntityUpdate = protocolVersion >= ProtocolDef.Version.VersionIncludesChannelID
                ? ReadMultipleChannels(bitStream, packetSimulationFrame, floatingOrigin)
                : ReadSingleChannel(bitStream, packetSimulationFrame, floatingOrigin, ChannelID.Default);

            octetStream.ReturnIfPoolable();

            FlushAll();

            if (!gotEntityUpdate && totalSize >= octetStreamWarnThreshold)
            {
                logger.Warning(Warning.CoreInConnectionPacketFullOfMessages);
            }

            OnPacketReceived?.Invoke(totalSize);
        }

        private bool ReadSingleChannel(IInBitStream bitStream, AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin, ChannelID channelID)
        {
            if (!channels.TryGetValue(channelID, out var channel))
            {
                throw new Exception($"Unexpected channelID: {channelID} does not exist");
            }

            return channel.Deserialize(bitStream, packetSimulationFrame, packetFloatingOrigin);
        }

        private bool ReadMultipleChannels(IInBitStream bitStream, AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
        {
            var gotEntityUpdate = false;

            while (Deserialize.ReadChannelID(bitStream, out var channelID))
            {
                gotEntityUpdate |= ReadSingleChannel(bitStream, packetSimulationFrame, packetFloatingOrigin, channelID);
            }

            return gotEntityUpdate;
        }

        internal void Clear()
        {
            foreach (var channel in channels.Values)
            {
                channel.Clear();
            }
        }

        internal void SetMaximumTransmissionUnit(int mtu)
        {
            octetStreamWarnThreshold = mtu - FULL_PACKET_MARGIN;
        }

        private void FlushAll()
        {
            while (FlushChangeBuffer())
            {
                // Flush while something was flushed, to make sure to completely service all channels
            }
        }

        private bool FlushChangeBuffer()
        {
            var didFlushAnything = false;

            allRefsInfos.Clear();
            foreach (var channel in channels.Values)
            {
                var refs = channel.GetRefsInfos();
                if (refs != null)
                {
                    allRefsInfos.AddRange(refs);
                }
            }

            refsResolver.Resolve(allRefsInfos, entityRegistry);

            foreach (var channel in channels.Values)
            {
                didFlushAnything |= channel.FlushBuffer(refsResolver.ResolvableEntities);
            }

            return didFlushAnything;
        }
    }
}
