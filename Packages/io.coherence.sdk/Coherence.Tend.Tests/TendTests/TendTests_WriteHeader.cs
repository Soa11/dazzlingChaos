namespace Coherence.Tend.Tests
{
    using System;
    using Brook.Octet;
    using Moq;
    using NUnit.Framework;

    public partial class TendTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void WriteHeader_ShouldWriteAndReturnHeader(bool isReliable)
        {
            // Arrange
            var outStream = new OutOctetStream(2048);
            var expectedHeader = new TendHeader
            {
                isReliable = isReliable,
                packetID = new SequenceID(10),
                receivedID = new SequenceID(15),
                receiveMask = new ReceiveMask(20),
            };
            var inStream = SerializeHeader(expectedHeader);

            _ = outgoingLogicMock.SetupGet(o => o.CanIncrementOutgoingSequence).Returns(true);
            _ = outgoingLogicMock.SetupGet(o => o.OutgoingSequenceID).Returns(expectedHeader.packetID);
            _ = incomingLogicMock.SetupGet(o => o.LastReceivedToUs).Returns(expectedHeader.receivedID);
            _ = incomingLogicMock.SetupGet(o => o.ReceiveMask).Returns(expectedHeader.receiveMask);

            // Act
            var header = tend.WriteHeader(outStream, isReliable);

            // Assert
            if (!isReliable)
            {
                Assert.AreEqual(isReliable, header.isReliable, "Returned header should be correct");
            }
            else
            {
                Assert.AreEqual(expectedHeader, header, "Returned header should be correct");
            }

            Assert.AreEqual(inStream.ReadOctets(inStream.RemainingOctetCount).ToArray(), outStream.Close(),
                "OutStream should be correct");
        }

        [Test]
        public void WriteHeader_WhenCanNotIncrement_ShouldThrow()
        {
            // Arrange
            _ = outgoingLogicMock.SetupGet(o => o.CanIncrementOutgoingSequence).Returns(false);

            // Act - Assert
            _ = Assert.Throws<Exception>(() => tend.WriteHeader(null, true));
        }

        [Test]
        public void WriteHeader_WhenReliable_ShouldNotIncreaseSequenceId()
        {
            // Arrange
            var outStream = new OutOctetStream(2048);

            _ = outgoingLogicMock.SetupGet(o => o.CanIncrementOutgoingSequence).Returns(true);

            // Act
            _ = tend.WriteHeader(outStream, true);

            // Assert
            outgoingLogicMock.Verify(o => o.IncreaseOutgoingSequenceID(), Times.Never,
                "Should not call IncreaseOutgoingSequenceId()");
        }

        [Test]
        public void WriteHeader_WhenNotReliable_ShouldNotIncreaseSequenceId()
        {
            // Arrange
            var outStream = new OutOctetStream(2048);

            _ = outgoingLogicMock.SetupGet(o => o.CanIncrementOutgoingSequence).Returns(true);

            // Act
            _ = tend.WriteHeader(outStream, false);

            // Assert
            outgoingLogicMock.Verify(o => o.IncreaseOutgoingSequenceID(), Times.Never,
                "Should not call IncreaseOutgoingSequenceId()");
        }

        [Test]
        public void OnPacketSent_ShouldIncreaseSequenceID_IfItMatches()
        {
            // Arrange
            var packetSeqID = new SequenceID(0);

            _ = outgoingLogicMock.SetupGet(o => o.CanIncrementOutgoingSequence).Returns(true);
            _ = outgoingLogicMock.SetupGet(o => o.OutgoingSequenceID).Returns(packetSeqID);

            // Act
            tend.OnPacketSent(packetSeqID, true);

            // Assert
            outgoingLogicMock.Verify(o => o.IncreaseOutgoingSequenceID(), Times.Once,
                "Should call IncreaseOutgoingSequenceId() once");
        }

        [Test]
        public void OnPacketSent_ShouldNotIncreaseSequenceID_IfItDoesntMatch()
        {
            // Arrange
            var packetSeqID = new SequenceID(0);
            var outgoingSeqID = new SequenceID(1);

            _ = outgoingLogicMock.SetupGet(o => o.CanIncrementOutgoingSequence).Returns(true);
            _ = outgoingLogicMock.SetupGet(o => o.OutgoingSequenceID).Returns(outgoingSeqID);

            // Act
            tend.OnPacketSent(packetSeqID, true); // this is like resending a reliable packet.

            // Assert
            outgoingLogicMock.Verify(o => o.IncreaseOutgoingSequenceID(), Times.Never,
                "Should not call IncreaseOutgoingSequenceId()");
        }

        [Test]
        public void WriteHeader_WhenIncreaseDoesNotThrow_ShouldNotRevertSequenceId()
        {
            // Arrange
            var outStream = new OutOctetStream(2048);
            var sequenceId = new SequenceID(10);

            _ = outgoingLogicMock.SetupGet(o => o.CanIncrementOutgoingSequence).Returns(true);
            _ = outgoingLogicMock.SetupGet(o => o.OutgoingSequenceID).Returns(sequenceId);

            // Act
            _ = tend.WriteHeader(outStream, true);

            // Assert
            outgoingLogicMock.VerifySet(o => o.OutgoingSequenceID = It.IsAny<SequenceID>(), Times.Never,
                "Should not revert OutgoingSequenceID");
        }
    }
}
