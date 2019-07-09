using System;
using System.IO;
using System.Threading;
using NBitcoin;
using my_node.extensions;

namespace my_node.storage
{
    public class Blocks : StorageBase
    {
        private readonly ReaderWriterLock _lock = new ReaderWriterLock();
        private SlimChain _chain;

        public override string FileName => ".blocks";

        public Blocks(string basePath = null)
            : base(basePath)
        {
            _chain = new SlimChain(Network.Main.GenesisHash);
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

                Console.WriteLine($"Slimchain file saved to {stream.Name}");
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
