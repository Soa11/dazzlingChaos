// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tend.Tests
{
    using Brook;
    using NUnit.Framework;
    using System;

    public static class Sequences
    {
        [Test]
        public static void Setting()
        {
            SequenceID s = new SequenceID(0);
            Assert.AreEqual(0, s.Value);
        }

        [Test]
        public static void NextWrap()
        {
            SequenceID current = SequenceID.Max;
            SequenceID next = current.Next();

            Assert.AreEqual(SequenceID.MaxValue, current.Value);
            Assert.AreEqual(0, next.Value);
        }

        [Test]
        public static void NormalNext()
        {
            SequenceID current = new SequenceID(12);
            SequenceID next = current.Next();

            Assert.AreEqual(12, current.Value);
            Assert.AreEqual(13, next.Value);
        }

        [Test]
        public static void DistanceWrap()
        {
            SequenceID current = SequenceID.Max;
            SequenceID next = new SequenceID(0);

            Assert.AreEqual(1, current.Distance(next));
        }

        [Test]
        public static void DistanceNormal()
        {
            SequenceID current = new SequenceID(0);
            SequenceID next = new SequenceID(10);

            Assert.AreEqual(10, current.Distance(next));
        }

        [Test]
        public static void DistancePassed()
        {
            SequenceID current = new SequenceID(10);
            SequenceID next = new SequenceID(9);

            Assert.AreEqual(SequenceID.MaxValue, current.Distance(next));
        }

        [Test]
        public static void DistancePassedAgain()
        {
            SequenceID current = new SequenceID(10);
            SequenceID next = SequenceID.Max;

            Assert.AreEqual(SequenceID.MaxValue - 10, current.Distance(next));
        }

        [Test]
        public static void DistanceSame()
        {
            SequenceID current = new SequenceID(10);
            SequenceID next = new SequenceID(10);

            Assert.AreEqual(0, current.Distance(next));
        }
    }
}
