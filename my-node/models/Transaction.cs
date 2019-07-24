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
        public long SystemVersion { get; set; }

        //public virtual ICollection<TxIn> In { get; set; }
        //public virtual ICollection<TxOut> Out { get; set; }
        public virtual Block Block { get; set; }
    }
}
