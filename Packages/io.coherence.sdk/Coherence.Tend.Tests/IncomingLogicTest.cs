// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend.Tests
{
    using NUnit.Framework;
    using System;
    using Log;
    using Moq;
    using Coherence.Tend;

    using SeqID = System.UInt16;

    public class IncomingLogicTest
    {
        private Mock<Logger> loggerMock;
        private IncomingLogic incomingLogic;

        [SetUp]
        public void Setup()
        {
            loggerMock = new Mock<Logger>(null, null, null, null);
            loggerMock.Setup(m => m.With<It.IsAnyType>()).Returns(() => loggerMock.Object);

            incomingLogic = new IncomingLogic(loggerMock.Object);
        }

        [Test]
        public void SamePacketTwice_ShouldIgnore()
        {
            // Tests if packet with same sequenceId is received twice that it is ignored.

            // Arrange
            var sequenceId = new SequenceID(10);

            // Act
            incomingLogic.ReceivedToUs(sequenceId);
            var result = incomingLogic.ReceivedToUs(sequenceId);

            // Assert
            Assert.AreEqual(false, result);
        }

        [Test]
        public void NotSuccessor_ShouldIgnore()
        {
            // Tests if packets are not received in order that they are ignored.

            // Arrange
            var sequenceId1 = new SequenceID(10);
            var sequenceId2 = new SequenceID(9);

            // Act
            incomingLogic.ReceivedToUs(sequenceId1);
            var result = incomingLogic.ReceivedToUs(sequenceId2);

            // Assert
            Assert.AreEqual(false, result);
        }

        [Test]
        public void FirstReceive_ShouldAcceptAndCorrectMask()
        {
            // Tests if first received packet is handled correctly and correct mask is generated.

            // Arrange
            var sequenceId = new SequenceID(1);
            uint expectedMask = 0b1;

            // Act
            var result = incomingLogic.ReceivedToUs(sequenceId);

            // Assert
            Assert.AreEqual(true, result);
            Assert.AreEqual(expectedMask, incomingLogic.ReceiveMask.Bits);
        }

        [Test]
        public void Dropped_ShouldAcceptAndCorrectMask()
        {
            // Tests if some sequenceIds are dropped that packets are still accepted and correct mask is generated.

            // Arrange
            var sequence1Id = new SequenceID(1);
            var sequence3Id = new SequenceID(3);
            uint expectedMask1 = 0b1;
            uint expectedMask3 = 0b101;

            // Act - receive 1
            var result1 = incomingLogic.ReceivedToUs(sequence1Id);
            // Assert - receive 1
            Assert.AreEqual(true, result1);
            Assert.AreEqual(expectedMask1, incomingLogic.ReceiveMask.Bits);

            // Act - receive 3
            var result3 = incomingLogic.ReceivedToUs(sequence3Id);
            // Assert - receive 3
            Assert.AreEqual(true, result3);
            Assert.AreEqual(expectedMask3, incomingLogic.ReceiveMask.Bits);
        }

        [TestCase((SeqID)0)]
        [TestCase(SequenceID.MaxValue)]
        [TestCase((SeqID)12)]
        public void TooBigDistance_ShouldFail(SeqID startingSequenceId)
        {
            // Tests if distance between two incoming sequenceIds is too bit that it throws an exception.

            // Arrange
            var sequence1 = new SequenceID(startingSequenceId);
            var sequence2 = new SequenceID((SeqID)((sequence1.Value + ReceiveMask.Range + 1) % SequenceID.MaxRange));

            // Act
            incomingLogic.ReceivedToUs(sequence1);

            // Assert
            Assert.That(incomingLogic.ReceivedToUs(sequence2), Is.False);
        }

        [Test]
        public void InitialValues_AreCorrect()
        {
            Assert.AreEqual(SequenceID.Max, incomingLogic.LastReceivedToUs);
            Assert.AreEqual(0u, incomingLogic.ReceiveMask.Bits);
        }

        [TestCase((SeqID)0)]
        [TestCase(SequenceID.MaxValue)]
        [TestCase((SeqID)12)]
        public void MaximumDistance_ShouldAccept(SeqID startingSequenceId)
        {
            // Tests if incoming packets with maximum allowed distance are still accepted.

            // Arrange
            var sequence1 = new SequenceID(startingSequenceId);
            var sequence2 = new SequenceID((SeqID)((sequence1.Value + ReceiveMask.Range) % SequenceID.MaxRange));

            // Act
            incomingLogic.ReceivedToUs(sequence1);
            var result = incomingLogic.ReceivedToUs(sequence2);

            // Assert
            Assert.AreEqual(true, result);
        }

        [Test]
        public void TestDroppedReceivedRandom()
        {
            // Tests if randomly dropped packets generate correct mask.

            System.Random random = new System.Random();

            var packetCount = 1000;
            uint expectedHeader = 0;

            for (var packetIndex = 0; packetIndex < packetCount; packetIndex++)
            {
                var drop = random.Next() % 2 == 0;
                if (!drop)
                {
                    expectedHeader |= 1;

                    incomingLogic.ReceivedToUs(new SequenceID((SeqID)(packetIndex % SequenceID.MaxRange)));

                    Assert.AreEqual(incomingLogic.ReceiveMask.Bits, expectedHeader, $"wrong at {packetIndex}\ngot           : {Convert.ToString(incomingLogic.ReceiveMask.Bits, 2).PadLeft(32, '0')}\nexpected: {Convert.ToString(expectedHeader, 2).PadLeft(32, '0')}");
                }

                expectedHeader <<= 1;
            }
        }
    }
}
