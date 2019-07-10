using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Threading;

namespace my_node
{
    public class NodeManager
    {
        private readonly AddressManager _addressManager;

        public NodeManager()
        {
            _addressManager = new AddressManager();

            var dnsSeed = new DNSSeedData("sipa", "seed.bitcoin.sipa.be");
            var ips = dnsSeed.GetAddressNodesAsync(8333).Result;
            foreach (var ip in ips)
                _addressManager.AddAsync(ip).Wait();
        }

        public Node GetNode()
        {
            Console.WriteLine("\rConnecting to peer");
            var node = Node.Connect(Network.Main, _addressManager);
            node.Disconnected += (thisNode) =>
            {
                if (thisNode.DisconnectReason.Exception == null)
                    return;

                Console.WriteLine($"\rDisconnected from: {thisNode.Peer.Endpoint.Address}:{thisNode.Peer.Endpoint.Port}. Reason: {thisNode.DisconnectReason.Reason}");
                node = Node.Connect(Network.Main, _addressManager);
            };

            while (node.State != NodeState.Connected)
                Thread.Sleep(100);

            Console.WriteLine($"\rConnected to: {node.Peer.Endpoint.Address}:{node.Peer.Endpoint.Port}");

            return node;
        }
    }
}
