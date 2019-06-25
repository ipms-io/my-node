using NBitcoin;
using ZeroFormatter;
using ZeroFormatter.Formatters;
using ZeroFormatter.Internal;

namespace my_node
{
    public class Uint256Formatter : Formatter<DefaultResolver, uint256>
    {
        public override uint256 Deserialize(ref byte[] bytes, int offset, DirtyTracker tracker, out int byteSize)
        {
            byteSize = 32;
            return new uint256(BinaryUtil.ReadBytes(ref bytes, offset, 32));
        }

        public override int? GetLength()
        {
            return 32;
        }

        public override int Serialize(ref byte[] bytes, int offset, uint256 value)
        {
            return BinaryUtil.WriteBytes(ref bytes, offset, value.ToBytes());
        }
    }
}
