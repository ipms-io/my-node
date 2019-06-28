using System;
using System.Threading;

namespace my_node.extensions
{
    [Flags]
    public enum ReaderWriterLockType : short
    {
        READ = 1,
        WRITE = 2
    }

    public class ReaderWriterLockObject : IDisposable
    {
        private ReaderWriterLock _rwl;
        private ReaderWriterLockType _type;

        public ReaderWriterLockObject(ReaderWriterLock rwl, ReaderWriterLockType type)
        {
            _rwl = rwl;
            _type = type;

            if (_type.HasFlag(ReaderWriterLockType.READ))
                _rwl.AcquireReaderLock(15000);

            if (_type.HasFlag(ReaderWriterLockType.WRITE))
                _rwl.AcquireWriterLock(15000);
        }

        public void Dispose()
        {
            if (_type.HasFlag(ReaderWriterLockType.READ))
                _rwl.ReleaseReaderLock();

            if (_type.HasFlag(ReaderWriterLockType.WRITE))
                _rwl.ReleaseWriterLock();
        }
    }

    public static class ReaderWriterLockExtensions
    {
        public static ReaderWriterLockObject LockRead(this ReaderWriterLock rwl)
        {
            return new ReaderWriterLockObject(rwl, ReaderWriterLockType.READ);
        }

        public static ReaderWriterLockObject LockWrite(this ReaderWriterLock rwl)
        {
            return new ReaderWriterLockObject(rwl, ReaderWriterLockType.WRITE);
        }
    }
}
