// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public class SortedValueMap<K, V> : IEnumerable<KeyValuePair<K, V>>
    {
        private readonly Dictionary<K, V> dictionary;
        private readonly List<KeyValuePair<K, V>> sorted;
        private readonly Comparison<KeyValuePair<K, V>> comparer;
        private bool isSorted;

        public SortedValueMap(Comparison<KeyValuePair<K, V>> comparer)
        {
            this.comparer = comparer;
            dictionary = new Dictionary<K, V>();
            sorted = new List<KeyValuePair<K, V>>();
            isSorted = true;
        }

        public SortedValueMap(Comparison<KeyValuePair<K, V>> comparer, SortedValueMap<K, V> data)
        {
            this.comparer = comparer;
            this.dictionary = new Dictionary<K, V>(data.dictionary);
            sorted = new List<KeyValuePair<K, V>>(dictionary.Count);
            isSorted = false;
        }

        public SortedValueMap(Comparison<KeyValuePair<K, V>> comparer, IDictionary<K, V> data)
        {
            this.comparer = comparer;
            this.dictionary = new Dictionary<K, V>(data);
            sorted = new List<KeyValuePair<K, V>>(dictionary.Count);
            isSorted = false;
        }

        public SortedValueMap(Comparison<KeyValuePair<K, V>> comparer, int capacity)
        {
            this.comparer = comparer;
            dictionary = new Dictionary<K, V>(capacity);
            sorted = new List<KeyValuePair<K, V>>(capacity);
            isSorted = true;
        }

        /// <summary>
        /// Get enumerator for unsorted KeyValuePair's
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Clear()
        {
            dictionary.Clear();
            sorted.Clear();
            isSorted = true;
        }

        public int Count => dictionary.Count;

        public void Add(K key, V value)
        {
            dictionary.Add(key, value);
            isSorted = false;
        }

        public bool ContainsKey(K key)
        {
            return dictionary.ContainsKey(key);
        }

        public bool Remove(K key)
        {
            if (!dictionary.TryGetValue(key, out var v))
            {
                return false;
            }

            dictionary.Remove(key);
            sorted.Remove(new KeyValuePair<K, V>(key, v));
            return true;
        }

        public bool TryGetValue(K key, out V value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        public V this[K key]
        {
            get => dictionary[key];
            set
            {
                dictionary[key] = value;
                isSorted = false;
            }
        }

        public ICollection<K> Keys { get => dictionary.Keys; }

        public IReadOnlyList<KeyValuePair<K, V>> Sorted
        {
            get
            {
                if (!isSorted)
                {
                    sorted.Clear();
                    sorted.AddRange(dictionary);
                    sorted.Sort(comparer);
                    isSorted = true;
                }

                return sorted;
            }
        }
    }
}
