using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroFormatter.Formatters;

namespace my_node
{
    class Program
    {
        private static SlimChain _chain;
        private static BlockTransaction _blockTx;
        private static Dictionary<uint256, Transaction> _txMap;
        private static readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bitcoin");
        private static readonly string _slimChainFile = Path.Combine(_path, ".slimChain");
        private static readonly string _blockTransactionsFile = Path.Combine(_path, ".blockTransactions");
        private static readonly object _syncLock = new object();
        private static readonly CancellationTokenSource _syncCancellationTokenSource = new CancellationTokenSource();

        private static int _counter = 0;

        static async Task Main(string[] args)
        {
            Formatter<DefaultResolver, uint256>.Register(new Uint256Formatter());
            _txMap = new Dictionary<uint256, Transaction>();

            var addressManager = new AddressManager();

            var dnsSeed = new DNSSeedData("sipa", "seed.bitcoin.sipa.be");
            var ips = await dnsSeed.GetAddressNodesAsync(8333);
            foreach (var ip in ips)
                await addressManager.AddAsync(ip);

            Console.WriteLine("Connecting");
            var node = Node.Connect(Network.Main, addressManager);
            while (!node.IsConnected)
                Thread.Sleep(100);

            Console.WriteLine($"Connected to: {node.Peer.Endpoint.Address}:{node.Peer.Endpoint.Port}");

            await LoadSlimChainFile(node);
            LoadBlockTransactionFile();

            //SyncSlimChain(node, _syncCancellationTokenSource.Token);

            SlimChainedBlock slimChainedBlock;
            lock (_syncLock)
                slimChainedBlock = _chain.GetBlock(581180);

            var address = Bitcoin.Instance.Mainnet.CreateBitcoinAddress("38J8cCMJiERVKAN1W32g1CPVmniYymjJns");

            Console.WriteLine($"Assembling chain for address {address} starting on block 581180");
            await BuildCoinChain(node, slimChainedBlock.Hash, null, address);

            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();

            node.DisconnectAsync("stopping client");

            _syncCancellationTokenSource.Cancel();
            SaveSlimChainFile();
            SaveBlockTransactionsFile();

            Console.WriteLine("Good bye");

            Environment.Exit(1);
        }

        private static void LoadBlockTransactionFile()
        {
            _blockTx = new BlockTransaction();

            if (File.Exists(_blockTransactionsFile))
            {
                using (var stream = new FileStream(_blockTransactionsFile, FileMode.Open))
                    _blockTx.Load(stream);
            }
        }

        private static async Task LoadSlimChainFile(Node node)
        {
            Directory.CreateDirectory(_path);
            if (File.Exists(_slimChainFile))
            {
                _chain = new SlimChain(Network.Main.GenesisHash);

                using (var stream = new FileStream(_slimChainFile, FileMode.Open))
                    _chain.Load(stream);
            }
            else
            {
                await FirstSync(node);
            }
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
                    var isTxKnown = _blockTx.FirstOrDefault(x => x.Value.ContainsKey(outPoint.Hash));
                    if (isTxKnown.Key != null)
                    {
                        blockHash = isTxKnown.Key;
                        // We already mapped this tx, return
                        if (_blockTx[blockHash][outPoint.Hash])
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
                            _txMap.Add(tx.GetHash(), tx);
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

                _blockTx.AddOrReplace(blockHash, txMap);
            });
        }

        static Task GetSlimChainAsync(Node node)
        {
            return Task.Run(() => { _chain = node.GetSlimChain(); });
        }

        static Task SyncSlimChainAsync(Node node, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                lock (_syncLock)
                {
                    node.SynchronizeSlimChain(_chain, cancellationToken: cancellationToken);
                }
            });
        }

        static async Task FirstSync(Node node)
        {
            Console.WriteLine("Downloading chain headers for the first time.");
            Console.WriteLine("It may take a few minutes to finish. Be patient...");

            await ConsoleWait(GetSlimChainAsync(node));

            Console.WriteLine("Chain download complete.");

            SaveSlimChainFile();
        }

        static void SaveSlimChainFile()
        {
            lock (_syncLock)
            {
                using (var stream = new FileStream(_slimChainFile, FileMode.Create))
                {
                    _chain.Save(stream);
                    Console.WriteLine($"Slimchain file saved to {stream.Name}");
                }
            }
        }

        static void SaveBlockTransactionsFile()
        {
            using (var stream = new FileStream(_blockTransactionsFile, FileMode.Create))
            {
                _blockTx.Save(stream);
                Console.WriteLine($"BlockTransaction file saved to {stream.Name}");
            }
        }

        static Task SyncSlimChain(Node node, CancellationToken cancellationToken)
        {
            var syncTask = Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Synchronizing chain...");

                    await ConsoleWait(SyncSlimChainAsync(node, cancellationToken));

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

        static Task ConsoleWait(Task task)
        {
            int count = 0;
            while (!task.IsCompleted)
            {
                switch (count++ % 4)
                {
                    case 1:
                        Console.Write("\rWait");
                        break;
                    case 2:
                        Console.Write("\rWait.");
                        break;
                    case 3:
                        Console.Write("\rWait..");
                        break;
                    case 4:
                        Console.Write("\rWait...");
                        break;
                }
                Thread.Sleep(100);
            }

            Console.Write("\r");

            return task;
        }
    }
}
