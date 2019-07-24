using Microsoft.EntityFrameworkCore;
using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static my_node.extensions.ConsoleExtensions;
using BlockHeader = my_node.models.BlockHeader;
using Transaction = my_node.models.Transaction;

namespace my_node.storage
{
    public class BlockTransactions
    {
        private readonly ConcurrentQueue<Context> _contexts;
        private readonly Context _context;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _syncThreads;

        public BlockTransactions(Context context)
        {
            _syncThreads = 10;
            _context = context;

            _contexts = new ConcurrentQueue<Context>();
            _semaphore = new SemaphoreSlim(_syncThreads);
        }

        public void Sync(Blocks blocks, NodeManager nodeManager, CancellationToken cancelationToken)
        {
            Task.Run(async () =>
            {
                var blocksDone = 0L;
                var timeStart = DateTime.UtcNow.ToUnixTimestamp();

                var blocksToSync = await _context.Blocks
                                                 .AsNoTracking()
                                                 .OrderBy(b => b.Height)
                                                 .Where(b => b.TransactionCount.Equals(0))
                                                 .Select(b => uint256.Parse(b.Hash))
                                                 .ToListAsync(cancelationToken);

                Console.WriteLine($"\rGetting {blocksToSync.Count} blocks");
                Console.WriteLine($"\rInitializing {(int)(_syncThreads * 1.5)} nodes.");
                var nodes = new ConcurrentQueue<Node>();
                await ConsoleWait(Task.Run(() =>
                {
                    for (var i = 0; i < _syncThreads * 1.5; i++)
                        nodes.Enqueue(nodeManager.GetNode());

                    for (var i = 0; i < _syncThreads; i++)
                        _contexts.Enqueue(new Context());
                }, cancelationToken));

                var syncQueue = new ConcurrentQueue<uint256>(blocksToSync);

                var count = 1;
                while (!syncQueue.IsEmpty)
                {
                    await blocks.WaitSync();

                    var currBlocksDone = blocksDone;
                    var timeElapsed = (DateTime.UtcNow.ToUnixTimestamp() - timeStart);
                    var bps = currBlocksDone / timeElapsed;
                    var remainingText = string.Empty;
                    if (bps > 0)
                    {
                        var remaining = (blocksToSync.Count - currBlocksDone) / bps;
                        if (remaining > 60 * 60 * 24)
                            remainingText = $"{remaining / 60 / 60 / 24}d remaining.";
                        else if (remaining < 60 * 60 * 24)
                            remainingText = $"{remaining / 60 / 60}h remaining.";
                        else if (remaining < 60 * 60)
                            remainingText = $"{remaining / 60}m remaining.";
                        else if (remaining < 60)
                            remainingText = $"{remaining}s remaining.";
                    }

                    Console.Write($"\rSyncing: {Math.Round((decimal)count++ / blocksToSync.Count, 5, MidpointRounding.AwayFromZero)}% @{bps}bps | {remainingText}");
                    _semaphore.Wait(cancelationToken);
                    ThreadPool.QueueUserWorkItem(async _ =>
                    {
                        _contexts.TryDequeue(out var context);
                        uint256 blockHash = null;
                        nodes.TryDequeue(out var node);
                        try
                        {
                            Interlocked.Increment(ref blocksDone);
                            if (!syncQueue.TryDequeue(out blockHash))
                                return;

                            var dbBlockTask = context.Blocks.FirstOrDefaultAsync(b => b.Hash.Equals(blockHash.ToString()), cancelationToken);
                            var nodeBlock = node.GetBlocks(new List<uint256> { blockHash }).First();
                            var dbBlock = await dbBlockTask;

                            //if (dbBlock.TransactionCount == nodeBlock.Transactions.Count)
                            //{
                            //    Console.ForegroundColor = ConsoleColor.DarkYellow;
                            //    Console.WriteLine($"\rBlock {dbBlock.Hash} already existed in database.");
                            //    Console.ForegroundColor = ConsoleColor.White;
                            //    return;
                            //}

                            dbBlock.Transactions = new List<Transaction>(nodeBlock.Transactions.Count);
                            foreach (var blockTransaction in nodeBlock.Transactions)
                            {
                                var tx = new Transaction
                                {
                                    BlockHash = dbBlock.Hash,
                                    Hash = blockTransaction.GetHash().ToString()
                                };

                                //if (context.Transactions.SingleOrDefault(t => t.Hash.Equals(tx.Hash)) != null)
                                //{
                                //    Console.ForegroundColor = ConsoleColor.DarkYellow;
                                //    Console.WriteLine($"\rTransaction {tx.Hash} already existed in database.");
                                //    Console.ForegroundColor = ConsoleColor.White;
                                //    continue;
                                //}

                                dbBlock.Transactions.Add(tx);
                            }

                            dbBlock.TransactionCount = dbBlock.Transactions.Count;
                            //if (context.BlockHeaders.FirstOrDefault(bh => bh.BlockHash.Equals(dbBlock.Hash)) == null)
                                dbBlock.BlockHeader = new BlockHeader(nodeBlock.Header);
                            //else
                            //{
                            //    Console.ForegroundColor = ConsoleColor.DarkYellow;
                            //    Console.WriteLine($"\rBlock Header {dbBlock.Hash} already existed in database.");
                            //    Console.ForegroundColor = ConsoleColor.White;
                            //}

                            context.Blocks.Update(dbBlock);
                            await context.SaveChangesAsync(cancelationToken);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Decrement(ref blocksDone);


                            if (!cancelationToken.IsCancellationRequested)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\rERROR: {ex.Message}");

                                if (ex.InnerException != null)
                                    Console.WriteLine($"\rInner Exception: {ex.InnerException.Message}");

                                Console.ForegroundColor = ConsoleColor.White;
                                syncQueue.Enqueue(blockHash);
                                if (ex.Source == "NBitcoin" && ex.TargetSite.DeclaringType.Name == "Node")
                                    node = nodeManager.GetNode();
                            }
                        }
                        finally
                        {
                            if (!cancelationToken.IsCancellationRequested)
                            {
                                nodes.Enqueue(node);
                                _contexts.Enqueue(context);
                            }

                            _semaphore.Release();
                        }

                    }, cancelationToken);
                }
            }, cancelationToken);
        }
    }
}
