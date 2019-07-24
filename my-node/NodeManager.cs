using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Protocol;
using System.Threading;

namespace my_node
{
    public class NodeManager
    {
        private readonly AddressManager _addressManager;
        private readonly Dictionary<string, string> _dnsSeedList;

        public NodeManager()
        {
            Console.WriteLine("Initializing node manager;");

            _dnsSeedList = new Dictionary<string, string>
            {
                { "bitcoin.sipa.be"         , "seed.bitcoin.sipa.be"          }, // Pieter Wuille
                { "bluematt.me"             , "dnsseed.bluematt.me"           }, // Matt Corallo
                { "bitcoinstats.com"        , "seed.bitcoinstats.com"         }, // Christian Decker
                { "bitcoin.jonasschnelli.ch", "seed.bitcoin.jonasschnelli.ch" }, // Jonas Schnelli
                { "btc.petertodd.org"       , "seed.btc.petertodd.org"        }, // Peter Todd
                { "bitcoin.sprovoost.nl"    , "seed.bitcoin.sprovoost.nl"     }, // Sjors Provoost
                { "emzy.de"                 , "dnsseed.emzy.de"               }  // Stephan Oeste
            };

            _addressManager = new AddressManager();

            foreach (var rawDNSSeed in _dnsSeedList)
            {
                try
                {
                    var dnsSeed = new DNSSeedData(rawDNSSeed.Key, rawDNSSeed.Value);
                    var ips = dnsSeed.GetAddressNodesAsync(8333).Result;
                    foreach (var ip in ips)
                        _addressManager.AddAsync(ip).Wait();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR: {ex.Message} | {rawDNSSeed.Key}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

        }

        public Node GetNode(int retries = 0)
        {
            try
            {
                var node = Node.Connect(Network.Main, _addressManager);
                //node.VersionHandshake();

                while (node.State != NodeState.Connected)
                    Thread.Sleep(100);

                return node;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\rERROR: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.White;

                if (retries < 5)
                    return GetNode(++retries);

                throw;
            }
        }
    }
}
