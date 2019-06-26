using System;
using System.IO;
using NBitcoin;

namespace my_node.storage
{
	public class Blocks : StorageBase, SlimChain
	{
		private SlimChain _chain;

		public override string FileName => ".blocks";

		public Blocks(string basePath = null)
		{
			if (string.IsNullOrWhiteSpace(basePath))
				basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), BitcoinPath);

			BasePath = basePath;

			Directory.CreateDirectory(BasePath);
		}

		public override bool Load()
		{
			if (File.Exists(FullPath))
			{
				_chain = new SlimChain(Network.Main.GenesisHash);

				using(var stream = new FileStream(FullPath, FileMode.Open))
				_chain.Load(stream);

				return true;
			}

			return false;
		}

		public override void Save()
		{
			lock(_syncLock)
			{
				using(var stream = new FileStream(_slimChainFile, FileMode.Create))
				{
					_chain.Save(stream);
					Console.WriteLine($"Slimchain file saved to {stream.Name}");
				}
			}
		}
	}
}
