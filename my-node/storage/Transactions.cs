using my_node.extensions;
using NBitcoin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ZeroFormatter;

namespace my_node.storage
{
    public class Transactions : StorageBase, IDictionary<uint256, Transaction>
    {
        private readonly ReaderWriterLock _lock = new ReaderWriterLock();
        private Dictionary<uint256, Transaction> _transactions;

        public override string FileName => ".transactions";

        public Transactions(string basePath = null)
            : base(basePath)
        {
            _transactions = new Dictionary<uint256, Transaction>();
        }

        public override bool Load()
        {
            if (File.Exists(FullPath))
            {
                using (var stream = new FileStream(FullPath, FileMode.Open))
                using (_lock.LockWrite())
                    _transactions = ZeroFormatterSerializer.Deserialize<Dictionary<uint256, Transaction>>(stream);

                return true;
            }

            return false;
        }
        public override void Save()
        {
            using (var stream = new FileStream(FullPath, FileMode.Create))
            {
                using (_lock.LockRead())
                    ZeroFormatterSerializer.Serialize(stream, _transactions);

                Console.WriteLine($"BlockTransaction file saved to {stream.Name}");
            }
        }

        #region Interface Implementation
        public Transaction this[uint256 key]
        {
            get
            {
                using (_lock.LockRead())
                    return _transactions[key];
            }
            set
            {
                using (_lock.LockWrite())
                    _transactions[key] = value;
            }
        }

        public ICollection<uint256> Keys
        {
            get
            {
                ICollection<uint256> keys = null;
                using (_lock.LockRead())
                    keys = _transactions.Keys;

                return keys;
            }
        }

        public ICollection<Transaction> Values
        {
            get
            {
                ICollection<Transaction> values = null;
                using (_lock.LockRead())
                    values = _transactions.Values;

                return values;
            }
        }

        public int Count
        {
            get
            {
                int count = 0;
                using (_lock.LockRead())
                    count = _transactions.Count;

                return count;
            }
        }

        public bool IsReadOnly => false;

        public void Add(uint256 key, Transaction value)
        {
            using (_lock.LockWrite())
                _transactions.Add(key, value);
        }

        public void Add(KeyValuePair<uint256, Transaction> item)
        {
            using (_lock.LockWrite())
                _transactions.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            using (_lock.LockWrite())
                _transactions.Clear();
        }

        public bool Contains(KeyValuePair<uint256, Transaction> item)
        {
            var containsItem = false;
            using (_lock.LockRead())
                containsItem = _transactions.ContainsKey(item.Key) || _transactions[item.Key] == item.Value;

            return containsItem;
        }

        public bool ContainsKey(uint256 key)
        {
            var containsKey = false;
            using (_lock.LockRead())
                containsKey = _transactions.ContainsKey(key);

            return containsKey;
        }

        public void CopyTo(KeyValuePair<uint256, Transaction>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<uint256, Transaction>> GetEnumerator()
        {
            IEnumerator<KeyValuePair<uint256, Transaction>> enumerator = null;
            using (_lock.LockRead())
                enumerator = _transactions.GetEnumerator();

            return enumerator;
        }

        public bool Remove(uint256 key)
        {
            var remove = false;
            using (_lock.LockWrite())
                remove = _transactions.Remove(key);

            return remove;
        }

        public bool Remove(KeyValuePair<uint256, Transaction> item)
        {
            var remove = false;
            using (_lock.LockWrite())
                remove = _transactions.Remove(item.Key);

            return remove;
        }


        public bool TryGetValue(uint256 key, out Transaction value)
        {
            var tryGetValue = false;
            using (_lock.LockRead())
                tryGetValue = _transactions.TryGetValue(key, out value);

            return tryGetValue;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            IEnumerator<KeyValuePair<uint256, Transaction>> enumerator = null;
            using (_lock.LockRead())
                enumerator = _transactions.GetEnumerator();

            return enumerator;
        }
        #endregion
    }
}
