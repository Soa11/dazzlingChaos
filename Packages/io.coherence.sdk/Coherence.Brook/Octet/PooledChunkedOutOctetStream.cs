// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brook.Octet
{
    using Brook.Octet;
    using Common.Pooling;
    using Microsoft.IO;

    internal class PooledChunkedOutOctetStream : ChunkedOutOctetStream, IPoolable
    {
        private readonly IPool<PooledChunkedOutOctetStream> streamPool;

        public PooledChunkedOutOctetStream(IPool<PooledChunkedOutOctetStream> streamPool, RecyclableMemoryStream stream) : base(stream)
        {
            this.streamPool = streamPool;
        }

        public void Return()
        {
            Reset();
            streamPool.Return(this);
        }
    }
}
