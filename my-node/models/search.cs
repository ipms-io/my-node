using NBitcoin;

namespace my_node.models
{
    public class Search
    {
        public OutPoint OutPoint { get; set; }
        public uint256 BlockHash { get; set; }
        public BitcoinAddress Address { get; set; }
    }
}
