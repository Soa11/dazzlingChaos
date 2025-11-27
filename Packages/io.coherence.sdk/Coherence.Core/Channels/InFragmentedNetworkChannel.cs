// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common;
    using Coherence.Common.Pooling;
    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.ProtocolDef;
    using Coherence.Serializer.Fragmentation;
    using Coherence.SimulationFrame;
    using Coherence.Transport;
    using Microsoft.IO;

    internal class InFragmentedNetworkChannel : IInNetworkChannel
    {
        public event Action<List<IncomingEntityUpdate>> OnEntityUpdate;
        public event Action<IEntityCommand> OnCommand;
        public event Action<IEntityInput> OnInput;

        private readonly IInNetworkChannel wrappedChannel;
        private readonly IFragmentationSerializer fragmentationSerializer;

        /// <summary>
        /// Queue of pending in-flight channel packets.
        /// </summary>
        private LinkedList<ChannelPacket> channelPacketsQueue = new();
        private ChannelPacketID lastAckedChannelPacketID;

        private readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager =
            new(OutFragmentedNetworkChannel.RecyclableMemoryStreamManagerOptions);
        private readonly Pool<PooledChunkedOutOctetStream> outStreamPool;

        private readonly Logger logger;

        public InFragmentedNetworkChannel(IInNetworkChannel wrappedChannel, IFragmentationSerializer fragmentationSerializer, Logger logger)
        {
            this.wrappedChannel = wrappedChannel;
            this.fragmentationSerializer = fragmentationSerializer;
            this.lastAckedChannelPacketID = new ChannelPacketID(fragmentationSerializer.MaxChannelPacketID, fragmentationSerializer.MaxChannelPacketID);
            this.logger = logger.With<InFragmentedNetworkChannel>();

            this.wrappedChannel.OnEntityUpdate += updates => OnEntityUpdate?.Invoke(updates);
            this.wrappedChannel.OnCommand += command => OnCommand?.Invoke(command);
            this.wrappedChannel.OnInput += input => OnInput?.Invoke(input);

            this.outStreamPool = Pool<PooledChunkedOutOctetStream>
                .Builder(pool => new PooledChunkedOutOctetStream(pool, recyclableMemoryStreamManager.GetStream()))
                .Build();
        }

        public bool FlushBuffer(IReadOnlyCollection<Entity> resolvableEntities) =>
            this.wrappedChannel.FlushBuffer(resolvableEntities);

        public List<RefsInfo> GetRefsInfos() =>
            this.wrappedChannel.GetRefsInfos();

        public bool Deserialize(IInBitStream stream, AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
        {
            ChannelPacketID channelPacketId;
            while ((channelPacketId = fragmentationSerializer.ReadChannelPacketID(stream)) != ChannelPacketID.EndOfChannelPackets)
            {
                DeserializeChannelPacket(stream, channelPacketId, packetSimulationFrame, packetFloatingOrigin);
            }

            return AckCompleteChannelPacketsAndDropOlder();
        }

        private void DeserializeChannelPacket(IInBitStream stream, ChannelPacketID channelPacketId,
            AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
        {
            if (!TryGetChannelPacket(channelPacketId, out var channelPacket))
            {
                // The channel packet is stale, just ignore it, but we still need to read the stream to advance it.
                _ = fragmentationSerializer.DeserializeChannelPacketFragments(stream, null, logger, out _, out _, out _, out _);

                return;
            }

            var fragmentSection = fragmentationSerializer.DeserializeChannelPacketFragments(stream, channelPacket.Data, logger,
                out var receivedLast, out var receivedFirst, out var referenceSimulationFrame, out var floatingOrigin);

            channelPacket.AckedFragments.Add(fragmentSection);
            channelPacket.IsLastAcked |= receivedLast;

            if (receivedFirst)
            {
                channelPacket.ReferenceSimulationFrame = referenceSimulationFrame ?? packetSimulationFrame;
                channelPacket.FloatingOrigin = floatingOrigin ?? packetFloatingOrigin;
            }

            logger.Debug("Deserialized channel packet fragments",
                ("channelPacketID", channelPacketId),
                ("fragmentSection", fragmentSection),
                ("receivedLast", receivedLast));
        }

        /// <summary>
        /// Checks the queue for fully acked channel packets and before acking it,
        /// drops all older packets that are not fully acked.
        /// </summary>
        private bool AckCompleteChannelPacketsAndDropOlder()
        {
            var gotEntityUpdate = false;
            var currentChannelPacketNode = channelPacketsQueue.First;
            var countToDrop = 0;

            while (currentChannelPacketNode != null)
            {
                if (currentChannelPacketNode.Value.IsFullyAcked())
                {
                    currentChannelPacketNode = currentChannelPacketNode.Next;

                    for (var i = 0; i < countToDrop; i++)
                    {
                        var channelPacketID = ReleaseChannelPacket();

                        logger.Debug("Dropped channel packet",
                            ("channelPacketID", channelPacketID));
                    }

                    gotEntityUpdate |= PushChannelPacket();
                    _ = ReleaseChannelPacket();

                    countToDrop = 0;
                }
                else
                {
                    countToDrop++;
                    currentChannelPacketNode = currentChannelPacketNode.Next;
                }
            }

            return gotEntityUpdate;
        }

        public void Clear()
        {
            foreach (var channelPacket in channelPacketsQueue)
            {
                channelPacket.Data.ReturnIfPoolable();
            }

            this.channelPacketsQueue = new LinkedList<ChannelPacket>();
            this.lastAckedChannelPacketID = new ChannelPacketID(fragmentationSerializer.MaxChannelPacketID, fragmentationSerializer.MaxChannelPacketID);

            this.wrappedChannel.Clear();
        }

        private bool TryGetChannelPacket(ChannelPacketID id, out ChannelPacket channelPacket)
        {
            if (!lastAckedChannelPacketID.IsValidSuccessor(id))
            {
                logger.Debug("Ignoring stale channel packet",
                    ("channelPacketID", id),
                    ("lastAckedChannelPacketId", lastAckedChannelPacketID));

                // The channel packet is older than the last acked packet, ignore it.
                channelPacket = null;
                return false;
            }

            foreach (var packet in channelPacketsQueue)
            {
                if (packet.ID == id)
                {
                    channelPacket = packet;
                    return true;
                }
            }

            // The channel packet is not in the queue, create a new one,
            // but first create all the missing packets up to this one.

            var nextId = channelPacketsQueue.Last?.Value?.ID.Next() ?? lastAckedChannelPacketID.Next();
            while (nextId.IsValidSuccessor(id) || nextId.Equals(id))
            {
                var buffer = outStreamPool.Rent();
                var newChannelPacket = new ChannelPacket(nextId, buffer);
                _ = channelPacketsQueue.AddLast(newChannelPacket);

                logger.Debug("Created new channel packet",
                    ("channelPacketID", nextId));

                nextId = nextId.Next();
            }

            channelPacket = channelPacketsQueue.Last.Value;
            return true;
        }

        private ChannelPacketID ReleaseChannelPacket()
        {
            if (channelPacketsQueue.First == null)
            {
                throw new InvalidOperationException("No channel packets to release.");
            }

            var channelPacketID = channelPacketsQueue.First.Value.ID;

            channelPacketsQueue.First.Value.Data.ReturnIfPoolable();
            channelPacketsQueue.RemoveFirst();

            return channelPacketID;
        }

        private bool PushChannelPacket()
        {
            if (channelPacketsQueue.First == null)
            {
                throw new InvalidOperationException("No channel packets to push.");
            }

            var channelPacket = channelPacketsQueue.First.Value;

            lastAckedChannelPacketID = channelPacket.ID;

            if (channelPacket.ReferenceSimulationFrame == null || channelPacket.FloatingOrigin == null)
            {
                throw new Exception("Channel packet's simFrame or floatingOrigin is null, but the packet is fully acked. This should not happen.");
            }

            channelPacket.Data.Seek(channelPacket.OctetCount);
            var inOctetStream = new InOctetStream(channelPacket.Data.Close());
            var bitStream = new InBitStream(inOctetStream, (int)channelPacket.OctetCount * 8);

            logger.Debug("Pushing fully received channel packet",
                ("channelPacketID", channelPacket.ID),
                ("octetCount", channelPacket.OctetCount));

            return this.wrappedChannel.Deserialize(bitStream, channelPacket.ReferenceSimulationFrame.Value, channelPacket.FloatingOrigin.Value);
        }

        private class ChannelPacket
        {
            public ChannelPacketID ID { get; private set; }
            public IOutOctetStream Data { get; private set; }
            public FragmentMap AckedFragments { get; private set; }

            public AbsoluteSimulationFrame? ReferenceSimulationFrame { get; set; }
            public Vector3d? FloatingOrigin { get; set; }

            public uint OctetCount => !IsLastAcked ? 0 : AckedFragments.FragmentSections[^1].LastIndex + 1;

            public bool IsLastAcked { get; set; }

            public ChannelPacket(ChannelPacketID id, IOutOctetStream buffer)
            {
                ID = id;
                Data = buffer;
                AckedFragments = new FragmentMap();
                IsLastAcked = false;
                ReferenceSimulationFrame = null;
                FloatingOrigin = null;
            }

            public bool IsFullyAcked()
            {
                return IsLastAcked && AckedFragments.FragmentSections.Count == 1 &&
                    AckedFragments.FragmentSections[0].Index == 0;
            }
        }
    }
}
