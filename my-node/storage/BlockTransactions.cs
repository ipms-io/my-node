using NBitcoin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ZeroFormatter;
using my_node.extensions;

namespace my_node.storage
{
    public class BlockTransactions : StorageBase, IDictionary<uint256, Dictionary<uint256, bool>>
    {
        private readonly ReaderWriterLock _lock = new ReaderWriterLock();
        private Dictionary<uint256, Dictionary<uint256, bool>> _blockTransaction;

        public override string FileName => ".blockTransactions";

        public BlockTransactions(string basePath = null)
            : base(basePath)
        {
            _blockTransaction = new Dictionary<uint256, Dictionary<uint256, bool>>();
        }

        public override bool Load()
        {
            if (!File.Exists(FullPath))
                return false;

            using (var stream = new FileStream(FullPath, FileMode.Open))
            using (_lock.LockWrite())
                _blockTransaction = ZeroFormatterSerializer.Deserialize<Dictionary<uint256, Dictionary<uint256, bool>>>(stream);
                
            return true;

        }

        public override void Save()
        {
            using (var stream = new FileStream(FullPath, FileMode.Create))
            {
                using (_lock.LockRead())
                    ZeroFormatterSerializer.Serialize(stream, _blockTransaction);

                Console.WriteLine($"BlockTransaction file saved to {stream.Name}");
            }
        }

        #region Interface Implementation 
        public Dictionary<uint256, bool> this[uint256 key]
        {
            get
            {
                using (_lock.LockRead())
                    return _blockTransaction[key];
            }
            set
            {
                using (_lock.LockWrite())
                    _blockTransaction[key] = value;
            }
        }

        public ICollection<uint256> Keys
        {
            get
            {
                ICollection<uint256> keys = null;
                using (_lock.LockRead())
                    keys = _blockTransaction.Keys;

                return keys;
            }
        }

        public ICollection<Dictionary<uint256, bool>> Values
        {
            get
            {
                ICollection<Dictionary<uint256, bool>> values = null;
                using (_lock.LockRead())
                    values = _blockTransaction.Values;

                return values;
            }
        }

        public int Count
        {
            get
            {
                var count = 0;
                using (_lock.LockRead())
                    count = _blockTransaction.Count;

                return count;
            }
        }

        public bool IsReadOnly => false;

        public void Add(uint256 key, Dictionary<uint256, bool> value)
        {
            using (_lock.LockWrite())
                _blockTransaction.Add(key, value);
        }

        public void Add(KeyValuePair<uint256, Dictionary<uint256, bool>> item)
        {
            using (_lock.LockWrite())
                _blockTransaction.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            using (_lock.LockWrite())
                _blockTransaction.Clear();
        }

        public bool Contains(KeyValuePair<uint256, Dictionary<uint256, bool>> item)
        {
            var containsItem = false;
            using (_lock.LockRead())
                containsItem = _blockTransaction.ContainsKey(item.Key) || _blockTransaction[item.Key] == item.Value;

            return containsItem;
        }

        public bool ContainsKey(uint256 key)
        {
            var containsKey = false;
            using (_lock.LockRead())
                containsKey = _blockTransaction.ContainsKey(key);

            return containsKey;
        }

        public void CopyTo(KeyValuePair<uint256, Dictionary<uint256, bool>>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<uint256, Dictionary<uint256, bool>>> GetEnumerator()
        {
            IEnumerator<KeyValuePair<uint256, Dictionary<uint256, bool>>> enumerator = null;
            using (_lock.LockRead())
                enumerator = _blockTransaction.GetEnumerator();

            return enumerator;
        }

        public bool Remove(uint256 key)
        {
            var remove = false;
            using (_lock.LockWrite())
                remove = _blockTransaction.Remove(key);

            return remove;
        }

        public bool Remove(KeyValuePair<uint256, Dictionary<uint256, bool>> item)
        {
            var remove = false;
            using (_lock.LockWrite())
                remove = _blockTransaction.Remove(item.Key);

            return remove;
        }

        public bool TryGetValue(uint256 key, out Dictionary<uint256, bool> value)
        {
            var tryGetValue = false;
            using (_lock.LockRead())
                tryGetValue = _blockTransaction.TryGetValue(key, out value);

            return tryGetValue;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            IEnumerator<KeyValuePair<uint256, Dictionary<uint256, bool>>> enumerator = null;
            using (_lock.LockRead())
                enumerator = _blockTransaction.GetEnumerator();

            return enumerator;
        }
        #endregion
    }
}
