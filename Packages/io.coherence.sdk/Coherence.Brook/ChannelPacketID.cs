// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brook
{
    using System;

    /// <summary>
    /// Represents an auto incrementing channel packet identifier. Used for fragmentation.
    /// </summary>
    public readonly struct ChannelPacketID : IEquatable<ChannelPacketID>
    {
        public const uint EndOfChannelPackets = 0;

        public readonly uint MaxValue;
        public readonly uint MaxRange;

        public uint Value { get; }

        public ChannelPacketID(uint value, uint maxValue)
        {
            if (value > maxValue)
            {
                throw new ArgumentException($"Invalid ChannelPacketID: {value}. It must be less than or equal to {maxValue}.");
            }

            Value = value;

            MaxValue = maxValue;
            MaxRange = maxValue + 1;
        }

        public ChannelPacketID Next() {
            var newValue = (Value + 1) % MaxRange;

            if (newValue == 0)
            {
                // Value 0 is reserved for the end of channel packets.
                newValue = 1;
            }

            return new(newValue, MaxValue);
        }


        /// <summary>
        /// Returns the looping distance from this id to the other id.
        /// </summary>
        public uint Distance(ChannelPacketID other)
        {
            var otherValue = other.Value;
            var thisValue = Value;

            if (otherValue < thisValue)
            {
                otherValue += MaxRange;
                otherValue--; // We wrapped around, but the index 0 is reserved, so we subtract 1 to account for that.
            }

            return otherValue - thisValue;
        }

        /// <summary>
        /// Returns true if the other id is a valid successor of this id.
        /// </summary>
        public bool IsValidSuccessor(ChannelPacketID other)
        {
            var distance = Distance(other);

            return distance > 0 && distance <= MaxValue / 2;
        }

        public bool Equals(ChannelPacketID other) => Value == other.Value;

        public override string ToString()
        {
            return $"[ChannelPacketID {Value}]";
        }

        public static implicit operator uint(ChannelPacketID id) => id.Value;
    }
}
