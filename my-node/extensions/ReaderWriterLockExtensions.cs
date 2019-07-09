using System;
using System.Threading;

namespace my_node.extensions
{
    public enum ReaderWriterLockType
    {
        Read,
        Write
    }

    public class ReaderWriterLockObject : IDisposable
    {
        private ReaderWriterLock _rwl;
        private ReaderWriterLockType _type;

        public ReaderWriterLockObject(ReaderWriterLock rwl, ReaderWriterLockType type)
        {
            _rwl = rwl;
            _type = type;
            if (_type == ReaderWriterLockType.Read)
                _rwl.AcquireReaderLock(TimeSpan.FromMinutes(1));
            else if (_type == ReaderWriterLockType.Write)
                _rwl.AcquireWriterLock(TimeSpan.FromMinutes(1));
        }

        public void Dispose()
        {
            if (_type == ReaderWriterLockType.Read)
                _rwl.ReleaseReaderLock();
            else if (_type == ReaderWriterLockType.Write)
                _rwl.ReleaseWriterLock();
        }
    }

    public static class ReaderWriterLockExtensions
    {
        public static ReaderWriterLockObject LockRead(this ReaderWriterLock rwl)
        {
            return new ReaderWriterLockObject(rwl, ReaderWriterLockType.Read);
        }

        public static ReaderWriterLockObject LockWrite(this ReaderWriterLock rwl)
        {
            return new ReaderWriterLockObject(rwl, ReaderWriterLockType.Write);
        }
    }
}
