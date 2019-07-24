using my_node.formatters;
using my_node.storage;
using NBitcoin.Protocol;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using my_node.models;
using NBitcoin;
using static my_node.extensions.ConsoleExtensions;
using Block = my_node.models.Block;
using Transaction = my_node.models.Transaction;

namespace my_node
{
    class Program
    {
        private static Blocks _blocks;
        private static BlockTransactions _blockTransactions;
        private static NodeManager _nodeManager;
        private static readonly CancellationTokenSource _syncCancellationTokenSource = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            var context = new Context();

            RegisterFormatters.RegisterAll();
            _blocks = new Blocks(context);
            _blockTransactions = new BlockTransactions(context);
            //_transactions = new Transactions();
            _nodeManager = new NodeManager();

            if (!_blocks.Load())
                using (var node = _nodeManager.GetNode())
                    await ConsoleWait(GetSlimChainAsync(node));

            _blocks.SyncSlimChain(_nodeManager, _syncCancellationTokenSource.Token);
            await ConsoleWait(_blocks.WaitSync());

            _blockTransactions.Sync(_blocks, _nodeManager, _syncCancellationTokenSource.Token);

            //SlimChainedBlock slimChainedBlock;
            //lock (_syncLock)
            //    slimChainedBlock = _blocks.GetBlock(581180);

            //var address = Bitcoin.Instance.Mainnet.CreateBitcoinAddress("38J8cCMJiERVKAN1W32g1CPVmniYymjJns");

            //Console.WriteLine($"Assembling chain for address {address} starting on block 581180");

            //coinHistoryBuilder.Start();
            //await coinHistoryBuilder.BuildCoinHistory(new Search { BlockHash = slimChainedBlock.Hash, Address = address });

            Console.WriteLine("\rPress ENTER to exit");
            Console.ReadLine();

            _syncCancellationTokenSource.Cancel();
            //coinHistoryBuilder.Stop();
            //_blocks.Save();
            //_blockTransactions.Save();
            //_transactions.Save();

            Console.WriteLine("\rGood bye");

            Environment.Exit(1);
        }

        static Task GetSlimChainAsync(Node node)
        {
            Console.WriteLine("Downloading SlimChain.");
            return Task.Run(() => { _blocks.SetChain(node.GetSlimChain()); });
        }
    }
}
