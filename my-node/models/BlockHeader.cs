using NBitcoin;

namespace my_node.models
{
    public class BlockHeader
    {
        public string BlockHash { get; set; }
        public string HashMerkleRoot { get; set; }
        public uint Time { get; set; }
        public uint Bits { get; set; }
        public int Version { get; set; }
        public uint Nonce { get; set; }

        public BlockHeader()
        { }
        public BlockHeader(NBitcoin.BlockHeader blockHeader)
        {
            HashMerkleRoot = blockHeader.HashMerkleRoot.ToString();
            Time = (uint)blockHeader.BlockTime.Offset.TotalSeconds;
            Bits = blockHeader.Bits.ToCompact();
            Version = blockHeader.Version;
            Nonce = blockHeader.Nonce;
        }
    }
}
