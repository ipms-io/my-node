using NBitcoin;

namespace my_node.models
{
    public class TxIn
    {
        public string TxHash { get; set; }
        public string PrevHash { get; set; }
        public byte[] PrevScript { get; set; }
        public uint PrevN { get; set; }
        public byte[] ScriptSig { get; set; }
        public uint Sequence { get; set; }

        public virtual Transaction PrevTransaction { get; set; }
    }
}
