// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using System;

    /// <summary>
    /// Custom attributes associated with a <see cref="LobbyData">Lobby</see>
    /// or a <see cref="LobbyPlayer">player occupying a lobby</see>.
    /// </summary>
    public struct CloudAttribute : IEquatable<CloudAttribute>
    {
        /// <summary>
        /// Identifier for the attribute.
        /// </summary>
        [JsonIgnore]
        public string Key => key;

        [JsonProperty("key")]
        private string key;

        [JsonProperty("val")]
        private object value;

        [JsonProperty("pub")]
        private bool? isPublic;

        [JsonProperty("idx")]
        private string index;

        [JsonProperty("aggr")]
        private string aggregate;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudAttribute"/> struct with an integer value.
        /// </summary>
        /// <param name="key">
        /// Identifier for the Attribute.
        /// </param>
        /// <param name="value"> Integer value of the Attribute. </param>
        /// <param name="isPublic"> Public attributes will be returned and visible to all Players. </param>
        public CloudAttribute(string key, long value, bool? isPublic = null)
        {
            this.key = key;
            this.value = value;
            this.isPublic = isPublic;
            this.index = null;
            this.aggregate = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudAttribute"/> struct with a string value.
        /// </summary>
        /// <param name="key"> Identifier for the Attribute. </param>
        /// <param name="value"> String value of the Attribute. </param>
        /// <param name="isPublic"> Public attributes will be returned and visible to all Players. </param>
        public CloudAttribute(string key, string value, bool? isPublic = null)
        {
            this.key = key;
            this.value = value;
            this.isPublic = isPublic;
            this.index = null;
            this.aggregate = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudAttribute"/> struct with an integer value, index and aggregator.
        /// </summary>
        /// <param name="key"> Identifier for the Attribute. </param>
        /// <param name="value"> Integer value of the Attribute. </param>
        /// <param name="index"> A predetermined value that will allow the Attribute to be used in Filter Expressions. </param>
        /// <param name="aggregate"> This applies to the lobby index. When a player joins/leaves the lobby their indexes are aggregated into the lobby indexes. </param>
        /// <param name="isPublic"> Public attributes will be returned and visible to all Players. </param>
        public CloudAttribute(string key, long value, IntAttributeIndex index, IntAggregator aggregate, bool? isPublic = null)
        {
            this.key = key;
            this.value = value;
            this.isPublic = isPublic;
            this.index = index.ToString();
            this.aggregate = aggregate == IntAggregator.None ? null : aggregate.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudAttribute"/> struct with a string value, index and aggregator.
        /// </summary>
        /// <param name="key"> Identifier for the Attribute. </param>
        /// <param name="value"> String value of the Attribute. </param>
        /// <param name="index"> A predetermined value that will allow the Attribute to be used in Filter Expressions. </param>
        /// <param name="aggregate"> This applies to the lobby index. When a player joins/leaves the lobby their indexes are aggregated into the lobby indexes. </param>
        /// <param name="isPublic"> Public attributes will be returned and visible to all Players. </param>
        public CloudAttribute(string key, string value, StringAttributeIndex index, StringAggregator aggregate, bool? isPublic = null)
        {
            this.key = key;
            this.value = value;
            this.isPublic = isPublic;
            this.index = index.ToString();
            this.aggregate = aggregate == StringAggregator.None ? null : aggregate.ToString().ToLowerInvariant();
        }

        /// <exception cref="InvalidCastException">
        /// Thrown when the value cannot be cast to a long.
        /// </exception>
        public long GetLongValue()
        {
            try
            {
                return (long)value;
            }
            catch (InvalidCastException)
            {
                LogError($"Invalid Cast: Attribute {key} is not a long value.");
                return 0;
            }
        }

        /// <exception cref="InvalidCastException">
        /// Thrown when the value cannot be cast to a string.
        /// </exception>
        public string GetStringValue()
        {
            try
            {
                return (string)value;
            }
            catch (InvalidCastException)
            {
                LogError($"Invalid Cast: Attribute {key} is not a string value.");
                return string.Empty;
            }
        }

        private void LogError(string errorMsg)
        {
#if UNITY
            UnityEngine.Debug.LogError(errorMsg);
#else
            Console.WriteLine(errorMsg);
#endif
        }

        public bool Equals(CloudAttribute other)
            => key == other.key
            && Equals(value, other.value)
            && isPublic == other.isPublic
            && index == other.index
            && aggregate == other.aggregate;

        public override bool Equals(object obj) => obj is CloudAttribute other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(key, value, isPublic, index, aggregate);
    }

    public enum StringAttributeIndex
    {
        s1,
        s2,
        s3,
        s4,
        s5
    }

    public enum IntAttributeIndex
    {
        n1,
        n2,
        n3,
        n4,
        n5
    }

    public enum IntAggregator
    {
        None,
        Sum,
        Avg,
        Min,
        Max,
        Owner
    }

    public enum StringAggregator
    {
        None,
        Owner
    }
}
