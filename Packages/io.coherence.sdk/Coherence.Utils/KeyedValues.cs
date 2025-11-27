namespace Coherence.Utils
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Keyed value collection that does not throw even if its contents are modified
    /// while its being iterated over using foreach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optimized for fast iteration of values.
    /// </para>
    /// <para>
    /// Adding and removing items is O(n) since it uses arrays internally.
    /// </para>
    /// </remarks>
    internal sealed class KeyedValues<TKey, TValue>
    {
        private TKey[] keys;
        private TValue[] values;
        private int count;
        private readonly HashSet<Enumerator> activeValueEnumerators = new();
        private readonly Stack<Enumerator> valueEnumeratorsPool = new();

        internal int Count => count;
        internal int GetActiveValueEnumeratorCount => activeValueEnumerators.Count;
        internal int GetValueEnumeratorPoolCount => valueEnumeratorsPool.Count;

        public KeyedValues(int capacity = 16)
        {
            keys = new TKey[capacity];
            values = new TValue[capacity];
        }

        public void Add(TKey key, TValue value)
        {
            count++;
            if (count >= keys.Length)
            {
                var capacity = count + count;
                Array.Resize(ref keys, capacity);
                Array.Resize(ref values, capacity);
            }

            keys[count - 1] = key;
            values[count - 1] = value;
        }

        public void Remove(TKey key)
        {
            var index = Array.IndexOf(keys, key, 0, count);
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
            Array.Copy(keys, index + 1, keys, index, count - index);
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
            Array.Clear(keys, 0, count);
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
