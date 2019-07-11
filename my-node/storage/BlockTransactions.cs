using Microsoft.EntityFrameworkCore;
using my_node.extensions;
using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using my_node.models;
using Block = my_node.models.Block;
using Transaction = my_node.models.Transaction;
using static my_node.extensions.ConsoleExtensions;
using BlockHeader = my_node.models.BlockHeader;

namespace my_node.storage
{
    public class BlockTransactions
    {
        private readonly Context _context;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _syncThreads;

        public BlockTransactions(Context context)
        {
            _context = context;
            _syncThreads = 1;
            _semaphore = new SemaphoreSlim(_syncThreads);
        }

        public void Sync(Blocks blocks, NodeManager nodeManager, CancellationToken cancelationToken)
        {
            Task.Run(async () =>
            {
                var tip = await blocks.GetDbTipAsync();
                var blocksToSync = await _context.Blocks
                                                 .AsNoTracking()
                                                 .OrderBy(b => b.Height)
                                                 .Where(b => b.TransactionCount.Equals(0))
                                                 .ToListAsync(cancelationToken);

                Console.WriteLine($"\rGetting {blocksToSync.Count} blocks");
                Console.WriteLine($"\rInitializing {_syncThreads * 1.5} nodes.");
                var nodes = new ConcurrentQueue<Node>();
                await ConsoleWait(Task.Run(() =>
                {
                    for (var i = 0; i < _syncThreads * 1.5; i++)
                        nodes.Enqueue(nodeManager.GetNode());
                }, cancelationToken));
                
                var syncQueue = new ConcurrentQueue<Block>(blocksToSync);

                var count = 1;
                while (!syncQueue.IsEmpty)
                {
                    await blocks.WaitSync();

                    Console.Write(
                        $"\rSyncing: {Math.Round((decimal)count++ / blocksToSync.Count, 5, MidpointRounding.AwayFromZero)}%");
                    _semaphore.Wait(cancelationToken);
                    var count1 = count;
                    ThreadPool.QueueUserWorkItem(async _ =>
                    {
                        Block block = null;
                        nodes.TryDequeue(out var node);
                        try
                        {
                            if (!syncQueue.TryDequeue(out block))
                                return;

                            var nodeBlock = node.GetBlocks(new List<uint256> { uint256.Parse(block.Hash) }).First();
                            block.Transactions = new List<Transaction>(nodeBlock.Transactions.Count);

                            foreach (var blockTransaction in nodeBlock.Transactions)
                            {
                                var tx = new Transaction
                                {
                                    Hash = blockTransaction.GetHash().ToString(),
                                    BlockHash = block.Hash
                                };
                                await _context.Transactions.AddAsync(tx, cancelationToken);
                            }

                            block.TransactionCount = block.Transactions.Count;
                            block.BlockHeader = new BlockHeader(nodeBlock.Header);
                            _context.Blocks.Update(block);
                            await _context.SaveChangesAsync(cancelationToken);
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\rERROR: {e.Message}");
                            Console.ForegroundColor = ConsoleColor.White;
                            syncQueue.Enqueue(block);
                            if (e.Source == "NBitcoin" && e.TargetSite.DeclaringType.Name == "Node")
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
    }
}
