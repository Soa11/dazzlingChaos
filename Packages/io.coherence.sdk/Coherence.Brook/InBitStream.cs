// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brook
{
    using System;
    using Debugging;
    using Log;

    public class InBitStream : IInBitStream
    {
        private IOctetReader octetReader;
        private int remainingBits;
        private uint data;
        private int position;
        private int bitSize;

        private static readonly LazyLogger Logger = Log.GetLazyLogger<InBitStream>();

        public int Position => position;

        [ThreadStatic]
        private static InBitStream shared;
        public static InBitStream Shared
        {
            get
            {
                if (shared == null)
                {
                    shared = new InBitStream(null, 0);
                }
                return shared;
            }
        }

        public InBitStream(IOctetReader octetReader, int octetCount)
        {
            if (octetReader != null)
            {
                _ = Reset(octetReader, octetCount);
            }
        }

        public InBitStream Reset(IOctetReader octetReader, int octetCount)
        {
            if (octetCount > octetReader.RemainingOctetCount)
            {
                throw new ArgumentException($"Given OctetCount is greater than available data in the octet reader. " +
                    $"{octetCount} > {octetReader.RemainingOctetCount}");
            }

            this.octetReader = octetReader;
            this.bitSize = octetCount * 8;
            this.position = 0;
            this.remainingBits = 0;
            this.data = 0;

            return this;
        }

        public void GrowSize(int additionalBytes)
        {
            if (bitSize + additionalBytes * 8 - position - remainingBits > octetReader.RemainingOctetCount * 8)
            {
                throw new ArgumentException($"Cannot grow bit size by {additionalBytes * 8} bits. Not enough data in octet reader. " +
                    $"{bitSize + additionalBytes * 8 - position + remainingBits} > {octetReader.RemainingOctetCount * 8}");
            }

            bitSize += additionalBytes * 8;
        }

        public void ReadBytesUnaligned(Span<byte> buffer, int bitCount)
        {
            DbgAssert.ThatFmt(buffer.Length * 8 >= bitCount,
                "Bit count {0} is too large for buffer of size {1}B", bitCount, buffer.Length);

            const int chunkBitSize = 8;
            var restBitCount = bitCount % chunkBitSize;
            var chunkCount = bitCount / chunkBitSize;

            for (var i = 0; i < chunkCount; i++)
            {
                buffer[i] = (byte)ReadBits(chunkBitSize);
            }

            if (restBitCount > 0)
            {
                buffer[chunkCount] = (byte)(ReadBits(restBitCount) << (chunkBitSize - restBitCount));
            }
        }

        public ushort ReadUint16()
        {
            return (ushort)ReadBits(16);
        }

        public int ReadSignedBits(int count)
        {
            var sign = ReadBits(1);
            var v = (int)ReadBits(count - 1);

            if (sign != 0)
            {
                v = -v;
            }

            return v;
        }

        public int RemainingBits()
        {
            return bitSize - position;
        }

        public bool IsEof => position == bitSize;

        public short ReadInt16()
        {
            return (short)ReadSignedBits(16);
        }

        public uint ReadUint32()
        {
            return ReadBits(32);
        }

        public ulong ReadUint64()
        {
            ulong upper = ReadRawBits(32);
            var result = upper << 32;
            ulong lower = ReadRawBits(32);

            result |= lower;

            return result;
        }

        public byte ReadUint8()
        {
            return (byte)ReadBits(8);
        }

        private uint MaskFromCount(int count)
        {
            return count == 32 ? 0xffffffff : ((uint)1 << count) - 1;
        }

        private uint ReadOnce(int bitsToRead)
        {
            if (bitsToRead == 0)
            {
                return 0;
            }

            if (bitsToRead > remainingBits)
            {
                throw new EndOfStreamException(bitsToRead, remainingBits);
            }

            var mask = MaskFromCount(bitsToRead);
            var shiftPos = remainingBits - bitsToRead;

            if (position + bitsToRead > bitSize)
            {
                Logger.Warning(Warning.InBitStreamEOS, ("position", position), ("bitsToRead", bitsToRead), ("bitSize", bitSize));

                var s = $"Position:{position} bitsToRead:{bitsToRead} bitSize:{bitSize}";
                throw new EndOfStreamException(s);
            }

            position += bitsToRead;

            uint bits = 0;

            if (shiftPos < 32)
            {
                bits = (data >> shiftPos) & mask;
            }

            // logger.Info("READ mask {0:X} shift:{1} bits:{2:X} data:{3:X} {4:X}", mask, shiftPos, bits, data, (data >> shiftPos));
            remainingBits -= bitsToRead;
            return bits;
        }

        private void Fill()
        {
            var octetsToRead = 4;

            var remainingOctets = RemainingBits() / 8;
            if (octetsToRead > remainingOctets)
            {
                octetsToRead = remainingOctets;
            }

            uint newData = 0;
            for (var i = 0; i < octetsToRead; ++i)
            {
                newData <<= 8;
                var octet = octetReader.ReadOctet();
                newData |= octet;
            }

            data = newData;
            remainingBits = octetsToRead * 8;
            // logger.Info("Data is now {0:X} octetsToRead:{1} Remaining:{2}", data, octetsToRead, remainingBits);
        }

        public uint ReadBits(int count)
        {
            if (count > 32)
            {
                throw new Exception("Max 32 bits to read");
            }

            if (count > remainingBits)
            {
                var secondCount = count - remainingBits;
                var v = ReadOnce(remainingBits);
                Fill();

                v <<= secondCount;
                v |= ReadOnce(secondCount);
                return v;
            }
            else
            {
                var v = ReadOnce(count);
                return v;
            }
        }

        public uint ReadRawBits(int count)
        {
            return ReadBits(count);
        }
    }
}
