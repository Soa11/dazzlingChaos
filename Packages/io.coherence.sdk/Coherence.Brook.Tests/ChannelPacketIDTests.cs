// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brook.Tests
{
    using System;
    using Coherence.Brook;
    using Coherence.Tests;
    using NUnit.Framework;

    public class ChannelPacketIDTests : CoherenceTest
    {
        [Test]
        [Description("Validates that creating a ChannelPacketID with valid input parameters correctly initializes the instance with the expected Value and MaxValue properties.")]
        public void NewChannelPacketID_ValidInput_CreatesCorrectInstance()
        {
            // Arrange
            uint value = 5;
            uint maxValue = 10;

            // Act
            var result = new ChannelPacketID(value, maxValue);

            // Assert
            Assert.That(result.Value, Is.EqualTo(value));
            Assert.That(result.MaxValue, Is.EqualTo(maxValue));
        }

        [Test]
        [Description("Validates that creating a ChannelPacketID when the value equals the maximum value correctly initializes the instance without throwing an exception.")]
        public void NewChannelPacketID_ValueEqualsMaxValue_CreatesCorrectInstance()
        {
            // Arrange
            uint value = 10;
            uint maxValue = 10;

            // Act
            var result = new ChannelPacketID(value, maxValue);

            // Assert
            Assert.That(result.Value, Is.EqualTo(value));
            Assert.That(result.MaxValue, Is.EqualTo(maxValue));
        }

        [Test]
        [Description("Validates that creating a ChannelPacketID with a value that exceeds the maximum value throws an ArgumentException with appropriate error details.")]
        public void NewChannelPacketID_ValueExceedsMaxValue_ThrowsArgumentException()
        {
            // Arrange
            uint value = 15;
            uint maxValue = 10;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new ChannelPacketID(value, maxValue));
            Assert.That(exception.Message, Does.Contain("15"));
            Assert.That(exception.Message, Does.Contain("10"));
        }

        [Test]
        [Description("Validates that creating a ChannelPacketID with zero values for both value and maxValue correctly initializes the instance.")]
        public void NewChannelPacketID_ZeroValues_CreatesCorrectInstance()
        {
            // Arrange
            uint value = 0;
            uint maxValue = 0;

            // Act
            var result = new ChannelPacketID(value, maxValue);

            // Assert
            Assert.That(result.Value, Is.EqualTo(value));
            Assert.That(result.MaxValue, Is.EqualTo(maxValue));
        }

        [Test]
        [Description("Validates that the Value property of a ChannelPacketID returns the correct value that was used during initialization.")]
        public void ChannelPacketID_Value_ReturnsCorrectValue()
        {
            // Arrange
            uint expectedValue = 7;
            var channelID = new ChannelPacketID(expectedValue, 20);

            // Act
            uint result = channelID.Value;

            // Assert
            Assert.That(result, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("Validates that the Next() method returns a ChannelPacketID with the next sequential value when not at the maximum boundary.")]
        public void ChannelPacketID_Next_ReturnsNextSequentialID()
        {
            // Arrange
            var channelID = new ChannelPacketID(5, 10);

            // Act
            var next = channelID.Next();

            // Assert
            Assert.That(next.Value, Is.EqualTo(6u));
        }

        [Test]
        [Description("Validates that the Next() method wraps around to 1 when at maximum value, skipping 0 which is reserved for end of channel packets.")]
        public void ChannelPacketID_Next_WrapAround_SkipsZero()
        {
            // Arrange
            var channelID = new ChannelPacketID(10, 10);

            // Act
            var next = channelID.Next();

            // Assert
            Assert.That(next.Value, Is.EqualTo(1u)); // Should wrap to 1, not 0 (reserved for end of channel packets)
        }

        [Test]
        [Description("Validates that the Equals method returns true when comparing two ChannelPacketID instances with the same value, regardless of their maxValue.")]
        public void ChannelPacketID_Equals_SameValue_ReturnsTrue()
        {
            // Arrange
            var id1 = new ChannelPacketID(5, 10);
            var id2 = new ChannelPacketID(5, 20); // Different maxValue but same value

            // Act
            bool result = id1.Equals(id2);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        [Description("Validates that the Equals method returns false when comparing two ChannelPacketID instances with different values.")]
        public void ChannelPacketID_Equals_DifferentValue_ReturnsFalse()
        {
            // Arrange
            var id1 = new ChannelPacketID(5, 10);
            var id2 = new ChannelPacketID(7, 10);

            // Act
            bool result = id1.Equals(id2);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        [Description("Validates that the Equals method returns true when comparing two ChannelPacketID instances that both have zero values.")]
        public void ChannelPacketID_Equals_ZeroValue_ReturnsTrue()
        {
            // Arrange
            var channelID = new ChannelPacketID(0, 10);
            var zeroValueID = new ChannelPacketID(0, 5);

            // Act
            bool result = channelID.Equals(zeroValueID);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        [Description("Validates that the implicit conversion operator correctly converts a ChannelPacketID to its underlying uint value.")]
        public void ChannelPacketID_ImplicitConversion_ReturnsValue()
        {
            // Arrange
            var channelID = new ChannelPacketID(42, 100);

            // Act
            uint result = channelID;

            // Assert
            Assert.That(result, Is.EqualTo(42u));
        }

        [Test]
        [Description("Validates that the ToString method returns the expected formatted string representation of the ChannelPacketID.")]
        public void ChannelPacketID_ToString_ReturnsCorrectFormat()
        {
            // Arrange
            var channelID = new ChannelPacketID(42, 100);

            // Act
            string result = channelID.ToString();

            // Assert
            Assert.That(result, Is.EqualTo("[ChannelPacketID 42]"));
        }

        [Test]
        [Description("Validates that the ToString method correctly formats a ChannelPacketID with zero value as '[ChannelPacketID 0]'.")]
        public void ChannelPacketID_ToString_ZeroValue_ReturnsZero()
        {
            // Arrange
            var channelID = new ChannelPacketID(0, 10);

            // Act
            string result = channelID.ToString();

            // Assert
            Assert.That(result, Is.EqualTo("[ChannelPacketID 0]"));
        }

        [TestCase(1, 1, 0)]
        [TestCase(2, 2, 0)]
        [TestCase(10, 10, 0)]
        [TestCase(2, 3, 1)]
        [TestCase(2, 10, 8)]
        [TestCase(9, 10, 1)]
        [TestCase(10, 1, 1)]
        [TestCase(10, 2, 2)]
        [TestCase(5, 4, 9)]
        [Description("Validates that Distance method correctly calculates, even when it loops around (current > next).")]
        public void ChannelPacketID_Distance(int current, int next, int expected)
        {
            // Arrange
            var currentID = new ChannelPacketID((uint)current, 10);
            var nextID = new ChannelPacketID((uint)next, 10);

            // Act
            var distance = currentID.Distance(nextID);

            // Assert
            Assert.That(distance, Is.EqualTo((uint)expected));
        }

        [TestCase(1, 1, false)]
        [TestCase(10, 10, false)]
        [TestCase(1, 2, true)]
        [TestCase(1, 5, true)]
        [TestCase(1, 6, true)]
        [TestCase(1, 7, false)]
        [TestCase(5, 10, true)]
        [TestCase(5, 1, false)]
        [TestCase(6, 1, true)]
        [TestCase(10, 1, true)]
        [TestCase(10, 5, true)]
        [TestCase(10, 6, false)]
        [Description("Validates that IsValidSuccessor method correctly identifies valid successors based on the distance and max range.")]
        public void ChannelPacketID_IsValidSuccessor(int current, int next, bool expected)
        {
            // Arrange
            var currentID = new ChannelPacketID((uint)current, 10);
            var nextID = new ChannelPacketID((uint)next, 10);

            // Act
            var isValidSuccessor = currentID.IsValidSuccessor(nextID);

            // Assert
            Assert.That(isValidSuccessor, Is.EqualTo(expected));
        }
    }
}
