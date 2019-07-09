using my_node.formatters;
using my_node.models;
using my_node.storage;
using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static my_node.extensions.ConsoleExtensions;

namespace my_node
{
    class Program
    {
        private static Blocks _blocks;
        private static BlockTransactions _blockTransactions;
        private static Transactions _transactions;
        private static NodeManager _nodeManager;
        private static readonly object _syncLock = new object();
        private static readonly CancellationTokenSource _syncCancellationTokenSource = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            RegisterFormatters.RegisterAll();
            _blocks = new Blocks();
            _blockTransactions = new BlockTransactions();
            _transactions = new Transactions();
            _nodeManager = new NodeManager();
            var coinHistoryBuilder = new CoinHistoryBuilder(_blocks, _blockTransactions, _transactions, _nodeManager);

            using (var node = _nodeManager.GetNode())
                if (!_blocks.Load())
                    await ConsoleWait(GetSlimChainAsync(node));

            _blockTransactions.Load();
            _transactions.Load();

            SyncSlimChain(_syncCancellationTokenSource.Token);
            Thread.Sleep(10000);

            CheckLastSyncedBlock(_syncCancellationTokenSource.Token);

            //SlimChainedBlock slimChainedBlock;
            //lock (_syncLock)
            //    slimChainedBlock = _blocks.GetBlock(581180);

            //var address = Bitcoin.Instance.Mainnet.CreateBitcoinAddress("38J8cCMJiERVKAN1W32g1CPVmniYymjJns");

            //Console.WriteLine($"Assembling chain for address {address} starting on block 581180");

            //coinHistoryBuilder.Start();
            //await coinHistoryBuilder.BuildCoinHistory(new Search { BlockHash = slimChainedBlock.Hash, Address = address });

            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();

            _syncCancellationTokenSource.Cancel();
            coinHistoryBuilder.Stop();
            _blocks.Save();
            _blockTransactions.Save();
            _transactions.Save();

            Console.WriteLine("Good bye");

            Environment.Exit(1);
        }

        static Task CheckLastSyncedBlock(CancellationToken cancelationToken)
        {
            return Task.Run(() =>
            {
                var tip = _blocks.GetTip();
                var blocksToSync = new List<uint256>();

                while (!_blockTransactions.ContainsKey((tip.Hash)))
                {
                    blocksToSync.Add(tip.Hash);

                    if (tip.Previous == null)
                        break;

                    tip = _blocks.GetBlock(tip.Previous);
                }

                Console.WriteLine($"Getting {blocksToSync.Count} blocks");
                var node = _nodeManager.GetNode();
                var blocks = node.GetBlocks(blocksToSync);

                var semaphore = new SemaphoreSlim(10);
                var count = 1;
                foreach (var block in blocks)
                {
                    Console.Write($"\r{Math.Round((decimal)count++ / blocksToSync.Count, 5, MidpointRounding.AwayFromZero)}%");
                    semaphore.Wait(cancelationToken);
                    Task.Run(() =>
                    {
                        try
                        {
                            SaveTransactionsFromBlock(block, semaphore);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"ERROR: {e}");
                            node = _nodeManager.GetNode();
                        }

                    }, cancelationToken);
                }

            }, cancelationToken);
        }

        static void SaveTransactionsFromBlock(Block block, SemaphoreSlim semaphore)
        {
            var transactions = new Dictionary<uint256, bool>();

            foreach (var blockTransaction in block.Transactions)
                transactions.Add(blockTransaction.GetHash(), false);

            _blockTransactions.Add(block.GetHash(), transactions);

            semaphore.Release();
        }

        static Task GetSlimChainAsync(Node node)
        {
            return Task.Run(() => { _blocks.SetChain(node.GetSlimChain()); });
        }

        static Task SyncSlimChainAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    lock (_syncLock)
                    {
                        using (var node = _nodeManager.GetNode())
                        using (_blocks.LockWrite())
                        {
                            var chain = _blocks.GetChain();
                            node.SynchronizeSlimChain(chain, cancellationToken: cancellationToken);
                            _blocks.SetChain(chain);
                        }

                        _blocks.Save();
                        _blockTransactions.Save();
                        _transactions.Save();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex}");
                    SyncSlimChain(cancellationToken).Wait(cancellationToken);
                }
            }, cancellationToken);
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
    }
}
