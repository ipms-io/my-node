using my_node.formatters;
using my_node.models;
using my_node.storage;
using NBitcoin;
using NBitcoin.Protocol;
using System;
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
            SlimChainedBlock slimChainedBlock;
            lock (_syncLock)
                slimChainedBlock = _blocks.GetBlock(581180);

            var address = Bitcoin.Instance.Mainnet.CreateBitcoinAddress("38J8cCMJiERVKAN1W32g1CPVmniYymjJns");

            Console.WriteLine($"Assembling chain for address {address} starting on block 581180");

            coinHistoryBuilder.Start();
            await coinHistoryBuilder.BuildCoinHistory(new Search { BlockHash = slimChainedBlock.Hash, Address = address });

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
                    Console.WriteLine($"ERROR: {ex.ToString()}");
                    SyncSlimChain(cancellationToken).Wait();
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
    }
}
