using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using my_node.formatters;
using my_node.storage;

using static my_node.extensions.ConsoleExtensions;

namespace my_node
{
    class Program
    {
        private static Blocks _blocks;
        private static BlockTransactions _blockTransactions;
        private static Transactions _transactions;
        private static NodeManager _nodeManager;
        private static readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bitcoin");
        private static readonly string _slimChainFile = Path.Combine(_path, ".slimChain");
        private static readonly string _blockTransactionsFile = Path.Combine(_path, ".blockTransactions");
        private static readonly object _syncLock = new object();
        private static readonly CancellationTokenSource _syncCancellationTokenSource = new CancellationTokenSource();

        private static int _counter = 0;

        static async Task Main(string[] args)
        {
            RegisterFormatters.RegisterAll();
            _blocks = new Blocks();
            _blockTransactions = new BlockTransactions();
            _transactions = new Transactions();
            _nodeManager = new NodeManager();

            using (var node = _nodeManager.GetNode())
                if (!_blocks.Load())
                    await ConsoleWait(GetSlimChainAsync(node));

            _blockTransactions.Load();
            _transactions.Load();

            SyncSlimChain(_syncCancellationTokenSource.Token);

            SlimChainedBlock slimChainedBlock;
            lock (_syncLock)
                slimChainedBlock = _blocks.GetBlock(581180);

            var address = Bitcoin.Instance.Mainnet.CreateBitcoinAddress("38J8cCMJiERVKAN1W32g1CPVmniYymjJns");

            Console.WriteLine($"Assembling chain for address {address} starting on block 581180");
            await BuildCoinChain(node, slimChainedBlock.Hash, null, address);

            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();

            _syncCancellationTokenSource.Cancel();
            _blocks.Save();
            _blockTransactions.Save();
            _transactions.Save();

            Console.WriteLine("Good bye");

            Environment.Exit(1);
        }
        static Task GetSlimChainAsync(Node node)
        {
            return Task.Run(() => { _blocks.SetChain(node.GetSlimChain()); });
        }

        static Task SyncSlimChainAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                lock (_syncLock)
                {
                    using (var node = _nodeManager.GetNode())
                    using (_blocks.LockWrite())
                    {
                        var chain = _blocks.GetChain();
                        node.SynchronizeSlimChain(chain, cancellationToken: cancellationToken;
                        _blocks.SetChain(chain);
                    }
                }
            });
        }

        static Task SyncSlimChain(CancellationToken cancellationToken)
        {
            var syncTask = Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Synchronizing chain...");

                    await ConsoleWait(SyncSlimChainAsync(cancellationToken));

                    cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMinutes(10));

                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Sync cancelled!");
                        break;
                    }
                }
            }, cancellationToken);

            return syncTask;
        }

        static Task BuildCoinChain(Node node, uint256 blockHash, OutPoint outPoint, BitcoinAddress address)
        {
            return Task.Run(async () =>
            {
                if (_counter >= 5)
                    return;

                Dictionary<uint256, bool> txMap = new Dictionary<uint256, bool>();
                if (outPoint != null && outPoint.Hash != 0)
                {
                    var isTxKnown = _blockTransactions.FirstOrDefault(x => x.Value.ContainsKey(outPoint.Hash));
                    if (isTxKnown.Key != null)
                    {
                        blockHash = isTxKnown.Key;
                        // We already mapped this tx, return
                        if (_blockTransactions[blockHash][outPoint.Hash])
                            return;
                    }
                }

                // We're looking for a specific block
                Console.WriteLine($"Looking for transactions in block 0x{blockHash}");
                var blocks = node.GetBlocks(new List<uint256> { blockHash });
                var block = blocks.FirstOrDefault();

                if (block == null)
                {
                    Console.WriteLine($"ERROR: Block 0x{blockHash} not found!");
                    return;
                }

                bool found = false;

                foreach (var tx in block.Transactions)
                {
                    if (found)
                        txMap.Add(tx.GetHash(), false);
                    else
                    {
                        if (outPoint != null && outPoint.Hash != 0 && tx.GetHash() == outPoint.Hash)
                        {
                            found = true;

                            if (tx.IsCoinBase)
                            {
                                Console.WriteLine($"Coinbase reached at block {block.GetHash()}");
                            }
                            else
                            {
                                _counter++;
                                Console.WriteLine($"Found output, going deeper...");
                                var coin = tx.Outputs.AsCoins().ToList()[(int)outPoint.N];
                                await BuildCoinChain(node, block.Header.HashPrevBlock, coin.Outpoint, null);
                            }
                        }
                        else if (address != null)
                        {
                            var coins = tx.Outputs.AsCoins();
                            foreach (var coin in coins)
                            {
                                if (coin.TxOut.IsTo(address))
                                {
                                    found = true;

                                    if (tx.IsCoinBase)
                                    {
                                        Console.WriteLine($"Coinbase reached at block {block.GetHash()}");
                                    }
                                    else
                                    {
                                        _counter++;
                                        Console.WriteLine($"Found address, going deeper...");
                                        await BuildCoinChain(node, block.Header.HashPrevBlock, coin.Outpoint, null);
                                    }
                                }
                            }
                        }

                        if (found)
                        {
                            _transactions.Add(tx.GetHash(), tx);
                            txMap.AddOrReplace(tx.GetHash(), true);
                        }
                        else
                            txMap.TryAdd(tx.GetHash(), false);
                    }
                }

                if (!found)
                {
                    _counter++;
                    Console.WriteLine("Nothing found, going to previous block");
                    await BuildCoinChain(node, block.Header.HashPrevBlock, outPoint, null);
                }

                _blockTransactions.AddOrReplace(blockHash, txMap);
            });
        }
    }
}
