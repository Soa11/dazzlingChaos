// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brook.Tests
{
    using Coherence.Brook.Octet;
    using Coherence.Tests;
    using Microsoft.IO;
    using NUnit.Framework;

    public class ChunkedOutOctetStreamTests : CoherenceTest
    {
        private const int blockSize = 1024;

        private RecyclableMemoryStreamManager recyclableStreamManager;

        private uint blockCreatedEventCount = 0;
        private uint largeBufferCreatedCount = 0;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            recyclableStreamManager = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options()
            {
                BlockSize = blockSize,
                LargeBufferMultiple = 1024 * 4, // 4 KB
                MaximumBufferSize = 256 * 1024, // 256 KB
                MaximumSmallPoolFreeBytes = 1 * 1024 * 1024, // 1 MB
                MaximumLargePoolFreeBytes = 1 * 1024 * 1024, // 1 MB
                UseExponentialLargeBuffer = true,
                MaximumStreamCapacity = 0,
            });

            recyclableStreamManager.BlockCreated += (sender, args) =>
            {
                blockCreatedEventCount++;
            };

            recyclableStreamManager.LargeBufferCreated += (sender, args) =>
            {
                largeBufferCreatedCount++;
            };

            blockCreatedEventCount = 0;
            largeBufferCreatedCount = 0;
        }

        private ChunkedOutOctetStream NewStream()
        {
            return new ChunkedOutOctetStream(recyclableStreamManager.GetStream());
        }

        private void ExpectBlockCreatedEventCount(uint expectedCount)
        {
            Assert.That(blockCreatedEventCount, Is.EqualTo(expectedCount));
            blockCreatedEventCount = 0;
        }

        private void ExpectLargeBufferCreatedCount(uint expectedCount)
        {
            Assert.That(largeBufferCreatedCount, Is.EqualTo(expectedCount));
            largeBufferCreatedCount = 0;
        }

        [Test]
        [Description("Verifies that writing small amount of data doesn't allocate excessive blocks")]
        public void Writing_Small([Values(true, false)] bool useDispose)
        {
            // Arrange - new stream
            var stream = NewStream();

            // Act - serialize 1 block
            stream.WriteOctets(new byte[blockSize]);

            // Assert - expect 1 block allocated
            ExpectBlockCreatedEventCount(1);
            ExpectLargeBufferCreatedCount(0);

            // Arrange - dispose/reset the old stream and create a new one
            if (useDispose)
            {
                stream.Dispose();
            }
            else
            {
                stream.Reset();
            }

            var newStream = NewStream();

            // Act - write again to the new stream
            newStream.WriteOctets(new byte[blockSize]);

            // Assert - expect the block to be pooled and reused
            ExpectBlockCreatedEventCount(0);
            ExpectLargeBufferCreatedCount(0);
        }

        [Test]
        [Description("Verifies that writing large amount of data allocates the correct number of blocks, " +
            "and that those blocks are pooled and reused later.")]
        public void Writing_Large([Values(true, false)] bool useDispose)
        {
            // Arrange - new stream
            var stream = NewStream();

            // Act - serialize 100 blocks
            stream.WriteOctets(new byte[blockSize * 100]);

            // Assert - expect 100 blocks allocated
            ExpectBlockCreatedEventCount(100);
            ExpectLargeBufferCreatedCount(0);

            // Arrange - dispose/reset the old stream and create a new one
            if (useDispose)
            {
                stream.Dispose();
            }
            else
            {
                stream.Reset();
            }

            var newStream = NewStream();

            // Act - write again to the new stream
            newStream.WriteOctets(new byte[blockSize * 100]);

            // Assert - expect 100 blocks to be pooled and reused
            ExpectBlockCreatedEventCount(0);
            ExpectLargeBufferCreatedCount(0);
        }

        [Test]
        [Description("Verifies that getting the buffer of the stream that contains a single block" +
            "doesn't allocate a large buffer.")]
        public void GetBuffer_Small()
        {
            // Arrange - new stream
            var stream = NewStream();

            // Act - serialize 1 block
            stream.WriteOctets(new byte[blockSize]);

            // Assert - expect 1 block allocated
            ExpectBlockCreatedEventCount(1);
            ExpectLargeBufferCreatedCount(0);

            // Act - get the buffer
            _ = stream.Close();

            // Assert - expect the Close() action to not allocate a large buffer
            ExpectBlockCreatedEventCount(0);
            ExpectLargeBufferCreatedCount(0);
        }

        [Test]
        [Description("Verifies that getting the buffer of the stream allocates a single large buffer, " +
            "and that the buffer is pooled and reused later.")]
        public void GetBuffer_Large([Values(true, false)] bool useDispose)
        {
            // Arrange - new stream
            var stream = NewStream();

            // Act - serialize 100 blocks
            stream.WriteOctets(new byte[blockSize * 100]);

            // Assert - expect 100 blocks allocated
            ExpectBlockCreatedEventCount(100);
            ExpectLargeBufferCreatedCount(0);

            // Act - get the buffer
            _ = stream.Close();

            // Assert - expect the Close() action to allocate a large buffer
            ExpectBlockCreatedEventCount(0);
            ExpectLargeBufferCreatedCount(1);

            // Arrange - dispose/reset the old stream and create a new one
            if (useDispose)
            {
                stream.Dispose();
            }
            else
            {
                stream.Reset();
            }

            var newStream = NewStream();

            // Act - write again to the new stream
            newStream.WriteOctets(new byte[blockSize * 100]);

            // Assert - expect 100 blocks to be pooled and reused
            ExpectBlockCreatedEventCount(0);
            ExpectLargeBufferCreatedCount(0);

            // Act - get the buffer
            _ = newStream.Close();

            // Assert - expect the Close() to reuse the large buffer
            ExpectBlockCreatedEventCount(0);
            ExpectLargeBufferCreatedCount(0);
        }
    }
}
