using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace UnsafeGbxConnector.Utilities
{
    // Provide a pool of StringBuilder
    // Getting and Recycling is Thread-Safe
    // All threads will access the same pool
    internal static class StringBuilderPool
    {
        private static readonly List<StringBuilder> _builders = new();

        [ThreadStatic] private static int ThreadId;

        private static int Owner;

        static StringBuilderPool()
        {
            // initialize 256 sb by default
            for (var i = 0; i < 256; i++)
            {
                _builders.Add(new StringBuilder(512));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Lock()
        {
            if (ThreadId == 0)
                ThreadId = Environment.CurrentManagedThreadId;

            while (Interlocked.CompareExchange(ref Owner, ThreadId, 0) != ThreadId)
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unlock()
        {
            if (ThreadId != Interlocked.Exchange(ref Owner, 0))
                throw new UnauthorizedAccessException("Unlocking failure");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringBuilder Get()
        {
            Lock();
            if (_builders.Count == 0)
            {
                Unlock();
                return new StringBuilder();
            }

            var sb = _builders[^1];
            sb.Clear();

            _builders.RemoveAt(_builders.Count - 1);

            Unlock();
            return sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Recycle(StringBuilder sb)
        {
            Lock();
            _builders.Add(sb);
            Unlock();
        }
    }
}