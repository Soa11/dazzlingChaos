// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brook.Octet
{
    using System;
    using Microsoft.IO;

    public class ChunkedOutOctetStream : OutOctetStream, IDisposable
    {
        public override uint Capacity => uint.MaxValue;

        private RecyclableMemoryStream recyclableStream;

        public ChunkedOutOctetStream(RecyclableMemoryStream recyclableStream) : base(recyclableStream)
        {
            this.recyclableStream = recyclableStream;
        }

        public override void Reset()
        {
            base.Reset();

            recyclableStream.Reset();
        }

        protected override void ResizeAndReset(int capacity) => Reset();

        public void Dispose()
        {
            if (recyclableStream != null)
            {
                recyclableStream.Dispose();
                recyclableStream = null;
            }
        }
    }
}
