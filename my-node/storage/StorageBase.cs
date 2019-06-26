using System;
using System.IO;
namespace my_node.storage
{
    public abstract class StorageBase
    {
        public string BasePath { get; internal set; }
        public string FullPath => Path.Combine(BasePath, FileName);
        public string BitcoinPath = ".bitcoin";

        public abstract string FileName { get; }

        public StorageBase(string basePath = null)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), BitcoinPath);

            BasePath = basePath;

            Directory.CreateDirectory(BasePath);
        }

        public abstract bool Load();
        public abstract void Save();
    }
}
