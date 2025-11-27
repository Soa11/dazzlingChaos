namespace Coherence.Tend.Tests
{
    using Coherence.Brook.Octet;
    using Coherence.Tend;
    using NUnit.Framework;

    public partial class TendTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void SerializeDeserializeHeader_ShouldRemainEqual(bool isReliable)
        {
            // Arrange
            var expectedHeader = new TendHeader
            {
                isReliable = isReliable,
                packetID = new SequenceID(10),
                receivedID = new SequenceID(15),
                receiveMask = new ReceiveMask(20)
            };
            var outStream = new OutOctetStream(2048);

            // Act
            TendHeader.SerializeHeader(outStream, expectedHeader);
            var header = TendHeader.DeserializeHeader(new InOctetStream(outStream.Close().ToArray()));

            // Assert
            if (!isReliable)
            {
                Assert.AreEqual(isReliable, header.isReliable, "Headear should be correct.");
            }
            else
            {
                Assert.AreEqual(expectedHeader, header, "Header should be correct.");
            }
        }
    }
}
