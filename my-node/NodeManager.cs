using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Threading;

namespace my_node
{
    public class NodeManager
    {
        private AddressManager _addressManager;

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
            Console.WriteLine("Connecting");
            var node = Node.Connect(Network.Main, _addressManager);
            node.Disconnected += (thisNode) =>
            {
                Console.WriteLine($"Disconnected from: {thisNode.Peer.Endpoint.Address}:{thisNode.Peer.Endpoint.Port}. Reason: {thisNode.DisconnectReason}");
                thisNode.Dispose();
            };
            while (!node.IsConnected)
                Thread.Sleep(100);

            Console.WriteLine($"Connected to: {node.Peer.Endpoint.Address}:{node.Peer.Endpoint.Port}");

            return node;
        }
    }
}
