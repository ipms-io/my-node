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
            Console.WriteLine("Connecting");
            var state = NodeState.Offline;
            var connectedFinal = false;
            var node = Node.Connect(Network.Main, _addressManager);
            node.StateChanged += (thisNode, oldState) =>
            {
                if (state == NodeState.Connected && oldState == NodeState.HandShaked)
                    connectedFinal = true;

                state = oldState;
                Console.WriteLine($"Peer {thisNode.Peer.Endpoint.Address}:{thisNode.Peer.Endpoint.Port} stated changed to {oldState.ToString()}");
            };
            node.Disconnected += (thisNode) =>
            {
                Console.WriteLine($"Disconnected from: {thisNode.Peer.Endpoint.Address}:{thisNode.Peer.Endpoint.Port}. Reason: {thisNode.DisconnectReason.Reason}");
            };

            while (node.State != NodeState.Connected)
                Thread.Sleep(100);

            Console.WriteLine($"Connected to: {node.Peer.Endpoint.Address}:{node.Peer.Endpoint.Port}");

            return node;
        }
    }
}
