// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common;
    using Coherence.Core;
    using Coherence.Core.Channels;
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Coherence.Serializer.Fragmentation;
    using Coherence.SimulationFrame;
    using Coherence.Tests;
    using NUnit.Framework;

    public class FragmentedChannelsIntegTests : CoherenceTest
    {
        private InFragmentedNetworkChannel inChannel;
        private OutFragmentedNetworkChannel outChannel;

        private InChannelMock inChannelMock;
        private OutChannelMock outChannelMock;

        private Vector3d currentFloatingOrigin;

        private uint nextSendPacketSequenceId;
        private uint nextAckPacketSequenceId;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            inChannelMock = new InChannelMock();
            outChannelMock = new OutChannelMock();

            inChannel = new InFragmentedNetworkChannel(inChannelMock, new FragmentationSerializer(), logger);
            outChannel = new OutFragmentedNetworkChannel(outChannelMock, new FragmentationSerializer(), logger);

            currentFloatingOrigin = Vector3d.zero;
            nextSendPacketSequenceId = 1;
            nextAckPacketSequenceId = 1;
        }

        public ArraySegment<byte> Send(int packetSize = 1000, AbsoluteSimulationFrame? simulationFrame = null, Vector3d? floatingOrigin = null)
        {
            var stream = new OutOctetStream(new byte[packetSize]);
            var bitStream = new OutBitStream(stream);
            var ctx = new SerializerContext<IOutBitStream>(bitStream, false, logger);

            _ = outChannel.Serialize(ctx, simulationFrame ?? (AbsoluteSimulationFrame)0, floatingOrigin ?? Vector3d.zero, false, null);
            ctx.BitStream.Flush();

            _ = outChannel.MarkAsSent(nextSendPacketSequenceId++);

            return stream.Close();
        }

        public void Drop()
        {
            var ackedEntities = new HashSet<Entity>();
            var ackedComponentsPerEntity = new Dictionary<Entity, HashSet<uint>>();

            outChannel.OnDeliveryInfo(nextAckPacketSequenceId++, false, ref ackedEntities, ref ackedComponentsPerEntity);
        }

        public void Arrive(ArraySegment<byte> packet, AbsoluteSimulationFrame? simulationFrame = null, Vector3d? floatingOrigin = null)
        {
            var stream = new InOctetStream(packet);
            var bitStream = new InBitStream(stream, packet.Count * 8);

            _ = inChannel.Deserialize(bitStream, simulationFrame ?? (AbsoluteSimulationFrame)0, floatingOrigin ?? Vector3d.zero);

            var ackedEntities = new HashSet<Entity>();
            var ackedComponentsPerEntity = new Dictionary<Entity, HashSet<uint>>();

            outChannel.OnDeliveryInfo(nextAckPacketSequenceId++, true, ref ackedEntities, ref ackedComponentsPerEntity);
        }

        private byte[] CreateData(int size)
        {
            var data = new byte[size];

            for (var i = 0; i < size; i++)
            {
                data[i] = (byte)(i % (byte.MaxValue + 1));
            }

            return data;
        }

        [Test]
        [Description("Verifies that small data can be sent and received correctly without fragmentation")]
        public void SmallData()
        {
            var data = CreateData(10);
            outChannelMock.PendingData = data;

            var packet = Send(packetSize: 1000);
            Arrive(packet);

            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(1));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data));
        }

        [Test]
        [Description("Verifies that a large data can be sent and received correctly without fragmentation")]
        public void LargeData_FullyFits()
        {
            var data = CreateData(64000);
            outChannelMock.PendingData = data;

            var packet = Send(packetSize: 100000);
            Arrive(packet);

            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(1));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data));
        }

        [Test]
        [Description("Verifies that a large data is fragmented and received correctly")]
        public void MultipleFragments()
        {
            var data = CreateData(1000);
            outChannelMock.PendingData = data;

            var packet = Send(packetSize: 300);
            Arrive(packet);

            Assert.That(inChannelMock.ReceivedDataList, Is.Empty);

            packet = Send(packetSize: 300);
            Arrive(packet);

            Assert.That(inChannelMock.ReceivedDataList, Is.Empty);

            packet = Send(packetSize: 300);
            Arrive(packet);

            Assert.That(inChannelMock.ReceivedDataList, Is.Empty);

            packet = Send(packetSize: 300);
            Arrive(packet);

            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(1));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data));
        }

        [Test]
        [Description("Verifies that dropped fragments are resent and received correctly")]
        public void MultipleFragments_Drops()
        {
            var data = CreateData(1000);
            outChannelMock.PendingData = data;

            var packet = Send(packetSize: 300);
            Arrive(packet);

            Assert.That(inChannelMock.ReceivedDataList, Is.Empty);

            _ = Send(packetSize: 300);
            Drop();

            Assert.That(inChannelMock.ReceivedDataList, Is.Empty);

            packet = Send(packetSize: 300);
            Arrive(packet);

            Assert.That(inChannelMock.ReceivedDataList, Is.Empty);

            packet = Send(packetSize: 300);
            Arrive(packet);

            Assert.That(inChannelMock.ReceivedDataList, Is.Empty);

            packet = Send(packetSize: 300);
            Arrive(packet);

            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(1));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data));
        }

        [Test]
        [Description("Verifies that multiple channel packets can be in-flight at once and both arrive")]
        public void MultipleChannelPackets_BothArrive()
        {
            var data1 = CreateData(1000);
            var data2 = CreateData(2000);
            outChannelMock.PendingData = data1;

            _ = Send(packetSize: 600);

            outChannelMock.PendingData = data2;
            var packet2 = Send(packetSize: 1000); // this should include both data1 and partially data2

            Drop(); // packet1
            Arrive(packet2);

            Assert.That(inChannelMock.ReceivedDataList, Is.Empty); // nothing arrived yet

            var packet3 = Send(packetSize: 3000); // enough to fit dropped data1 and the rest of data2
            Arrive(packet3);

            outChannelMock.ExpectDelivery(true);
            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(2));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data1));
            Assert.That(inChannelMock.ReceivedDataList[1].data, Is.EqualTo(data2));
        }

        [Test]
        [Description("Verifies that multiple channel packets can be in-flight at once and " +
            "if the second arrives before the first, that the first channel packet is dropped completely")]
        public void MultipleChannelPackets_FirstDropped()
        {
            var data1 = CreateData(1000);
            var data2 = CreateData(2000);
            outChannelMock.PendingData = data1;

            _ = Send(packetSize: 600); // packet1: part of data1

            outChannelMock.PendingData = data2;
            var packet2 = Send(packetSize: 1000); // packet2: rest of data1 and part of data2

            var packet3 = Send(packetSize: 1500); // packet3: rest of data2

            Drop(); // packet1
            Arrive(packet2);
            Arrive(packet3);

            outChannelMock.ExpectDelivery(false); // data1 is dropped because data2 arrived before
            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(1));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data2));
        }

        [Test]
        [Description("Verifies that simulationFrame and floatingOrigin are correctly supplied " +
            "if the first fragment is not dropped.")]
        public void SimulationFrameAndFloatingOrigin_NoDrop()
        {
            var simulationFrame1 = (AbsoluteSimulationFrame)1000;
            var floatingOrigin1 = new Vector3d(100, 200, 300);
            this.currentFloatingOrigin = floatingOrigin1;

            var data = CreateData(10);
            outChannelMock.PendingData = data;

            var packet1 = Send(packetSize: 1000, simulationFrame: simulationFrame1, floatingOrigin: floatingOrigin1);
            Arrive(packet1, simulationFrame: simulationFrame1, floatingOrigin: floatingOrigin1);

            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(1));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data));
            Assert.That(inChannelMock.ReceivedDataList[0].simFrame, Is.EqualTo(simulationFrame1));
            Assert.That(inChannelMock.ReceivedDataList[0].fo, Is.EqualTo(floatingOrigin1));
        }

        [Test]
        [Description("Verifies that simulationFrame and floatingOrigin are correctly supplied " +
            "if the first fragment is not dropped and resent.")]
        public void SimulationFrameAndFloatingOrigin_Drop()
        {
            var simulationFrame1 = (AbsoluteSimulationFrame)1000;
            var floatingOrigin1 = new Vector3d(100, 200, 300);
            var simulationFrame2 = (AbsoluteSimulationFrame)2000;
            var floatingOrigin2 = new Vector3d(111, 222, 333);
            this.currentFloatingOrigin = floatingOrigin1;

            var data = CreateData(10);
            outChannelMock.PendingData = data;

            _ = Send(packetSize: 1000, simulationFrame: simulationFrame1, floatingOrigin: floatingOrigin1);
            Drop();

            var packet2 = Send(packetSize: 1000, simulationFrame: simulationFrame2, floatingOrigin: floatingOrigin2);
            Arrive(packet2, simulationFrame: simulationFrame2, floatingOrigin: floatingOrigin2);

            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(1));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data));
            Assert.That(inChannelMock.ReceivedDataList[0].simFrame, Is.EqualTo(simulationFrame1));
            Assert.That(inChannelMock.ReceivedDataList[0].fo, Is.EqualTo(floatingOrigin1));
        }

        [Test]
        [Description("Verifies that getting the channel packets out of order, and the older channel packet gets " +
            "fully acked first, that the newer channel packet can get fully acked and pushed too.")]
        public void OutOfOrder_BothReceived()
        {
            var data1 = CreateData(200);
            var data2 = CreateData(2000);
            outChannelMock.PendingData = data1;

            _ = Send(packetSize: 300); // packet1: data1

            outChannelMock.PendingData = data2;
            var packet2 = Send(packetSize: 1000); // packet2: part of data2

            Drop(); // packet1
            Arrive(packet2);

            var packet3 = Send(packetSize: 300); // packet3: again data1 and a bit of data2

            Arrive(packet3);

            var packet4 = Send(packetSize: 1500); // packet3: rest of data2

            Arrive(packet4);

            outChannelMock.ExpectDelivery(true);
            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(2));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data1));
            Assert.That(inChannelMock.ReceivedDataList[1].data, Is.EqualTo(data2));
        }

        [Test]
        [Description("Verifies that getting the channel packets out of order, and the newer channel packet gets " +
            "fully acked before the older packet is received, that the older channel packet is dropped.")]
        public void OutOfOrder_NewerReceived_OlderDropped()
        {
            var data1 = CreateData(200);
            var data2 = CreateData(1000);
            outChannelMock.PendingData = data1;

            _ = Send(packetSize: 300); // packet1: data1

            outChannelMock.PendingData = data2;
            var packet2 = Send(packetSize: 1500); // packet2: data2

            Drop(); // packet1

            var packet3 = Send(packetSize: 300); // packet3: again data1

            Arrive(packet2);
            Arrive(packet3);

            outChannelMock.ExpectDelivery(false);
            outChannelMock.ExpectDelivery(true);
            Assert.That(inChannelMock.ReceivedDataList, Has.Count.EqualTo(1));
            Assert.That(inChannelMock.ReceivedDataList[0].data, Is.EqualTo(data2));
        }

        private class InChannelMock : IInNetworkChannel
        {
            public event Action<List<IncomingEntityUpdate>> OnEntityUpdate { add { } remove { } }
            public event Action<IEntityCommand> OnCommand { add { } remove { } }
            public event Action<IEntityInput> OnInput { add { } remove { } }

            public List<(byte[] data, AbsoluteSimulationFrame simFrame, Vector3d fo)> ReceivedDataList = new();

            public bool Deserialize(IInBitStream stream, AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
            {
                var buffer = new byte[stream.RemainingBits() / 8];

                stream.ReadBytesUnaligned(buffer, stream.RemainingBits());

                ReceivedDataList.Add((buffer, packetSimulationFrame, packetFloatingOrigin));

                return false;
            }

            public void Clear() => throw new NotImplementedException();
            public bool FlushBuffer(IReadOnlyCollection<Entity> resolvableEntities) => throw new NotImplementedException();
            public List<RefsInfo> GetRefsInfos() => throw new NotImplementedException();
        }

        private class OutChannelMock : IOutNetworkChannel
        {
            public event Action<Entity> OnEntityAcked { add { } remove { } }

            public byte[] PendingData;
            public Queue<bool> Deliveries = new();

            public bool HasChanges(IReadOnlyCollection<Entity> ackedEntities) => PendingData != null;

            public bool Serialize(SerializerContext<IOutBitStream> serializerCtx, AbsoluteSimulationFrame referenceSimulationFrame,
                Vector3d floatingOrigin, bool holdOnToCommands, IReadOnlyCollection<Entity> ackedEntities)
            {
                if (PendingData == null || PendingData.Length == 0)
                {
                    return false;
                }

                serializerCtx.BitStream.WriteBytesUnaligned(PendingData, PendingData.Length * 8);
                serializerCtx.BitStream.Flush();

                return true;
            }

            public Dictionary<Entity, OutgoingEntityUpdate> MarkAsSent(uint sequenceId)
            {
                PendingData = null;

                return null;
            }

            public void OnDeliveryInfo(uint sequenceId, bool wasDelivered, ref HashSet<Entity> ackedEntities, ref Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity)
            {
                Deliveries.Enqueue(wasDelivered);
            }

            public void ExpectDelivery(bool wasDelivered)
            {
                Assert.That(Deliveries.Dequeue(), Is.EqualTo(wasDelivered));
            }

            public void ClearAllChangesForEntity(Entity entity) => throw new NotImplementedException();
            public void ClearLastSerializationResult() => throw new NotImplementedException();
            public void CreateEntity(Entity id, ICoherenceComponentData[] data) => throw new NotImplementedException();
            public void DestroyEntity(Entity id, IReadOnlyCollection<Entity> ackedEntities) => throw new NotImplementedException();
            public bool HasChangesForEntity(Entity entity) => throw new NotImplementedException();
            public void PushCommand(IEntityCommand command, bool useDebugStreams) => throw new NotImplementedException();
            public void PushInput(IEntityInput message, bool useDebugStreams) => throw new NotImplementedException();
            public void RemoveComponents(Entity id, uint[] componentTypes, Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity) => throw new NotImplementedException();
            public void Reset() => throw new NotImplementedException();
            public void UpdateComponents(Entity id, ICoherenceComponentData[] data) => throw new NotImplementedException();
        }
    }
}
