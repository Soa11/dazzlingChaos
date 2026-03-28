// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Entities;

    public class SerializedEntityMessage
    {
        public SerializedEntityMessage(Entity targetEntity, IOutOctetStream outOctetStream, uint bitCount)
        {
            TargetEntity = targetEntity;
            OutOctetStream = outOctetStream;
            BitCount = bitCount;
        }

        public Entity TargetEntity { get; }
        public IOutOctetStream OutOctetStream { get; private set; }
        public uint BitCount { get; }

        public void Return()
        {
            OutOctetStream.ReturnIfPoolable();
            OutOctetStream = null;
        }
    }
}
