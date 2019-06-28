using NBitcoin;
using System;
using ZeroFormatter;
using ZeroFormatter.Formatters;
using ZeroFormatter.Internal;

namespace my_node.formatters
{
    public class TransactionFormatter : Formatter<DefaultResolver, Transaction>
    {
        public override Transaction Deserialize(ref byte[] bytes, int offset, DirtyTracker tracker, out int byteSize)
        {
            var length = bytes.Length - offset;
            var version = ZeroFormatterSerializer.Deserialize<uint>(bytes);
            var newBytes = new byte[length];
            Array.Copy(bytes, offset, newBytes, 0, length);
            var transaction = Transaction.Load(newBytes, version, Network.Main);
            byteSize = transaction.GetSerializedSize(transaction.Version, SerializationType.Disk);

            return transaction;
        }

        public override int? GetLength()
        {
            return null;
        }

        public override int Serialize(ref byte[] bytes, int offset, Transaction value)
        {
            return BinaryUtil.WriteBytes(ref bytes, offset, value.ToBytes(value.Version));
        }
    }
}
