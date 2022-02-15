using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UnsafeGbxConnector.Utilities
{
    public class SynchronizationManager
    {
        [ThreadStatic] private static int ThreadId;

        private int _owner;

        public SynchronizationManager()
        {
            _owner = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SyncContext Synchronize()
        {
            return new SyncContext(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            if (ThreadId == 0)
                ThreadId = Environment.CurrentManagedThreadId;

            while (Interlocked.CompareExchange(ref _owner, ThreadId, 0) != ThreadId)
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unlock()
        {
            if (ThreadId != Interlocked.Exchange(ref _owner, 0))
                throw new UnauthorizedAccessException("Unlocking failure");
            
            Interlocked.MemoryBarrier();
        }

        public readonly struct SyncContext : IDisposable
        {
            private readonly SynchronizationManager _synchronizer;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SyncContext(SynchronizationManager synchronizer)
            {
                _synchronizer = synchronizer;
                _synchronizer.Lock();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _synchronizer.Unlock();
            }
        }
    }
}