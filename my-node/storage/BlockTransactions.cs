using NBitcoin;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroFormatter;
using my_node.extensions;
using NBitcoin.Protocol;

namespace my_node.storage
{
    public class BlockTransactions : StorageBase, IDictionary<uint256, Dictionary<uint256, bool>>
    {
        private readonly ReaderWriterLock _lock = new ReaderWriterLock();
        private Dictionary<uint256, Dictionary<uint256, bool>> _blockTransactions;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _syncThreads;

        public override string FileName => ".blockTransactions";

        public BlockTransactions(string basePath = null)
            : base(basePath)
        {
            _blockTransactions = new Dictionary<uint256, Dictionary<uint256, bool>>();
            _syncThreads = 16;
            _semaphore = new SemaphoreSlim(_syncThreads);
        }

        public override bool Load()
        {
            if (!File.Exists(FullPath))
                return false;

            using (var stream = new FileStream(FullPath, FileMode.Open))
            using (_lock.LockWrite())
                _blockTransactions = ZeroFormatterSerializer.Deserialize<Dictionary<uint256, Dictionary<uint256, bool>>>(stream);

            return true;

        }

        public override void Save()
        {
            using (var stream = new FileStream(FullPath, FileMode.Create))
            {
                using (_lock.LockRead())
                    ZeroFormatterSerializer.Serialize(stream, _blockTransactions);

                Console.WriteLine($"\rBlockTransaction file saved to {stream.Name}");
            }
        }

        public void Sync(Blocks blocks, NodeManager nodeManager, CancellationToken cancelationToken)
        {
            blocks.OnSyncFinished += (source, e) => { Save(); };

            Task.Run(async () =>
            {
                var tip = blocks.GetTip();
                var blocksToSync = new List<uint256>();

                using (_lock.LockRead())
                    while (!_blockTransactions.ContainsKey((tip.Hash)))
                    {
                        blocksToSync.Add(tip.Hash);

                        if (tip.Previous == null)
                            break;

                        tip = blocks.GetBlock(tip.Previous);
                    }

                blocksToSync.Reverse();

                Console.WriteLine($"\rGetting {blocksToSync.Count} blocks");
                var nodes = new ConcurrentQueue<Node>();
                for (var i = 0; i < _syncThreads * 1.5; i++)
                    nodes.Enqueue(nodeManager.GetNode());

                var syncQueue = new ConcurrentQueue<uint256>(blocksToSync);

                var count = 1;
                while (!syncQueue.IsEmpty)
                {
                    await blocks.WaitSync();

                    Console.Write(
                        $"\rSyncing: {Math.Round((decimal)count++ / blocksToSync.Count, 5, MidpointRounding.AwayFromZero)}%");
                    _semaphore.Wait(cancelationToken);
                    var count1 = count;
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        uint256 blockHash = null;
                        nodes.TryDequeue(out var node);
                        try
                        {
                            if (!syncQueue.TryDequeue(out blockHash))
                                return;

                            var block = node.GetBlocks(new List<uint256> { blockHash }).First();
                            var transactions = new Dictionary<uint256, bool>();

                            foreach (var blockTransaction in block.Transactions)
                                transactions.Add(blockTransaction.GetHash(), false);

                            using (_lock.LockWrite())
                                _blockTransactions.Add(block.GetHash(), transactions);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"\rERROR: {e}");
                            syncQueue.Enqueue(blockHash);
                            node = nodeManager.GetNode();
                        }
                        finally
                        {
                            nodes.Enqueue(node);
                            _semaphore.Release();
                        }

                    }, cancelationToken);
                }
            }, cancelationToken);
        }

        #region Interface Implementation 
        public Dictionary<uint256, bool> this[uint256 key]
        {
            get
            {
                using (_lock.LockRead())
                    return _blockTransactions[key];
            }
            set
            {
                using (_lock.LockWrite())
                    _blockTransactions[key] = value;
            }
        }

        public ICollection<uint256> Keys
        {
            get
            {
                ICollection<uint256> keys = null;
                using (_lock.LockRead())
                    keys = _blockTransactions.Keys;

                return keys;
            }
        }

        public ICollection<Dictionary<uint256, bool>> Values
        {
            get
            {
                ICollection<Dictionary<uint256, bool>> values = null;
                using (_lock.LockRead())
                    values = _blockTransactions.Values;

                return values;
            }
        }

        public int Count
        {
            get
            {
                var count = 0;
                using (_lock.LockRead())
                    count = _blockTransactions.Count;

                return count;
            }
        }

        public bool IsReadOnly => false;

        public void Add(uint256 key, Dictionary<uint256, bool> value)
        {
            using (_lock.LockWrite())
                _blockTransactions.Add(key, value);
        }

        public void Add(KeyValuePair<uint256, Dictionary<uint256, bool>> item)
        {
            using (_lock.LockWrite())
                _blockTransactions.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            using (_lock.LockWrite())
                _blockTransactions.Clear();
        }

        public bool Contains(KeyValuePair<uint256, Dictionary<uint256, bool>> item)
        {
            var containsItem = false;
            using (_lock.LockRead())
                containsItem = _blockTransactions.ContainsKey(item.Key) || _blockTransactions[item.Key] == item.Value;

            return containsItem;
        }

        public bool ContainsKey(uint256 key)
        {
            var containsKey = false;
            using (_lock.LockRead())
                containsKey = _blockTransactions.ContainsKey(key);

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
                enumerator = _blockTransactions.GetEnumerator();

            return enumerator;
        }

        public bool Remove(uint256 key)
        {
            var remove = false;
            using (_lock.LockWrite())
                remove = _blockTransactions.Remove(key);

            return remove;
        }

        public bool Remove(KeyValuePair<uint256, Dictionary<uint256, bool>> item)
        {
            var remove = false;
            using (_lock.LockWrite())
                remove = _blockTransactions.Remove(item.Key);

            return remove;
        }

        public bool TryGetValue(uint256 key, out Dictionary<uint256, bool> value)
        {
            var tryGetValue = false;
            using (_lock.LockRead())
                tryGetValue = _blockTransactions.TryGetValue(key, out value);

            return tryGetValue;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            IEnumerator<KeyValuePair<uint256, Dictionary<uint256, bool>>> enumerator = null;
            using (_lock.LockRead())
                enumerator = _blockTransactions.GetEnumerator();

            return enumerator;
        }
        #endregion
    }
}
