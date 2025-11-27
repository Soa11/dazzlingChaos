// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Utils.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;

    /// <summary>
    /// Edit mode unit tests for <see cref="KeyedValues{TKey, TValue}"/>.
    /// </summary>
    public class KeyedValues_Tests
    {
        private KeyedValues<int, string> keyedValues;

        [SetUp]
        public void SetUp() => keyedValues = new(0);

        [Test]
        public void Count_Is_Zero_Initially() => Assert.That(keyedValues.Count, Is.Zero);

        [Test]
        public void Add_Increases_Count_By_One()
        {
            keyedValues.Add(1, "value1");
            Assert.That(keyedValues.Count, Is.EqualTo(1));

            keyedValues.Add(2, "value2");
            Assert.That(keyedValues.Count, Is.EqualTo(2));

            keyedValues.Add(3, "value3");
            Assert.That(keyedValues.Count, Is.EqualTo(3));
        }

        [Test]
        public void Remove_Decreases_Count_By_One()
        {
            keyedValues.Add(1, "value1");
            keyedValues.Add(2, "value2");
            keyedValues.Add(3, "value3");

            keyedValues.Remove(2);
            Assert.That(keyedValues.Count, Is.EqualTo(2));

            keyedValues.Remove(1);
            Assert.That(keyedValues.Count, Is.EqualTo(1));

            keyedValues.Remove(3);
            Assert.That(keyedValues.Count, Is.Zero);
        }

        [Test]
        public void Count_Unchanged_When_Removing_Non_Existent_Key()
        {
            keyedValues.Add(1, "value1");
            keyedValues.Add(2, "value2");

            keyedValues.Remove(3);
            Assert.That(keyedValues.Count, Is.EqualTo(2));
        }

        [Test]
        public void Count_Is_Zero_After_Clear()
        {
            keyedValues.Add(1, "value1");
            keyedValues.Add(2, "value2");
            keyedValues.Add(3, "value3");

            keyedValues.Clear();
            Assert.That(keyedValues.Count, Is.Zero);
        }

        [Test]
        public void GetActiveValueEnumeratorCount_Is_Zero_Initially() => Assert.AreEqual(0, keyedValues.GetActiveValueEnumeratorCount);

        [Test]
        public void GetActiveValueEnumeratorCount_Increases_By_One_When_GetEnumerator_Is_Executed()
        {
            keyedValues.Add(1, "value1");
            keyedValues.Add(2, "value2");

            using var enumerator1 = keyedValues.GetEnumerator();
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(1));

            using var enumerator2 = keyedValues.GetEnumerator();
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(2));

            using var enumerator3 = keyedValues.GetEnumerator();
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(3));
        }

        [Test]
        public void GetActiveValueEnumeratorCount_Decreases_By_One_When_Enumerator_Is_Disposed()
        {
            keyedValues.Add(1, "value1");
            keyedValues.Add(2, "value2");

            var enumerator1 = keyedValues.GetEnumerator();
            var enumerator2 = keyedValues.GetEnumerator();
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(2));

            enumerator1.Dispose();
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(1));

            enumerator2.Dispose();
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(0));
        }

        [Test]
        public void GetValueEnumeratorPoolCount_Is_Zero_Initially() => Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.Zero);

        [Test]
        public void GetValueEnumeratorPoolCount_Increases_By_One_When_Enumerator_Is_Disposed()
        {
            var enumerator1 = keyedValues.GetEnumerator();
            var enumerator2 = keyedValues.GetEnumerator();

            Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.Zero);
            enumerator1.Dispose();
            Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.EqualTo(1));
            enumerator2.Dispose();
            Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.EqualTo(2));
            var enumerator3 = keyedValues.GetEnumerator();
            Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.EqualTo(1));
            enumerator3.Dispose();
            Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.EqualTo(2));
        }

        [Test]
        public void Add_And_Remove_Adjust_Count_Correctly()
        {
            for (var i = 0; i < 10; i++)
            {
                keyedValues.Add(i, $"value{i}");
            }
            Assert.That(keyedValues.Count, Is.EqualTo(10));

            keyedValues.Remove(5);
            keyedValues.Remove(9);
            keyedValues.Remove(1);
            Assert.That(keyedValues.Count, Is.EqualTo(7));

            keyedValues.Add(11, "value11");
            keyedValues.Add(12, "value12");
            Assert.That(keyedValues.Count, Is.EqualTo(9));

            for (var i = 0; i < 10; i++)
            {
                keyedValues.Remove(i);
            }
            keyedValues.Remove(11);
            keyedValues.Remove(12);
            Assert.That(keyedValues.Count, Is.Zero);
        }

        [Test]
        public void Enumerator_Reuse_Works_Correctly()
        {
            keyedValues.Add(1, "value1");
            keyedValues.Add(2, "value2");
            keyedValues.Add(3, "value3");

            // First iteration should create a single enumerator
            foreach (var _ in keyedValues)
            {
                Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(1));
                Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.Zero);
            }
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.Zero);
            Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.EqualTo(1));

            // Second iteration should reuse the same enumerator from the pool
            foreach (var _ in keyedValues)
            {
                Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(1));
                Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.Zero);
            }
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.Zero);
            Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.EqualTo(1));

            // Third iteration with nested enumerators should create a second enumerator
            foreach (var _1 in keyedValues)
            {
                foreach (var _2 in keyedValues)
                {
                    Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.EqualTo(2));
                    Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.Zero);
                }
            }
            Assert.That(keyedValues.GetActiveValueEnumeratorCount, Is.Zero);
            Assert.That(keyedValues.GetValueEnumeratorPoolCount, Is.EqualTo(2));
        }

        [Test]
        public void Add_And_Remove_Adjust_Count_Correctly_During_Enumeration()
        {
            for (var i = 0; i < 10; i++)
            {
                keyedValues.Add(i, $"value{i}");
            }

            using var enumerator = keyedValues.GetEnumerator();
            Assert.That(keyedValues.Count, Is.EqualTo(10));

            keyedValues.Remove(5);
            keyedValues.Remove(9);
            keyedValues.Remove(1);
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(keyedValues.Count, Is.EqualTo(7));

            keyedValues.Add(11, "value11");
            keyedValues.Add(12, "value12");
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(keyedValues.Count, Is.EqualTo(9));

            for (var i = 0; i < 10; i++)
            {
                keyedValues.Remove(i);
            }
            keyedValues.Remove(11);
            keyedValues.Remove(12);
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.That(keyedValues.Count, Is.Zero);
        }

        [Test]
        public void Items_Added_During_Iteration_Are_Not_Iterated_Over()
        {
            var originalValues = new[] { "1", "2", "3", "4", "5" };
            for (var i = 0; i < originalValues.Length; i++)
            {
                keyedValues.Add(i + 1, originalValues[i]);
            }

            var valuesAddedDuringIteration = new[] { "6", "7", "8", "9", "10" };
            var iteratedValues = new HashSet<string>();
            var index = 0;
            foreach (var item in keyedValues)
            {
                keyedValues.Add(6 + index, valuesAddedDuringIteration[index]);
                Assert.That(iteratedValues.Add(item), Is.True);
                index++;
            }

            Assert.That(iteratedValues, Is.EquivalentTo(originalValues));
        }

        [Test]
        public void Items_Removed_During_Iteration_Are_Not_Iterated_Over()
        {
            var originalValues = new[] { "1", "2", "3", "4", "5", "6" };
            for (var i = 0; i < originalValues.Length; i++)
            {
                keyedValues.Add(i + 1, originalValues[i]);
            }

            var firstIteratedValue = "";
            var iteratedValues = new HashSet<string>();
            foreach (var item in keyedValues)
            {
                firstIteratedValue = item;
                keyedValues.Remove(1);
                keyedValues.Remove(2);
                keyedValues.Remove(3);
                Assert.That(iteratedValues.Add(item), Is.True);
            }

            var expectedValues = new HashSet<string> { firstIteratedValue, "4", "5", "6" };
            Assert.That(iteratedValues, Is.EquivalentTo(expectedValues));
        }

        [Test]
        public void Iteration_Works_After_Many_Adds_And_Removes()
        {
            var count = 0;
            for (var x = 0; x < 10; x++)
            {
                for (var y = 0; y < 10; y++)
                {
                    count++;
                    keyedValues.Add(count, "value" + count);
                    Assert.That(keyedValues.Count, Is.EqualTo(count));
                }

                var iteratedCount = 0;
                foreach (var value in keyedValues)
                {
                    Assert.That(value, Is.Not.Null);
                    iteratedCount++;
                }
                Assert.That(iteratedCount, Is.EqualTo(count));
            }

            for (var x = 0; x < 10; x++)
            {
                for (var y = 0; y < 10; y++)
                {
                    keyedValues.Remove(count);
                    count--;
                    Assert.That(keyedValues.Count, Is.EqualTo(count));
                }

                var iteratedCount = 0;
                foreach (var value in keyedValues)
                {
                    Assert.That(value, Is.Not.Null);
                    iteratedCount++;
                }
                Assert.That(iteratedCount, Is.EqualTo(count));
            }

            Assert.That(count, Is.Zero);
        }
    }
}
