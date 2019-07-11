using System.Collections.Generic;

namespace my_node.models
{
    public class Block
    {
        public string Hash { get; set; }
        public int Height { get; set; }
        public int TransactionCount { get; set; }
        public virtual BlockHeader BlockHeader { get; set; }
        public virtual List<Transaction> Transactions { get; set; }
    }
}
