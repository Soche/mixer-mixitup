﻿using System.Collections.Generic;
using System.Linq;

namespace MixItUp.Base.Util
{
    public class DatabaseDictionary<K, V> : LockedDictionary<K, V>
    {
        private HashSet<K> addedValues = new HashSet<K>();
        private HashSet<K> changedValues = new HashSet<K>();
        private HashSet<K> removedValues = new HashSet<K>();

        private object valuesUpdateLock = new object();

        public DatabaseDictionary() { }

        public override V this[K key]
        {
            get
            {
                if (key != null)
                {
                    this.ValueChanged(key);
                    return base[key];
                }
                return default(V);
            }
            set
            {
                if (key != null)
                {
                    if (!this.ContainsKey(key))
                    {
                        this.ValueAdded(key);
                    }
                    base[key] = value;
                    this.ValueChanged(key);
                }
            }
        }

        public override void Add(K key, V value)
        {
            if (key != null)
            {
                base.Add(key, value);
                this.ValueAdded(key);
            }
        }

        public override bool Remove(K key)
        {
            if (key != null)
            {
                this.ValueRemoved(key);
                return base.Remove(key);
            }
            return false;
        }

        public override void Clear()
        {
            lock (valuesUpdateLock)
            {
                foreach (K key in this.Keys.ToList())
                {
                    this.removedValues.Add(key);
                }
            }
            base.Clear();
        }

        public IEnumerable<K> GetAddedKeys() { return this.GetKeyValues(this.addedValues).Keys; }

        public IEnumerable<V> GetAddedValues() { return this.GetKeyValues(this.addedValues).Values; }

        public IEnumerable<K> GetChangedKeys() { return this.GetKeyValues(this.changedValues).Keys; }

        public IEnumerable<V> GetChangedValues() { return this.GetKeyValues(this.changedValues).Values; }

        public IEnumerable<K> GetRemovedValues()
        {
            lock (valuesUpdateLock)
            {
                IEnumerable<K> values = this.removedValues.ToList();
                this.removedValues.Clear();
                return values;
            }
        }

        public void ManualValueChanged(K key)
        {
            if (key != null)
            {
                this.ValueChanged(key);
            }
        }

        public void ClearTracking()
        {
            this.addedValues.Clear();
            this.changedValues.Clear();
            this.removedValues.Clear();
        }

        private Dictionary<K, V> GetKeyValues(HashSet<K> keys)
        {
            lock (valuesUpdateLock)
            {
                Dictionary<K, V> values = new Dictionary<K, V>();
                foreach (K key in keys)
                {
                    if (base.ContainsKey(key))
                    {
                        values[key] = base[key];
                    }
                }
                keys.Clear();
                return values;
            }
        }

        private void ValueAdded(K key)
        {
            lock (valuesUpdateLock)
            {
                this.addedValues.Add(key);
            }
        }

        private void ValueChanged(K key)
        {
            lock (valuesUpdateLock)
            {
                if (this.ContainsKey(key))
                {
                    this.changedValues.Add(key);
                }
            }
        }

        private void ValueRemoved(K key)
        {
            lock (valuesUpdateLock)
            {
                this.removedValues.Add(key);
            }
        }
    }
}
