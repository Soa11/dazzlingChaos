// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend.Tests
{
    using NUnit.Framework;
    using Log;
    using Moq;
    using System.Linq;
    using System.Collections.Generic;
    using System;

    using SeqID = System.UInt16;

    public class OutgoingLogicTest
    {
        private Mock<Logger> loggerMock;
        private OutgoingLogic outgoingLogic;

        [SetUp]
        public void Setup()
        {
            loggerMock = new Mock<Logger>(null, null, null, null);
            _ = loggerMock.Setup(m => m.With<It.IsAnyType>()).Returns(() => loggerMock.Object);

            outgoingLogic = new OutgoingLogic(loggerMock.Object);
        }

        /// <summary>
        /// Asserts that queued DeliveryInfos in outgoingLogic are correct.
        /// Checks their <see cref="DeliveryInfo.PacketSequenceId"/> starting with <paramref name="startingSequenceId"/>,
        /// and checks their <see cref="DeliveryInfo.WasDelivered"/> with <paramref name="delivered"/> array.
        /// </summary>
        private void AssertInfoQueue(SequenceID startingSequenceId, params bool[] delivered)
        {
            Assert.AreEqual(outgoingLogic.Count, delivered.Length);

            var sequenceId = startingSequenceId;
            for (int i = 0; i < delivered.Length; i++)
            {
                var info = outgoingLogic.Dequeue();
                Assert.AreEqual(sequenceId, info.PacketSequenceId);
                Assert.AreEqual(delivered[i], info.WasDelivered);

                sequenceId = sequenceId.Next();
            }
        }

        [TestCase(0, 1, true)]
        [TestCase(0, 1, false)]
        [TestCase(10, 10 + ReceiveMask.Range, true)]
        [TestCase(10, 10 + ReceiveMask.Range, false)]
        public void ReceivedByRemote_IsSuccessor_CreatesInfo(byte sequence1, byte sequence2, bool delivered)
        {
            // Tests generated infos are correct when received sequenceIds are valid successors.

            // Arrange
            var sequenceId1 = new SequenceID(sequence1);
            var sequenceId2 = new SequenceID(sequence2);
            var mask = delivered ? new ReceiveMask(~(uint)0) : new ReceiveMask(0);
            var distance1 = sequence1 + 1;
            var distance2 = sequenceId1.Distance(sequenceId2);

            // Act - sequence1
            _ = outgoingLogic.ReceivedByRemote(sequenceId1, mask);

            // Assert - sequence1
            AssertInfoQueue(new SequenceID(0), Enumerable.Repeat(delivered, distance1).ToArray());
            Assert.AreEqual(sequenceId1, outgoingLogic.LastReceivedByRemoteSequenceID);

            // Act - sequence1
            _ = outgoingLogic.ReceivedByRemote(sequenceId2, mask);

            // Assert - sequence1
            AssertInfoQueue(sequenceId1.Next(), Enumerable.Repeat(delivered, distance2).ToArray());
            Assert.AreEqual(sequenceId2, outgoingLogic.LastReceivedByRemoteSequenceID);
        }

        [Test]
        public void ReceivedByRemote_NotSuccessor_ThrowsException()
        {
            // Tests if received SequenceIds are in incorrect order that an exception is thrown.

            // Arrange
            var sequenceId1 = new SequenceID(10);
            var sequenceId2 = new SequenceID(9);

            // Act
            _ = outgoingLogic.ReceivedByRemote(sequenceId1, new ReceiveMask());

            // Assert
            _ = Assert.Throws<UnorderedPacketException>(() => outgoingLogic.ReceivedByRemote(sequenceId2, new ReceiveMask()));
        }

        [Test]
        public void ReceivedByRemote_SameSequenceId_DoesNothing()
        {
            // Tests if same SequenceID is received twice that nothing happens.

            // Arrange
            var sequenceId = new SequenceID(10);

            // Act
            _ = outgoingLogic.ReceivedByRemote(sequenceId, new ReceiveMask());

            // empty info queue
            while (outgoingLogic.Count > 0)
            {
                _ = outgoingLogic.Dequeue();
            }

            var result = outgoingLogic.ReceivedByRemote(sequenceId, new ReceiveMask());

            // Assert
            Assert.AreEqual(false, result);
            Assert.AreEqual(0, outgoingLogic.Count);
            Assert.AreEqual(sequenceId, outgoingLogic.LastReceivedByRemoteSequenceID);
        }

        [Test]
        public void ReceivedByRemote_Random_CreatesInfo()
        {
            // Tests if randomly generated mask is correctly transformed into DeliveryInfos.

            // Arrange
            System.Random random = new System.Random();
            uint mask = 0;
            var delivered = new List<bool>();
            for (int i = 0; i < 32; i++)
            {
                bool d = random.Next() % 2 == 0;
                mask |= (uint)(d ? 1 : 0) << 31 - i;
                delivered.Add(d);
            }

            // Act
            _ = outgoingLogic.ReceivedByRemote(new SequenceID(31), new ReceiveMask(mask));

            // Assert
            AssertInfoQueue(new SequenceID(0), delivered.ToArray());
        }

        [Test]
        public void InitialValues()
        {
            Assert.AreEqual(0, outgoingLogic.Count);
            Assert.AreEqual(new SequenceID(0), outgoingLogic.OutgoingSequenceID);
            Assert.AreEqual(SequenceID.Max, outgoingLogic.LastReceivedByRemoteSequenceID);
        }

        [TestCase((SeqID)32)]
        [TestCase((SeqID)42)]
        [TestCase((SeqID)(SequenceID.MaxValue - 1))]
        public void IncreaseOutgoingSequenceId_Cant_ShouldThrow(SeqID sequenceId)
        {
            // Tests if trying to increase OutgoingSequenceID throws an exception if it can't be increased.

            // Arrange
            outgoingLogic.OutgoingSequenceID = new SequenceID(sequenceId);

            // Act - Assert
            Assert.AreEqual(false, outgoingLogic.CanIncrementOutgoingSequence);
            _ = Assert.Throws<Exception>(() => outgoingLogic.IncreaseOutgoingSequenceID());
        }

        [TestCase(SequenceID.MaxValue)]
        [TestCase((SeqID)0)]
        [TestCase((SeqID)30)]
        public void IncreaseOutgoingSequenceId_Can_ShouldIncrement(SeqID sequenceId)
        {
            // Tests if OutgoingSequenceID is correctly increased when it can be increased.

            // Arrange
            outgoingLogic.OutgoingSequenceID = new SequenceID(sequenceId);

            // Assert
            Assert.AreEqual(true, outgoingLogic.CanIncrementOutgoingSequence);

            // Act
            _ = outgoingLogic.IncreaseOutgoingSequenceID();

            // Assert
            Assert.AreEqual(new SequenceID(sequenceId).Next(), outgoingLogic.OutgoingSequenceID);
        }
    }
}
