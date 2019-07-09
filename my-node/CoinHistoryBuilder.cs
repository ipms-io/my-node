using my_node.models;
using my_node.storage;
using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace my_node
{
    public class CoinHistoryBuilder
    {
        private readonly Blocks _blocks;
        private readonly BlockTransactions _blockTransactions;
        private readonly Transactions _transactions;
        private readonly NodeManager _nodeManager;
        private readonly Queue<Search> _queue;

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

            _queue = new Queue<Search>();
            _node = _nodeManager.GetNode();
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    if (_queue.TryDequeue(out Search search))
                    {
                        await BuildCoinHistory(search);
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }, _cancellationTokenSource.Token);
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
                Block block = null;

                if (search.OutPoint != null && search.OutPoint.Hash != 0)
                {
                    var knownBlock = _blockTransactions.FirstOrDefault(x => x.Value.ContainsKey(search.OutPoint.Hash));
                    if (knownBlock.Key != null)
                    {
                        // We already mapped this tx
                        if (_blockTransactions[knownBlock.Key][search.OutPoint.Hash])
                        {
                            if (_transactions.TryGetValue(search.OutPoint.Hash, out Transaction tx))
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

                                        _queue.Enqueue(new Search { BlockHash = blockHash, OutPoint = txIn.PrevOut });
                                    }

                                    return Task.FromResult(true);
                                }
                            }
                        }
                    }
                }

                block = GetBlock(search.BlockHash);

                bool found = false;
                var txMap = new Dictionary<uint256, bool>();
                foreach (var tx in block.Transactions)
                {
                    if (found)
                        txMap.Add(tx.GetHash(), false);
                    else
                    {
                        if (search.OutPoint != null && search.OutPoint.Hash != 0 && tx.GetHash() == search.OutPoint.Hash)
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
                                    var blockHash = block.Header.HashPrevBlock;
                                    var knownBlock = GetKnownBlockFromTransactionHash(txIn.PrevOut.Hash);
                                    if (knownBlock.Key != null)
                                        blockHash = knownBlock.Key;

                                    _queue.Enqueue(new Search { BlockHash = blockHash, OutPoint = txIn.PrevOut });
                                }
                            }
                        }
                        else if (search.Address != null)
                        {
                            var coins = tx.Outputs.AsCoins();
                            foreach (var coin in coins)
                            {
                                if (coin.TxOut.IsTo(search.Address))
                                {
                                    found = true;

                                    if (tx.IsCoinBase)
                                    {
                                        Console.WriteLine($"Coinbase reached at block {block.GetHash()}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Found address, going deeper on {tx.Inputs.Count} inputs...");
                                        foreach (var txIn in tx.Inputs)
                                        {
                                            var blockHash = block.Header.HashPrevBlock;
                                            var knownBlock = GetKnownBlockFromTransactionHash(txIn.PrevOut.Hash);
                                            if (knownBlock.Key != null)
                                                blockHash = knownBlock.Key;

                                            _queue.Enqueue(new Search { BlockHash = blockHash, OutPoint = txIn.PrevOut });
                                        }
                                    }
                                }
                            }
                        }

                        if (found)
                        {
                            _transactions.TryAdd(tx.GetHash(), tx);
                            txMap.AddOrReplace(tx.GetHash(), true);
                        }
                        else
                            txMap.TryAdd(tx.GetHash(), false);
                    }
                }

                if (!found)
                {
                    Console.WriteLine("Nothing found, going to previous block");
                    _queue.Enqueue(new Search { BlockHash = block.Header.HashPrevBlock, OutPoint = search.OutPoint });
                }

                _blockTransactions.AddOrReplace(search.BlockHash, txMap);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.ToString()}");
                _node.Dispose();
                _node = _nodeManager.GetNode();
                _queue.Enqueue(search);
            }

            return Task.FromResult(false);
        }

        private KeyValuePair<uint256, Dictionary<uint256, bool>> GetKnownBlockFromTransactionHash(uint256 txHash)
        {
            return _blockTransactions.FirstOrDefault(x => x.Value.ContainsKey(txHash));
        }

        private Block GetBlock(uint256 blockHash)
        {
            Console.WriteLine($"Looking for transactions in block 0x{blockHash}");
            var blocks = _node.GetBlocks(new List<uint256> { blockHash });
            return blocks.FirstOrDefault();
        }
    }
}

