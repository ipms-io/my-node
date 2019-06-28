using my_node.models;
using my_node.storage;
using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace my_node
{
    public class CoinHistoryBuilder
    {
        private readonly BitcoinAddress[] _addresses = new BitcoinAddress[1] { Bitcoin.Instance.Mainnet.CreateBitcoinAddress("38J8cCMJiERVKAN1W32g1CPVmniYymjJns") };
        private readonly Blocks _blocks;
        private readonly BlockTransactions _blockTransactions;
        private readonly Transactions _transactions;
        private readonly NodeManager _nodeManager;
        private readonly ConcurrentQueue<Search> _searchQueue;
        private readonly ConcurrentQueue<Search> _transactionQueue;
        private readonly SemaphoreSlim _semaphore;
        ConcurrentDictionary<uint256, Block> _blockCache;

        private CancellationTokenSource _cancellationTokenSource;
        private Node _node;

        public CoinHistoryBuilder(Blocks blocks,
                                  BlockTransactions blockTransactions,
                                  Transactions transactions,
                                  NodeManager nodeManager)
        {
            _blocks = blocks;
            _blockTransactions = blockTransactions;
            _transactions = transactions;
            _nodeManager = nodeManager;

            _searchQueue = new ConcurrentQueue<Search>();
            _transactionQueue = new ConcurrentQueue<Search>();
            _node = _nodeManager.GetNode();

            _semaphore = new SemaphoreSlim(10);

            _blockCache = new ConcurrentDictionary<uint256, Block>();
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    if (_searchQueue.TryDequeue(out Search search))
                    {
                        await _semaphore.WaitAsync(_cancellationTokenSource.Token);
                        await BuildCoinHistory(search);
                        _semaphore.Release();
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }, _cancellationTokenSource.Token);

            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    if (_transactionQueue.TryDequeue(out Search search))
                    {
                        await _semaphore.WaitAsync(_cancellationTokenSource.Token);
                        await FindAddressInTransaction(search);
                        _semaphore.Release();
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            });
        }

        public void Stop()
        {
            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();
        }

        public Task<bool> BuildCoinHistory(Search search)
        {
            try
            {
                if (search.OutPoints.Count > 0)
                {
                    foreach (var outPoint in search.OutPoints)
                    {
                        var knownBlock = _blockTransactions.FirstOrDefault(x => x.Value.Contains(outPoint.Hash));
                        if (knownBlock.Key != null)
                        {
                            if (_transactions.TryGetValue(outPoint.Hash, out Transaction tx))
                            {
                                if (tx.IsCoinBase)
                                {
                                    Console.WriteLine("Found coinbase!");
                                    return Task.FromResult(true);
                                }
                                else
                                {
                                    Console.WriteLine($"Tx already mapped, going deeper on {tx.Inputs.Count} inputs...");
                                    foreach (var txIn in tx.Inputs)
                                    {
                                        var blockHash = _blocks.GetPreviousBlockHash(knownBlock.Key);
                                        knownBlock = GetKnownBlockFromTransactionHash(txIn.PrevOut.Hash);
                                        if (knownBlock.Key != null)
                                            blockHash = knownBlock.Key;

                                        _searchQueue.Enqueue(new Search { BlockHash = blockHash, OutPoints = new List<OutPoint> { txIn.PrevOut } });
                                    }

                                    return Task.FromResult(true);
                                }
                            }
                        }
                    }
                }

                Block block = GetBlock(search.BlockHash);

                bool found = false;
                var txMap = new Dictionary<uint256, bool>();
                var outPoints = new List<OutPoint>();
                foreach (var tx in block.Transactions)
                {
                    if (search.OutPoints.Count > 0)
                        foreach (var outPoint in search.OutPoints)
                            if (tx.GetHash() == outPoint.Hash)
                            {
                                found = true;

                                if (tx.IsCoinBase)
                                {
                                    Console.WriteLine($"Coinbase reached at block {block.GetHash()}");
                                }
                                else
                                {
                                    Console.WriteLine($"Found output, going deeper on {tx.Inputs.Count} inputs...");
                                    foreach (var txIn in tx.Inputs)
                                    {
                                        var knownBlock = GetKnownBlockFromTransactionHash(txIn.PrevOut.Hash);
                                        if (knownBlock.Key != null)
                                            _searchQueue.Enqueue(new Search { BlockHash = knownBlock.Key, OutPoints = new List<OutPoint> { txIn.PrevOut } });
                                        else
                                            outPoints.Add(txIn.PrevOut);
                                    }
                                }
                            }

                    _searchQueue.Enqueue(new Search { BlockHash = _blocks.GetPreviousBlockHash(search.BlockHash), OutPoints = outPoints });
                    _transactionQueue.Enqueue(new Search { BlockHash = search.BlockHash, Transaction = tx });
                }

                if (!found)
                {
                    Console.WriteLine("Going to previous block");
                    _searchQueue.Enqueue(new Search { BlockHash = block.Header.HashPrevBlock, OutPoints = search.OutPoints });
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.ToString()}");
                _node.Dispose();
                _node = _nodeManager.GetNode();
                _searchQueue.Enqueue(search);
            }

            search.OutPoints.Clear();
            search.OutPoints = null;
            search = null;

            return Task.FromResult(false);
        }

        private Task FindAddressInTransaction(Search search)
        {
            return Task.Run(() =>
            {
                var transactionHash = search.Transaction.GetHash();
                var coins = search.Transaction.Outputs.AsCoins();
                var outPoints = new List<OutPoint>();
                foreach (var coin in coins)
                {
                    foreach (var address in _addresses)
                        if (coin.TxOut.IsTo(address))
                        {
                            if (search.Transaction.IsCoinBase)
                            {
                                Console.WriteLine($"Coinbase reached at block {search.BlockHash}");
                            }
                            else
                            {
                                Console.WriteLine($"Found address, going deeper on {search.Transaction.Inputs.Count} inputs...");
                                foreach (var txIn in search.Transaction.Inputs)
                                {
                                    var knownBlock = GetKnownBlockFromTransactionHash(txIn.PrevOut.Hash);
                                    if (knownBlock.Key != null)
                                        _searchQueue.Enqueue(new Search { BlockHash = knownBlock.Key, OutPoints = new List<OutPoint> { txIn.PrevOut } });
                                    else
                                        outPoints.Add(txIn.PrevOut);
                                }
                            }

                            _transactions.TryAdd(transactionHash, search.Transaction);
                        }
                }

                _searchQueue.Enqueue(new Search { BlockHash = _blocks.GetPreviousBlockHash(search.BlockHash), OutPoints = outPoints });

                _blockTransactions.TryAdd(search.BlockHash, transactionHash);
            });
        }

        private KeyValuePair<uint256, HashSet<uint256>> GetKnownBlockFromTransactionHash(uint256 txHash)
        {
            return _blockTransactions.FirstOrDefault(x => x.Value.Contains(txHash));
        }

        private Block GetBlock(uint256 blockHash)
        {
            Console.WriteLine($"Looking for transactions in block 0x{blockHash}");
            if (!_blockCache.TryGetValue(blockHash, out Block block))
            {
                var blocks = _node.GetBlocks(new List<uint256> { blockHash });
                block = blocks.FirstOrDefault();
                _blockCache.TryAdd(blockHash, block);
            }

            return block;
        }
    }
}

