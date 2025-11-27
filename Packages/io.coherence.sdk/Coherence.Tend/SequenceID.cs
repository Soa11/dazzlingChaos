// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend
{
    using System;
    using SeqID = System.UInt16;

    /// <summary>
    /// Represents an auto incrementing sequence identifier. Usually a value between 0 and 2047 (11 bits). After 2047 it wraps around to 0.
    /// </summary>
    public struct SequenceID : IEquatable<SequenceID>
    {
        public const SeqID MaxRange = 1 << TendHeader.BitsForSequenceID;
        public const SeqID MaxValue = MaxRange - 1;

        public static SequenceID Max = new(MaxValue);

        /// <summary>
        /// Constructing a SequenceID
        /// </summary>
        public SequenceID(SeqID id)
        {
            Value = id;
        }

        public SeqID Value { get; }

        /// <summary>
        /// Returns the next SequenceID. Note that the value wraps around MaxRange.
        /// </summary>
        public SequenceID Next()
        {
            var nextValue = (SeqID)((Value + 1) % MaxRange);

            return new SequenceID(nextValue);
        }

        /// <summary>
        /// Returns the closest distance between the otherId and this SequenceID.
        /// </summary>
        public int Distance(SequenceID otherId)
        {
            int nextValue = otherId.Value;
            int idValue = Value;

            if (nextValue < idValue)
            {
                nextValue += MaxRange;
            }

            var diff = nextValue - idValue;

            return diff;
        }

        /// <summary>
        /// Checks if the nextId comes after this SequenceID.
        /// </summary>
        public bool IsValidSuccessor(SequenceID nextId)
        {
            var distance = Distance(nextId);

            return distance != 0 && distance <= ReceiveMask.Range;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format($"[SequenceID {Value}]");
        }

        public bool Equals(SequenceID other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SequenceID other && Equals(other);
        }
    }
}
