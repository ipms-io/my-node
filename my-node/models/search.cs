using NBitcoin;
using System.Collections.Generic;

namespace my_node.models
{
    public class Search
    {
        public List<OutPoint> OutPoints { get; set; }
        public uint256 BlockHash { get; set; }
        public Transaction Transaction { get; set; }

        public Search()
        {
            OutPoints = new List<OutPoint>();
        }
    }
}
