using ZeroFormatter.Formatters;
using NBitcoin;

namespace my_node.formatters
{
    public static class RegisterFormatters
    {
        public static void RegisterAll()
        {
            Formatter<DefaultResolver, uint256>.Register(new Uint256Formatter());
        }
    }
}