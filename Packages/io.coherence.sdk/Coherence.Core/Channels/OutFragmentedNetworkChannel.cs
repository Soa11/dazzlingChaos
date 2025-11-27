// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels
{
    using System;
    using System.Collections.Generic;
    using Coherence.Brisk;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common;
    using Coherence.Common.Pooling;
    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Coherence.Serializer.Fragmentation;
    using Coherence.SimulationFrame;
    using Microsoft.IO;

    internal class OutFragmentedNetworkChannel : IOutNetworkChannel
    {
        public const int MaxDefaultTransferRate = (Brisk.DefaultMTU - 45) * // 45 is approx header size
            20; // 20 packets per second
        public const int ChannelPacketOneWayTransferThresholdSeconds = 5;

        public static readonly RecyclableMemoryStreamManager.Options RecyclableMemoryStreamManagerOptions = new()
        {
            BlockSize = Brisk.DefaultMTU,
            LargeBufferMultiple = 1024 * 4, // 4 KB
            MaximumBufferSize = 256 * 1024, // 256 KB
            MaximumSmallPoolFreeBytes = 1 * 1024 * 1024, // 1 MB
            MaximumLargePoolFreeBytes = 1 * 1024 * 1024, // 1 MB
            UseExponentialLargeBuffer = true,
            MaximumStreamCapacity = 0,
        };

        public event Action<Entity> OnEntityAcked;

        private readonly IOutNetworkChannel wrappedChannel;
        private readonly IFragmentationSerializer fragmentationSerializer;

        /// <summary>
        /// Queue of pending in-flight channel packets.
        /// </summary>
        private LinkedList<ChannelPacket> channelPacketsQueue = new();
        private SentCache<Dictionary<ChannelPacketID, FragmentMap>> sentCache;

        private Dictionary<ChannelPacketID, FragmentMap> lastSerializationResult;
        private List<FragmentSection> sentFragmentSectionsCache = new(32);

        /// <summary>
        /// Queue of sent updates returned by the wrapped channel.
        /// Since it is possible to get multiple sent updates from the wrapped channel (by calling Serialize
        /// on the FragmentedChannel twice without calling MarkAsSent), we queue them up and return one
        /// by one when MarkAsSent is called.
        /// Note: We cannot merge these updates because they are the same instance that sits in the sentCache of the wrapped channel.
        /// So merging them would change them in the sentCache of the wrapped channel as well.
        /// </summary>
        private Queue<Dictionary<Entity, OutgoingEntityUpdate>> sentUpdatesQueue = new();

        private readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager =
            new(RecyclableMemoryStreamManagerOptions);
        private readonly Pool<PooledChunkedOutOctetStream> outStreamPool;

        private ChannelPacketID nextChannelPacketID;

        private readonly Logger logger;

        public OutFragmentedNetworkChannel(IOutNetworkChannel wrappedChannel, IFragmentationSerializer fragmentationSerializer, Logger logger)
        {
            this.wrappedChannel = wrappedChannel;
            this.fragmentationSerializer = fragmentationSerializer;
            this.logger = logger.With<OutFragmentedNetworkChannel>();

            this.sentCache = new SentCache<Dictionary<ChannelPacketID, FragmentMap>>(this.logger);

            this.nextChannelPacketID = new ChannelPacketID(1, fragmentationSerializer.MaxChannelPacketID);

            this.outStreamPool = Pool<PooledChunkedOutOctetStream>
                .Builder(pool => new PooledChunkedOutOctetStream(pool, recyclableMemoryStreamManager.GetStream()))
                .Build();

            this.wrappedChannel.OnEntityAcked += entity => OnEntityAcked?.Invoke(entity);
        }

        public void ClearAllChangesForEntity(Entity entity) =>
            wrappedChannel.ClearAllChangesForEntity(entity);

        public void CreateEntity(Entity id, ICoherenceComponentData[] data) =>
            wrappedChannel.CreateEntity(id, data);

        public void DestroyEntity(Entity id, IReadOnlyCollection<Entity> ackedEntities) =>
            wrappedChannel.DestroyEntity(id, ackedEntities);

        public void UpdateComponents(Entity id, ICoherenceComponentData[] data) =>
            wrappedChannel.UpdateComponents(id, data);

        public bool HasChangesForEntity(Entity entity) =>
            wrappedChannel.HasChangesForEntity(entity);

        public void PushCommand(IEntityCommand message, bool useDebugStreams) =>
            wrappedChannel.PushCommand(message, useDebugStreams);

        public void PushInput(IEntityInput message, bool useDebugStreams) =>
            wrappedChannel.PushInput(message, useDebugStreams);

        public void RemoveComponents(Entity id, uint[] componentTypes, Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity) =>
            wrappedChannel.RemoveComponents(id, componentTypes, ackedComponentsPerEntity);

        public bool Serialize(SerializerContext<IOutBitStream> serializerCtx,
            AbsoluteSimulationFrame referenceSimulationFrame, Vector3d floatingOrigin,
            bool holdOnToCommands, IReadOnlyCollection<Entity> ackedEntities)
        {
            lastSerializationResult = new Dictionary<ChannelPacketID, FragmentMap>();

            using (_ = fragmentationSerializer.NewEndOfChannelPacketsReservationScope(serializerCtx))
            {
                var hasMoreSpace = true;
                foreach (var channelPacket in channelPacketsQueue)
                {
                    hasMoreSpace = SerializeChannelPacketFragments(serializerCtx, channelPacket, referenceSimulationFrame, floatingOrigin);

                    if (!hasMoreSpace)
                    {
                        break;
                    }
                }

                if (hasMoreSpace && CanAllocatePacketID())
                {
                    var channelPacket = CreateNewChannelPacket(serializerCtx, referenceSimulationFrame, floatingOrigin, holdOnToCommands, ackedEntities);
                    if (channelPacket.HasValue)
                    {
                        _ = channelPacketsQueue.AddLast(channelPacket.Value);
                        _ = SerializeChannelPacketFragments(serializerCtx, channelPacket.Value, referenceSimulationFrame, floatingOrigin);
                    }
                }

                if (lastSerializationResult.Count > 0)
                {
                    fragmentationSerializer.WriteEndOfChannelPackets(serializerCtx);

                    return true;
                }
            }

            return false;
        }

        private bool SerializeChannelPacketFragments(SerializerContext<IOutBitStream> serializerCtx, ChannelPacket channelPacket,
            AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
        {
            if (!channelPacket.HasPendingFragments())
            {
                return true;
            }

            sentFragmentSectionsCache.Clear();

            // No need to send simFrame and FO if they are the same as the current packet's
            AbsoluteSimulationFrame? simFrame = packetSimulationFrame != channelPacket.ReferenceSimulationFrame ? channelPacket.ReferenceSimulationFrame : null;
            Vector3d? floatingOrigin = packetFloatingOrigin != channelPacket.FloatingOrigin ? channelPacket.FloatingOrigin : null;

            var hasMoreSpace = fragmentationSerializer.SerializeChannelPacketFragments(serializerCtx, simFrame, floatingOrigin,
                channelPacket.ID, channelPacket.Data, channelPacket.PendingFragments.FragmentSections, sentFragmentSectionsCache);

            if (sentFragmentSectionsCache.Count > 0)
            {
                var sentFragmentMap = new FragmentMap();
                sentFragmentMap.AddRange(sentFragmentSectionsCache);
                lastSerializationResult.Add(channelPacket.ID, sentFragmentMap);
            }

            logger.Debug("Serialized channel packet fragments",
                ("channelPacketID", channelPacket.ID),
                ("sentFragments", string.Join(", ", sentFragmentSectionsCache)),
                ("hasMoreSpace", hasMoreSpace));

            return hasMoreSpace;
        }

        private ChannelPacket? CreateNewChannelPacket(SerializerContext<IOutBitStream> originalCtx,
            AbsoluteSimulationFrame referenceSimulationFrame, Vector3d floatingOrigin, bool holdOnToCommands, IReadOnlyCollection<Entity> ackedEntities)
        {
            if (!CanAllocatePacketID())
            {
                return null;
            }

            if (!wrappedChannel.HasChanges(ackedEntities))
            {
                return null;
            }

            var octetStream = outStreamPool.Rent();
            var bitStream = originalCtx.UseDebugStreams
                ? new DebugOutBitStream(new OutBitStream(octetStream))
                        : (IOutBitStream)new OutBitStream(octetStream);

            var serializerCtx = new SerializerContext<IOutBitStream>(bitStream, originalCtx.UseDebugStreams, originalCtx.Logger, originalCtx.ProtocolVersion);
            serializerCtx.PreferredMaxBitCount = GetPreferredMaxBitCount(originalCtx.RemainingBitCount);

            if (!wrappedChannel.Serialize(serializerCtx, referenceSimulationFrame, floatingOrigin, holdOnToCommands, ackedEntities))
            {
                octetStream.ReturnIfPoolable();
                return null;
            }

            bitStream.Flush();

            var channelPacketID = AllocatePacketID();
            var channelPacket = new ChannelPacket(channelPacketID, octetStream, fragmentationSerializer.FragmentSizeInBytes, referenceSimulationFrame, floatingOrigin);

            logger.Debug("Created new channel packet",
                ("channelPacketID", channelPacketID),
                ("sizeBytes", octetStream),
                ("fragmentCount", channelPacket.NumberOfFragments));

            var oneWayTransferTime = octetStream.Position / (float)MaxDefaultTransferRate;
            if (oneWayTransferTime > ChannelPacketOneWayTransferThresholdSeconds)
            {
                logger.Warning(Warning.CoreFragmentationVeryBigData,
                    $"Started transferring {octetStream.Position / 1024}KB of data over a fragmented channel. " +
                    $"It will take more than {(int)(oneWayTransferTime * 2)} seconds for data to be fully transferred to other clients, " +
                    $"with the assumption of default MTU, default send rate and almost no other data taking up bandwidth. " +
                    $"Other data being sent over the fragmented channel will be on hold while this data is being transferred. " +
                    $"Consider exploring a more suitable approach for syncing data of this size.");
            }

            sentUpdatesQueue.Enqueue(wrappedChannel.MarkAsSent(channelPacketID));

            return channelPacket;
        }

        public void ClearLastSerializationResult() => lastSerializationResult = null;

        public bool HasChanges(IReadOnlyCollection<Entity> ackedEntities)
        {
            foreach (var channelPacket in channelPacketsQueue)
            {
                if (channelPacket.HasPendingFragments())
                {
                    return true;
                }
            }

            return wrappedChannel.HasChanges(ackedEntities);
        }

        public Dictionary<Entity, OutgoingEntityUpdate> MarkAsSent(uint sequenceId)
        {
            if (lastSerializationResult == null || lastSerializationResult.Count == 0)
            {
                sentCache.Enqueue(null, sequenceId);
            }
            else
            {
                foreach (var (sentId, sentFragments) in lastSerializationResult)
                {
                    GetInFlightChannelPacket(sentId).PendingFragments.RemoveRange(sentFragments.FragmentSections);
                }

                sentCache.Enqueue(lastSerializationResult, sequenceId);
            }

            if (sentUpdatesQueue.Count > 0)
            {
                return sentUpdatesQueue.Dequeue();
            }

            return null;
        }

        public void OnDeliveryInfo(uint sequenceId, bool wasDelivered, ref HashSet<Entity> ackedEntities, ref Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity)
        {
            if (!sentCache.TryDequeue(sequenceId, out var sentChannelPacketFragments, out var _))
            {
                logger.Error(Error.CoreChannelOutOrderedNetworkChannelAckNoSent);
                return;
            }

            if (sentChannelPacketFragments == null || sentChannelPacketFragments.Count == 0)
            {
                // No fragments were sent, nothing to do.
                return;
            }

            if (!wasDelivered)
            {
                foreach (var (droppedId, droppedFragments) in sentChannelPacketFragments)
                {
                    if (TryGetInFlightChannelPacket(droppedId, out var droppedChannelPacket))
                    {
                        droppedChannelPacket.PendingFragments.AddRange(droppedFragments.FragmentSections);
                    }
                }

                return;
            }

            foreach (var (ackedId, ackedFragments) in sentChannelPacketFragments)
            {
                if (TryGetInFlightChannelPacket(ackedId, out var ackedChannelPacket))
                {
                    ackedChannelPacket.AckedFragments.AddRange(ackedFragments.FragmentSections);
                }
            }

            AckCompleteChannelPacketsAndDropOlder(ref ackedEntities, ref ackedComponentsPerEntity);
        }

        /// <summary>
        /// Checks the queue for fully acked channel packets and before acking it, drops all older packets that are not fully acked.
        /// </summary>
        private void AckCompleteChannelPacketsAndDropOlder(ref HashSet<Entity> ackedEntities, ref Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity)
        {
            var currentChannelPacketNode = channelPacketsQueue.First;
            var countToDrop = 0;

            while (currentChannelPacketNode != null)
            {
                if (currentChannelPacketNode.Value.IsFullyAcked())
                {
                    currentChannelPacketNode = currentChannelPacketNode.Next;

                    for (var i = 0; i < countToDrop; i++)
                    {
                        AckOrDropChannelPacket(ack: false, ref ackedEntities, ref ackedComponentsPerEntity);
                    }

                    AckOrDropChannelPacket(ack: true, ref ackedEntities, ref ackedComponentsPerEntity);

                    countToDrop = 0;
                }
                else
                {
                    countToDrop++;
                    currentChannelPacketNode = currentChannelPacketNode.Next;
                }
            }
        }

        private void AckOrDropChannelPacket(bool ack, ref HashSet<Entity> ackedEntities, ref Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity)
        {
            if (channelPacketsQueue.First == null)
            {
                throw new InvalidOperationException("Cannot drop a channel packet when there are no in-flight packets.");
            }

            var channelPacket = channelPacketsQueue.First.Value;
            channelPacketsQueue.RemoveFirst();

            logger.Debug(ack ? "Acked channel packet" : "Dropped channel packet", ("channelPacketID", channelPacket.ID));

            wrappedChannel.OnDeliveryInfo(channelPacket.ID, ack, ref ackedEntities, ref ackedComponentsPerEntity);

            channelPacket.Data.ReturnIfPoolable();
        }

        /// <summary>
        /// Returns the number of bits a channel packet can use to exactly fit into the remaining space in the packet.
        /// This is calculated by subtracting the fragmentation header size from the remaining bit count.
        /// </summary>
        /// <param name="remainingBitCount">Number of bits remaining the given packet (with reserved bits already subtracted).</param>
        private uint GetPreferredMaxBitCount(uint remainingBitCount)
        {
            for (var fragmentCount = 1u; fragmentCount < remainingBitCount; fragmentCount++)
            {
                var headerSize = fragmentationSerializer.GetHeaderSizeInBits(
                    fragmentIndex: 0, // for new channel packet, the index will be 0.
                    fragmentCount,
                    includesLastFragment: true, // we are trying to fit the whole packet, so the last fragment is included.
                    includesSimFrame: false, // for new channel packets, the simulation frame is not included.
                    includesFO: false); // for new channel packets, the floating origin is not included.
                var size = headerSize + fragmentCount * fragmentationSerializer.FragmentSizeInBytes * 8;

                if (size > remainingBitCount)
                {
                    return (fragmentCount - 1) * fragmentationSerializer.FragmentSizeInBytes * 8;
                }
            }

            return remainingBitCount;
        }

        public void Reset()
        {
            foreach (var channelPacket in channelPacketsQueue)
            {
                channelPacket.Data.ReturnIfPoolable();
            }

            this.channelPacketsQueue = new LinkedList<ChannelPacket>();
            this.sentCache = new SentCache<Dictionary<ChannelPacketID, FragmentMap>>(this.logger);
            this.sentUpdatesQueue = new Queue<Dictionary<Entity, OutgoingEntityUpdate>>();
            this.nextChannelPacketID = new ChannelPacketID(1, fragmentationSerializer.MaxChannelPacketID);

            this.wrappedChannel.Reset();
        }

        private bool CanAllocatePacketID()
        {
            return channelPacketsQueue.Count < fragmentationSerializer.MaxInFlightChannelPackets;
        }

        private ChannelPacketID AllocatePacketID()
        {
            if (!CanAllocatePacketID())
            {
                throw new InvalidOperationException("Cannot allocate a new channel packet ID since all IDs are currently taken.");
            }

            var allocatedId = nextChannelPacketID;
            nextChannelPacketID = nextChannelPacketID.Next();

            return allocatedId;
        }

        private ChannelPacket GetInFlightChannelPacket(ChannelPacketID id)
        {
            if (TryGetInFlightChannelPacket(id, out var channelPacket))
            {
                return channelPacket;
            }

            throw new InvalidOperationException($"No in-flight channel packet found with ID {id}.");
        }

        private bool TryGetInFlightChannelPacket(ChannelPacketID id, out ChannelPacket channelPacket)
        {
            foreach (var packet in channelPacketsQueue)
            {
                if (packet.ID == id)
                {
                    channelPacket = packet;
                    return true;
                }
            }

            channelPacket = default;
            return false;
        }

        private struct ChannelPacket
        {
            public ChannelPacketID ID { get; private set; }
            public IOutOctetStream Data { get; private set; }
            public FragmentMap PendingFragments { get; private set; }
            public FragmentMap AckedFragments { get; private set; }

            public AbsoluteSimulationFrame ReferenceSimulationFrame { get; private set; }
            public Vector3d FloatingOrigin { get; private set; }

            public uint NumberOfFragments { get; private set; }

            public ChannelPacket(ChannelPacketID id, IOutOctetStream data, uint fragmentSizeInBytes,
                AbsoluteSimulationFrame referenceSimulationFrame, Vector3d floatingOrigin)
            {
                ID = id;
                Data = data;

                NumberOfFragments = (uint)Math.Ceiling((double)data.Position / fragmentSizeInBytes);

                var pendingFragments = new FragmentMap();
                pendingFragments.Add(0, NumberOfFragments);

                PendingFragments = pendingFragments;
                AckedFragments = new FragmentMap();
                ReferenceSimulationFrame = referenceSimulationFrame;
                FloatingOrigin = floatingOrigin;
            }

            public readonly bool HasPendingFragments()
            {
                return PendingFragments.FragmentSections.Count > 0;
            }

            public readonly bool IsFullyAcked()
            {
                return AckedFragments.FragmentSections.Count == 1 &&
                    AckedFragments.FragmentSections[0].Index == 0 &&
                    AckedFragments.FragmentSections[0].Count == NumberOfFragments;
            }
        }
    }
}
