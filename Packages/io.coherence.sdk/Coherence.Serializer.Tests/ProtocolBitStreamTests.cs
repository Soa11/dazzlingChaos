// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer.Tests
{
    using System.Linq;
    using Brook;
    using Brook.Octet;
    using NUnit.Framework;
    using Coherence.Tests;

    public class ProtocolBitStreamTests : CoherenceTest
    {
        private static readonly object[] StringTestCases =
        {
            new object[] { "Empty string", "" },
            new object[] { "Null string", null },
            new object[] { "Short string with unicode", "123123abcšđĐĆćŠš123123ߘߘ" },
            new object[] { "Very long string", new string('a', 10000) },
            new object[] { "Emojis", "😍😂🤣😒💕😁🙌👍👌❤️😊😘" },
            new object[] { "Long emojis exceeding stack alloc threshold", string.Join(string.Empty, Enumerable.Repeat("😍😂", OutProtocolBitStream.STACKALLOC_THRESHOLD)) },
            new object[] { "Unicode combining characters", "u̸̡̧̥͈̼̼͔̟͈̟͙͉̟̤̮͐̈͋n̵̹̠̘̜̯̦̩̹̱̖̾͊̏̊̆i̴̻̙̖͈͙̱̤̬̫͇̲̎̾͌͐̈̓͒̅͊̈̀͛͜͝c̴̯̬̞̅̈́̋͗̑̏̒͛̃͘ö̵̟͇̭͍̠͉́́̏̑͋̆̽͋̋̚d̴̢̧̙̱͔̟̈́͆͂̀͠è̴̡̬̻̣̘͔̳̠̞̝̀̑́͛͜" }
        };

        [Test]
        [TestCaseSource(nameof(StringTestCases))]
        [Description("Verifies that writing and reading strings works correctly.")]
        public void WriteString_TestCases(string description, string testString)
        {
            var octetStream = new OutOctetStream(20000);
            var bitStream = new OutBitStream(octetStream);
            var protoStream = new OutProtocolBitStream(bitStream, logger);

            // Act
            protoStream.WriteString(testString);
            bitStream.Flush();

            // Assert
            var written = octetStream.Close().ToArray();
            var inOctetStream = new InOctetStream(written);
            var inBitStream = new InBitStream(inOctetStream, written.Length);
            var inProtoStream = new InProtocolBitStream(inBitStream);

            var got = inProtoStream.ReadString();
            var expected = testString ?? string.Empty;
            Assert.That(got, Is.EqualTo(expected));
        }
    }
}
