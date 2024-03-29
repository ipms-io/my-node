﻿using System;
using System.Threading;

namespace my_node
{
    public enum ReaderWriterLockType
    {
        READ,
        WRITE
    }

    public class ReaderWriterLockObject : IDisposable
    {
        private ReaderWriterLock _rwl;
        private ReaderWriterLockType _type;

        public ReaderWriterLockObject(ReaderWriterLock rwl, ReaderWriterLockType type)
        {
            _rwl = rwl;
            _type = type;
            if (_type == ReaderWriterLockType.READ)
                _rwl.AcquireReaderLock(500);
            else if (_type == ReaderWriterLockType.WRITE)
                _rwl.AcquireWriterLock(500);
        }

        public void Dispose()
        {
            if (_type == ReaderWriterLockType.READ)
                _rwl.ReleaseReaderLock();
            else if (_type == ReaderWriterLockType.WRITE)
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
