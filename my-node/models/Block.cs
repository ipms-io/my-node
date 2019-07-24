using System.Collections.Generic;

namespace my_node.models
{
    public class Block
    {
        public string Hash { get; set; }
        public int Height { get; set; }
        public int TransactionCount { get; set; }
        public long SystemVersion { get; set; }

        public virtual BlockHeader BlockHeader { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }
    }
}
