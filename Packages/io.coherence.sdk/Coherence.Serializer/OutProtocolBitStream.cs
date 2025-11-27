// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using ProtocolDef;
    using Brook;
    using System.Text;
    using Entities;
    using System;
    using System.Numerics;
    using System.Threading;
    using Common;
    using Connection;
    using Log;

    public class OutProtocolBitStream : IOutProtocolBitStream
    {
        public const int ENUM_LENGTH_BITS = 6; // 64 bytes
        public const int ENUM_MAX_VALUE = (1 << ENUM_LENGTH_BITS) - 1;

        // Use 6 bits to support up to 64 characters - the previous limit.
        // Use 3 more bits to support up to 512 characters.
        // Use 4 more bits to support up to 8192 characters.
        // Use 3 more bits to support up to 65_536 characters.
        // Use 4 more bits to support up to 1_048_576 characters.
        // Fill the remaining 12 bits, to support full uint32 - but this would be madness.
        public static readonly VarIntDefinition StringLenVarIntDefinition = new(new byte[] { 6, 3, 4, 3, 4, 12 }, false);

        // Use 6 bits to support up to 64 bytes.
        // Use 3 more bits to support up to 512 bytes - the previous limit.
        // Use 4 more bits to support up to 8192 bytes.
        // Use 3 more bits to support up to 65_536 bytes.
        // Use 4 more bits to support up to 1_048_576 bytes.
        // Fill the remaining 12 bits, to support full uint32 - but this would be madness.
        public static readonly VarIntDefinition BytesLenVarIntDefinition = new(new byte[] { 6, 3, 4, 3, 4, 12 }, false);

        public IOutBitStream BitStream { get; private set; }
        private Cram.OutBitStream cramStream;
        private Logger logger;

        [ThreadStatic]
        private static OutProtocolBitStream shared;
        internal static OutProtocolBitStream Shared
        {
            get
            {
                if (shared == null)
                {
                    shared = new OutProtocolBitStream(null, null);
                }
                return shared;
            }
        }

        public OutProtocolBitStream(IOutBitStream bitStream, Logger incoming)
        {
            Reset(bitStream, incoming);
        }

        internal OutProtocolBitStream Reset(IOutBitStream bitStream, Logger incoming)
        {
            BitStream = bitStream;
            cramStream = new Cram.OutBitStream(bitStream);
            logger = incoming;
            return this;
        }

        public void WriteIntegerRange(int v, int bitCount, int offset)
        {
            BitStream.WriteBits((uint)(v - offset), bitCount);
        }

        public void WriteUIntegerRange(uint v, int bitCount, uint offset)
        {
            BitStream.WriteBits((v - offset), bitCount);
        }

        public void WriteDouble(double value)
        {
            cramStream.WriteDouble(value);
        }

        public void WriteFloat(float value, in FloatMeta meta)
        {
            cramStream.WriteFloat(value, meta);
        }

        public void WriteVector2(in Vector2 v, in FloatMeta meta)
        {
            cramStream.WriteVector2(v, meta);
        }

        public void WriteVector3(in Vector3 v, in FloatMeta meta)
        {
            cramStream.WriteVector3(v, meta);
        }

        public void WriteVector3d(in Vector3d v)
        {
            cramStream.WriteDouble(v.x);
            cramStream.WriteDouble(v.y);
            cramStream.WriteDouble(v.z);
        }

        public void WriteVector4(in Vector4 v, in FloatMeta meta)
        {
            cramStream.WriteVector4(v, meta);
        }

        public void WriteColor(in Vector4 v, in FloatMeta meta)
        {
            WriteVector4(v, meta);
        }

        public void WriteQuaternion(in Quaternion q, int bitsPerComponent)
        {
            cramStream.WriteQuaternion(q, bitsPerComponent);
        }

        public void WriteString(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                _ = SerializeTools.WriteVarInt(BitStream, 0, StringLenVarIntDefinition);
                return;
            }

            var byteCount = Encoding.UTF8.GetByteCount(s);

            _ = SerializeTools.WriteVarInt(BitStream, (uint)byteCount, StringLenVarIntDefinition);

            var totalByteCount = 0;

            Span<byte> buffer = stackalloc byte[Encoding.UTF8.GetMaxByteCount(1)];

            for (var index = 0; index < s.Length; index++)
            {
                var len = Encoding.UTF8.GetBytes(s.AsSpan(index, 1), buffer);
                BitStream.WriteBytesUnaligned(buffer, len * 8);

                totalByteCount += len;
            }

            if (totalByteCount != byteCount)
            {
                throw new Exception($"The expected number of bytes to be written is wrong. Expected {byteCount}, written: {totalByteCount}");
            }
        }

        public void WriteBytes(byte[] data)
        {
            if (data == null)
            {
                _ = SerializeTools.WriteVarInt(BitStream, 0, BytesLenVarIntDefinition);
                return;
            }

            _ = SerializeTools.WriteVarInt(BitStream, (uint)data.Length, BytesLenVarIntDefinition);
            BitStream.WriteBytesUnaligned(data, data.Length * 8);
        }

        public void WriteBits(uint value, int count)
        {
            BitStream.WriteBits(value, count);
        }

        public void WriteByte(byte value)
        {
            BitStream.WriteUint8(value);
        }

        public void WriteSByte(sbyte value)
        {
            BitStream.WriteUint8((byte)value);
        }

        public void WriteShort(short value)
        {
            BitStream.WriteUint16((ushort)value);
        }

        public void WriteUShort(ushort value)
        {
            BitStream.WriteUint16(value);
        }

        public void WriteChar(char value)
        {
            BitStream.WriteUint16(value);
        }

        public void WriteLong(long value)
        {
            BitStream.WriteUint64((ulong)value);
        }

        public void WriteULong(ulong value)
        {
            BitStream.WriteUint64(value);
        }

        public bool WriteMask(bool b)
        {
            BitStream.WriteBits(b ? 1U : 0U, 1);
            return b;
        }

        public void WriteMaskBits(uint mask, uint numBits)
        {
            BitStream.WriteBits(mask, (int)numBits);
        }

        public void WriteBool(bool b)
        {
            BitStream.WriteBits(b ? 1U : 0U, 1);
        }

        public void WriteEnum(int b)
        {
            if (b > ENUM_MAX_VALUE)
            {
                throw new Exception($"Enum too large. Max enum value: {ENUM_MAX_VALUE} bytes.");
            }

            BitStream.WriteBits((uint)b, ENUM_LENGTH_BITS);
        }

        public void WriteEntity(Entity entityID)
        {
            SerializeTools.SerializeEntity(entityID, BitStream);
        }

        public void WriteClientID(ClientID clientID)
        {
            BitStream.WriteBits((uint)clientID, ClientID.SIZE_IN_BITS);
        }
    }
}
