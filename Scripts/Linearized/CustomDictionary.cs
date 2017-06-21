using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections;

namespace MapReduce.NET.Collections
{
    public class CustomDictionary<K, V> : IDictionary<K, V>, IDictionary
    {
        int[] hashes;
        DictionaryEntry[] entries;
        const int initialsize = 89;
        int nextfree;
        const float loadfactor = 1f;
        static readonly uint[] primeSizes = new uint[]{ 89, 179, 359, 719, 1439, 2879, 5779, 11579, 23159, 46327,
                                        92657, 185323, 370661, 741337, 1482707, 2965421, 5930887, 11861791,
                                        23723599, 47447201, 94894427, 189788857, 379577741, 759155483};

        //int maxitems = (int)( initialsize * loadfactor );

        private struct DictionaryEntry
        {
            public K key;
            public int next;
            public V value;
            public uint hashcode;
        }

        public CustomDictionary()
        {
            Initialize();
        }

        public int InitOrGetPosition(K key)
        {
            return Add(key, default(V), false);
        }

        public V GetAtPosition(int pos)
        {
            return entries[pos].value;
        }

        public void StoreAtPosition(int pos, V value)
        {
            entries[pos].value = value;
        }

        public int Add(K key, V value, bool overwrite)
        {
            if (nextfree >= entries.Length)
                Resize();

            uint hash = (uint)key.GetHashCode();

            uint hashPos = hash % (uint)hashes.Length;

            int entryLocation = hashes[hashPos];

            int storePos = nextfree;


            if (entryLocation != -1) // already there
            {
                int currEntryPos = entryLocation;

                do
                {
                    DictionaryEntry entry = entries[currEntryPos];

                    // same key is in the dictionary
                    if (key.Equals(entry.key))
                    {
                        if (!overwrite)
                            return currEntryPos;

                        storePos = currEntryPos;
                        break; // do not increment nextfree - overwriting the value
                    }

                    currEntryPos = entry.next;

                } while (currEntryPos > -1);

                nextfree++;
            }
            else // new value
            {
                //hashcount++;
                nextfree++;
            }

            hashes[hashPos] = storePos;

            entries[storePos].next = entryLocation;
            entries[storePos].key = key;
            entries[storePos].value = value;
            entries[storePos].hashcode = hash;

            return storePos;
        }

        private void Resize()
        {
            uint newsize = FindNewSize();
            int[] newhashes = new int[newsize];
            DictionaryEntry[] newentries = new DictionaryEntry[newsize];

            Array.Copy(entries, newentries, nextfree);

            for (int i = 0; i < newsize; i++)
            {
                newhashes[i] = -1;
            }

            for (int i = 0; i < nextfree; i++)
            {
                uint pos = newentries[i].hashcode % newsize;
                int prevpos = newhashes[pos];
                newhashes[pos] = i;

                if (prevpos != -1)
                    newentries[i].next = prevpos;
            }

            hashes = newhashes;
            entries = newentries;

            //maxitems = (int) (newsize * loadfactor );
        }

        private uint FindNewSize()
        {
            uint roughsize = (uint)hashes.Length * 2 + 1;

            for (int i = 0; i < primeSizes.Length; i++)
            {
                if (primeSizes[i] >= roughsize)
                    return primeSizes[i];
            }

            throw new NotImplementedException("Too large array");
        }

        public V Get(K key)
        {
            int pos = GetPosition(key);

            if (pos == -1)
                throw new Exception("Key does not exist");

            return entries[pos].value;
        }

        public int GetPosition(K key)
        {
            uint hash = (uint)key.GetHashCode();

            uint pos = hash % (uint)hashes.Length;

            int entryLocation = hashes[pos];

            if (entryLocation == -1)
                return -1;

            int nextpos = entryLocation;

            do
            {
                DictionaryEntry entry = entries[nextpos];

                if (key.Equals(entry.key))
                    return nextpos;

                nextpos = entry.next;

            } while (nextpos != -1);

            return -1;
        }

        public bool ContainsKey(K key)
        {
            return GetPosition(key) != -1;
        }

        public ICollection<K> Keys
        {
            get { throw new NotImplementedException(); }
        }

        public bool Remove(K key)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(K key, out V value)
        {
            int pos = GetPosition(key);

            if (pos == -1)
            {
                value = default(V);
                return false;
            }

            value = entries[pos].value;

            return true;
        }

        public ICollection<V> Values
        {
            get { throw new NotImplementedException(); }
        }

        public V this[K key]
        {
            get
            {
                return Get(key);
            }
            set
            {
                Add(key, value, true);
            }
        }

        public void Add(KeyValuePair<K, V> item)
        {
            int pos = Add(item.Key, item.Value, false);

            if (pos + 1 != nextfree)
                throw new Exception("Key already exists");
        }

        void IDictionary<K, V>.Add(K key, V value)
        {
            int pos = Add(key, value, false);

            if (pos + 1 != nextfree)
                throw new Exception("Key already exists");
        }

        public void Clear()
        {
            Initialize();
        }

        private void Initialize()
        {
            this.hashes = new int[initialsize];
            this.entries = new DictionaryEntry[initialsize];
            nextfree = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                hashes[i] = -1;
            }
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            if (item.Key == null)
                return false;


            V value;

            if (!TryGetValue(item.Key, out value))
                return false;

            if (!item.Value.Equals(value))
                return false;

            return true;
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return nextfree; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            for (int i = 0; i < nextfree; i++)
            {
                yield return new KeyValuePair<K,V>(entries[i].key, entries[i].value);
            }
        }


        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < nextfree; i++)
            {
                yield return new KeyValuePair<K, V>(entries[i].key, entries[i].value);
            }
        }

        public void Add(object key, object value)
        {
            int pos = Add((K)key, (V)value, false);

            if (pos + 1 != nextfree)
                throw new Exception("Key already exists");
        }

        public bool Contains(object key)
        {
            return Contains((K)key);
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool IsFixedSize
        {
            get { throw new NotImplementedException(); }
        }

        ICollection IDictionary.Keys
        {
            get { throw new NotImplementedException(); }
        }

        public void Remove(object key)
        {
            throw new NotImplementedException();
        }

        ICollection IDictionary.Values
        {
            get { throw new NotImplementedException(); }
        }

        public object this[object key]
        {
            get
            {
                return this[(K)key];
            }
            set
            {
                this[(K)key] = (V)value;
            }
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public bool IsSynchronized
        {
            get { throw new NotImplementedException(); }
        }

        public object SyncRoot
        {
            get { throw new NotImplementedException(); }
        }
    }
}
