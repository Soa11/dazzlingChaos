// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer.Tests
{
    using Brook;
    using Brook.Octet;
    using NUnit.Framework;
    using Coherence.Tests;

    public class ProtocolBitStreamTests : CoherenceTest
    {
        [Test]
        [Description("Verifies that writing and reading an empty string works.")]
        public void WriteString_Empty()
        {
            var octetStream = new OutOctetStream(1000);
            var bitStream = new OutBitStream(octetStream);
            var protoStream = new OutProtocolBitStream(bitStream, logger);

            var testString = "";

            // Act
            protoStream.WriteString(testString);
            bitStream.Flush();

            // Assert
            var written = octetStream.Close().ToArray();
            var inOctetStream = new InOctetStream(written);
            var inBitStream = new InBitStream(inOctetStream, written.Length * 8);
            var inProtoStream = new InProtocolBitStream(inBitStream);

            var got = inProtoStream.ReadString();
            Assert.That(got, Is.EqualTo(testString));
        }

        [Test]
        [Description("Verifies that writing and reading a null string works.")]
        public void WriteString_Null()
        {
            var octetStream = new OutOctetStream(1000);
            var bitStream = new OutBitStream(octetStream);
            var protoStream = new OutProtocolBitStream(bitStream, logger);

            string testString = null;

            // Act
            protoStream.WriteString(testString);
            bitStream.Flush();

            // Assert
            var written = octetStream.Close().ToArray();
            var inOctetStream = new InOctetStream(written);
            var inBitStream = new InBitStream(inOctetStream, written.Length * 8);
            var inProtoStream = new InProtocolBitStream(inBitStream);

            var got = inProtoStream.ReadString();
            Assert.That(got, Is.EqualTo(string.Empty));
        }

        [Test]
        [Description("Verifies that writing and reading a short string works.")]
        public void WriteString_Short()
        {
            var octetStream = new OutOctetStream(1000);
            var bitStream = new OutBitStream(octetStream);
            var protoStream = new OutProtocolBitStream(bitStream, logger);

            var testString = "123123abcšđĐĆćŠš123123ߘߘ";

            // Act
            protoStream.WriteString(testString);
            bitStream.Flush();

            // Assert
            var written = octetStream.Close().ToArray();
            var inOctetStream = new InOctetStream(written);
            var inBitStream = new InBitStream(inOctetStream, written.Length * 8);
            var inProtoStream = new InProtocolBitStream(inBitStream);

            var got = inProtoStream.ReadString();
            Assert.That(got, Is.EqualTo(testString));
        }

        [Test]
        [Description("Verifies that writing and reading a very long string works.")]
        public void WriteString_Long()
        {
            var octetStream = new OutOctetStream(20000);
            var bitStream = new OutBitStream(octetStream);
            var protoStream = new OutProtocolBitStream(bitStream, logger);

            var testString = new string('a', 10000);

            // Act
            protoStream.WriteString(testString);
            bitStream.Flush();

            // Assert
            var written = octetStream.Close().ToArray();
            var inOctetStream = new InOctetStream(written);
            var inBitStream = new InBitStream(inOctetStream, written.Length * 8);
            var inProtoStream = new InProtocolBitStream(inBitStream);

            var got = inProtoStream.ReadString();
            Assert.That(got, Is.EqualTo(testString));
        }
    }
}
