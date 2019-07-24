using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using NBitcoin;
using my_node.extensions;
using my_node.models;
using NBitcoin.Protocol;
using static my_node.extensions.ConsoleExtensions;
using Block = my_node.models.Block;

namespace my_node.storage
{
    public delegate void SyncFinishedHandler(object source, EventArgs e);
    public delegate void SyncCatastrophicErrorHandler(object source, EventArgs e);

    public class Blocks : StorageBase
    {
        private readonly Context _context;
        private readonly ReaderWriterLock _lock = new ReaderWriterLock();

        private SlimChain _chain;
        private bool _syncing = true;

        public override string FileName => ".blocks";

        public event SyncFinishedHandler OnSyncFinished;
        public event SyncCatastrophicErrorHandler OnSyncCatastrophicError;

        public Blocks(Context context)
        {
            _context = context;
            _chain = new SlimChain(Network.Main.GenesisHash);
        }

        public Task WaitSync()
        {
            while (_syncing)
                Thread.Yield();

            return Task.CompletedTask;
        }

        public Task SyncSlimChainAsync(NodeManager nodeManager, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    _syncing = true;

                    using (var node = nodeManager.GetNode())
                    using (_lock.LockWrite())
                        node.SynchronizeSlimChain(_chain, cancellationToken: cancellationToken);

                    Save();

                    var tip = await GetDbTipAsync(cancellationToken) ?? new Block();

                    for (var h = _chain.Height; h > tip.Height; h--)
                    {
                        var block = _chain.GetBlock(h);
                        await _context.Blocks.AddAsync(new Block
                        {
                            Hash = block.Hash.ToString(),
                            Height = block.Height
                        }, cancellationToken);
                    }

                    await _context.SaveChangesAsync(cancellationToken);

                    OnSyncFinished?.Invoke(this, new EventArgs());
                    // TODO: Sign event on transactions

                    _syncing = false;
                }
                catch (Exception ex)
                {
                     Console.WriteLine($"\rERROR: {ex}");
                    OnSyncCatastrophicError?.Invoke(this, EventArgs.Empty);
                }
            }, cancellationToken);
        }

        public void SyncSlimChain(NodeManager nodeManager, CancellationToken cancellationToken)
        {
            OnSyncCatastrophicError = null;
            OnSyncCatastrophicError += (source, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"\rBlocks: Recovering from error. Initializing again.");
                Console.ForegroundColor = ConsoleColor.White;
                SyncSlimChain(nodeManager, cancellationToken);
            };

            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\rSynchronizing chain...");

                    await ConsoleWait(SyncSlimChainAsync(nodeManager, cancellationToken));

                    cancellationToken.WaitHandle.WaitOne(TimeSpan.FromHours(25));

                    if (!cancellationToken.IsCancellationRequested)
                        continue;

                    Console.WriteLine("\rSync cancelled!");
                    break;
                }
            }, cancellationToken);
        }

        public override bool Load()
        {
            if (!File.Exists(FullPath))
                return false;

            using (var stream = new FileStream(FullPath, FileMode.Open))
            using (_lock.LockWrite())
                _chain.Load(stream);

            return true;
        }

        public override void Save()
        {
            using (var stream = new FileStream(FullPath, FileMode.Create))
            {
                using (_lock.LockRead())
                    _chain.Save(stream);

                Console.WriteLine($"\rSlimchain file saved to {stream.Name}");
            }
        }
        public void SetChain(SlimChain slimChain)
        {
            _chain = slimChain;
        }

        public SlimChainedBlock GetBlock(int heigth)
        {
            SlimChainedBlock block = null;
            using (_lock.LockRead())
                block = _chain.GetBlock(heigth);

            return block;
        }
        public SlimChainedBlock GetBlock(uint256 hash)
        {
            SlimChainedBlock block = null;
            using (_lock.LockRead())
                block = _chain.GetBlock(hash);

            return block;
        }

        public ReaderWriterLockObject LockWrite()
        {
            return _lock.LockWrite();
        }

        public SlimChain GetChain()
        {
            SlimChain chain = null;
            using (_lock.LockRead())
                chain = _chain;

            return chain;
        }

        //public int GetChainHeight()
        //{
        //    var heigth = 0;
        //    using (_lock.LockRead())
        //        heigth = _chain.Height;

        //    return heigth;
        //}

        public SlimChainedBlock GetTip()
        {
            SlimChainedBlock tip = null;
            using (_lock.LockRead())
                tip = _chain.TipBlock;

            return tip;
        }

        public Task<Block> GetDbTipAsync(CancellationToken? cancellationToken = null)
        {
            return _context.Blocks
                .AsNoTracking()
                .OrderBy(b => b.Height)
                .LastOrDefaultAsync(cancellationToken ?? CancellationToken.None);
        }

        internal uint256 GetPreviousBlockHash(uint256 blockHash)
        {
            uint256 prevBlockHash = null;
            using (_lock.LockRead())
            {
                var block = _chain.GetBlock(blockHash);
                prevBlockHash = block?.Previous;
            }

            return prevBlockHash;
        }
    }
}
