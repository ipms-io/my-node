using System;
using System.Numerics;
using NBitcoin;

namespace my_node.models
{
    public class TxOut
    {
        public Guid Id { get; set; }
        public string TxHash { get; set; }
        public byte[] ScriptPubKey { get; set; }
        public ulong Value { get; set; }
    }
}
