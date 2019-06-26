using System.IO;
namespace my_node.storage
{
    public abstract class StorageBase
    {
        public string BasePath { get; internal set; }
        public string FullPath => Path.Combine(BasePath, FileName);
        public string BitcoinPath = ".bitcoin";

        public abstract string FileName { get; }

        public abstract bool Load();
        public abstract void Save();
    }
}
