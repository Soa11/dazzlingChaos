// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Utils
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Value collection that does not throw even if its contents are modified while its being iterated over using foreach.
    /// </summary>
    /// <remarks>
    /// Values are iterated in reverse order. This means that if new values are added while iterations
    /// are in progress, the new items will be excluded from those iterations.
    /// </remarks>
    internal sealed class Values<TValue>
    {
        private TValue[] values;
        private int count;
        private readonly HashSet<Enumerator> activeValueEnumerators = new();
        private readonly Stack<Enumerator> valueEnumeratorsPool = new();

        public int Count => count;
        internal int GetActiveValueEnumeratorCount => activeValueEnumerators.Count;
        internal int GetValueEnumeratorPoolCount => valueEnumeratorsPool.Count;

        public TValue this[int index] => values[index];

        public Values(int capacity = 16) => values = new TValue[capacity];

        public void Add(TValue value)
        {
            count++;
            if (count >= values.Length)
            {
                var capacity = count + count;
                Array.Resize(ref values, capacity);
            }

            values[count - 1] = value;
        }

        public void Remove(TValue value)
        {
            var index = Array.IndexOf(values, value, 0, count);
            if (index is -1)
            {
                return;
            }

            values[index] = default;

            count--;
            if (index >= count)
            {
                return;
            }

            // The number of elements to move is the total count minus the index of the item after the removed one.
            // Example: count is now 4, we removed index 1. We need to move elements from index 2 up to index 3.
            Array.Copy(values, index + 1, values, index, count - index);

            foreach (var valueEnumerator in activeValueEnumerators)
            {
                if (valueEnumerator.CurrentIndex > index)
                {
                    valueEnumerator.CurrentIndex--;
                }
            }
        }

        public void RemoveAt(int index)
        {
            values[index] = default;

            count--;
            if (index >= count)
            {
                return;
            }

            // The number of elements to move is the total count minus the index of the item after the removed one.
            // Example: count is now 4, we removed index 1. We need to move elements from index 2 up to index 3.
            Array.Copy(values, index + 1, values, index, count - index);

            foreach (var valueEnumerator in activeValueEnumerators)
            {
                if (valueEnumerator.CurrentIndex > index)
                {
                    valueEnumerator.CurrentIndex--;
                }
            }
        }

        public void Clear()
        {
            Array.Clear(values, 0, count);
            count = 0;
        }

        /// <summary>
        /// Gets an enumerator to iterate through all values in the collection.
        /// </summary>
        /// <remarks>
        /// The enumerator will not throw if the collection is modified in the middle of iteration.
        /// </remarks>
        public Enumerator GetEnumerator()
        {
            if (valueEnumeratorsPool.TryPop(out var enumerator))
            {
                enumerator.CurrentIndex = count;
                enumerator.Values = values;
            }
            else
            {
                enumerator = new(values, count, activeValueEnumerators, valueEnumeratorsPool);
            }

            activeValueEnumerators.Add(enumerator);
            return enumerator;
        }

        /// <summary>
        /// Enumerator that does not throw even if the enumerated items are modified in the middle of iteration.
        /// </summary>
        internal sealed class Enumerator : IDisposable
        {
            internal int CurrentIndex;
            internal TValue[] Values;
            private readonly HashSet<Enumerator> activeEnumerators;
            private readonly Stack<Enumerator> pool;

            public Enumerator(TValue[] values, int count, HashSet<Enumerator> activeEnumerators, Stack<Enumerator> pool)
            {
                Values = values;
                this.activeEnumerators = activeEnumerators;
                this.pool = pool;
                CurrentIndex = count; // Start at end for reverse iteration
            }

            public TValue Current => Values[CurrentIndex];

            public bool MoveNext()
            {
                if (CurrentIndex <= 0)
                {
                    return false;
                }

                CurrentIndex--;
                return true;
            }

            public void Dispose()
            {
                activeEnumerators.Remove(this);
                pool.Push(this);
            }
        }
    }
}
