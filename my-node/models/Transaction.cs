using NBitcoin;
using System.Collections.Generic;

namespace my_node.models
{
    public class Transaction
    {
        public string Hash { get; set; }
        public string BlockHash { get; set; }
        public uint Version { get; set; }
        public byte InCount { get; set; }
        public byte OutCount { get; set; }
        public uint LockTime { get; set; }

        public virtual List<TxIn> In { get; set; }
        public virtual List<TxOut> Out { get; set; }
    }
}
