// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using Brook;
    using ProtocolDef;
    using System.Text;
    using System.Numerics;
    using Entities;
    using Coherence.Common;
    using Coherence.Connection;

    public struct InProtocolBitStream : IInProtocolBitStream
    {
        private readonly Cram.InBitStream cramStream;
        private readonly IInBitStream bitStream;

        public IInBitStream BitStream => bitStream;

        public InProtocolBitStream(IInBitStream bitStream)
        {
            this.bitStream = bitStream;
            cramStream = new Cram.InBitStream(bitStream);
        }

        public int ReadIntegerRange(int bitCount, int offset)
        {
            long v = offset + bitStream.ReadBits(bitCount);

            return (int)v;
        }

        public uint ReadUIntegerRange(int bitCount, uint offset)
        {
            long v = offset + bitStream.ReadBits(bitCount);

            return (uint)v;
        }

        public Quaternion ReadQuaternion(int bitsPerComponent)
        {
            return cramStream.ReadQuaternion(bitsPerComponent);
        }

        public double ReadDouble()
        {
            return cramStream.ReadDouble();
        }

        public float ReadFloat(in FloatMeta meta)
        {
            return cramStream.ReadFloat(meta);
        }

        public Vector2 ReadVector2(in FloatMeta meta)
        {
            return cramStream.ReadVector2(meta);
        }

        public Vector3 ReadVector3(in FloatMeta meta)
        {
            return cramStream.ReadVector3(meta);
        }

        public Vector3d ReadVector3d()
        {
            return new Vector3d(
                cramStream.ReadDouble(),
                cramStream.ReadDouble(),
                cramStream.ReadDouble()
            );
        }

        public Vector4 ReadVector4(in FloatMeta meta)
        {
            return cramStream.ReadVector4(meta);
        }

        public Vector4 ReadColor(in FloatMeta meta)
        {
            return ReadVector4(meta);
        }

        public string ReadString()
        {
            var len = DeserializerTools.ReadVarInt(bitStream, OutProtocolBitStream.StringLenVarIntDefinition);

            if (len == 0)
            {
                return string.Empty;
            }

            var octets = new byte[len];

            bitStream.ReadBytesUnaligned(octets, (int)len * 8);

            return Encoding.UTF8.GetString(octets);
        }

        public byte[] ReadBytes()
        {
            var len = DeserializerTools.ReadVarInt(bitStream, OutProtocolBitStream.BytesLenVarIntDefinition);

            var octets = new byte[len];
            bitStream.ReadBytesUnaligned(octets, (int)len * 8);

            return octets;
        }

        public uint ReadBits(int count)
        {
            return bitStream.ReadBits(count);
        }

        public byte ReadByte()
        {
            return bitStream.ReadUint8();
        }

        public sbyte ReadSByte()
        {
            return (sbyte)bitStream.ReadUint8();
        }

        public short ReadShort()
        {
            return (short)bitStream.ReadUint16();
        }

        public ushort ReadUShort()
        {
            return bitStream.ReadUint16();
        }

        public char ReadChar()
        {
            return (char)bitStream.ReadUint16();
        }

        public long ReadLong()
        {
            return (long)bitStream.ReadUint64();
        }

        public ulong ReadULong()
        {
            return bitStream.ReadUint64();
        }

        public bool ReadMask()
        {
            bool bitIsSet = bitStream.ReadBits(1) != 0;

            return bitIsSet;
        }

        public uint ReadMaskBits(uint numBits)
        {
            return bitStream.ReadBits((int)numBits);
        }

        public bool ReadBool()
        {
            bool bitIsSet = bitStream.ReadBits(1) != 0;

            return bitIsSet;
        }

        public int ReadEnum()
        {
            return (int)bitStream.ReadBits(OutProtocolBitStream.ENUM_LENGTH_BITS);
        }

        public Entity ReadEntity()
        {
            return DeserializerTools.DeserializeEntity(bitStream);
        }

        public ClientID ReadClientID()
        {
            return (ClientID)bitStream.ReadBits(ClientID.SIZE_IN_BITS);
        }
    }
}
