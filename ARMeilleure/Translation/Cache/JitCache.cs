using ARMeilleure.CodeGen;
using ARMeilleure.CodeGen.Unwinding;
using ARMeilleure.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ARMeilleure.Translation.Cache
{
    static class JitCache
    {
        private const int PageSize = 4 * 1024;
        private const int PageMask = PageSize - 1;

        private const int CodeAlignment = 4; // Bytes.
        public const int CacheSize = 128 * 1024 * 1024;

        public static ConcurrentQueue<IntPtr> PurgeQueue { get; } = new ConcurrentQueue<IntPtr>();

        private static ReservedRegion _jitRegion;
        private static CacheMemoryAllocator _cacheAllocator;
        private static List<CacheEntry> _cacheEntries = new List<CacheEntry>();

        private static readonly object _lock = new object();
        private static bool _initialized;

        public static void Initialize(IJitMemoryAllocator allocator)
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                _jitRegion = new ReservedRegion(allocator, CacheSize);

                _cacheAllocator = new CacheMemoryAllocator(CacheSize);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    JitUnwindWindows.InstallFunctionTableHandler(_jitRegion.Pointer, CacheSize, _jitRegion.Pointer + Allocate(PageSize));
                }

                _initialized = true;
            }
        }

        public static IntPtr Map(CompiledFunction func)
        {
            byte[] code = func.Code;

            lock (_lock)
            {
                Debug.Assert(_initialized);

                int funcOffset = Allocate(code.Length);

                // If we fail to allocate, try to purge the purge queue.
                if (funcOffset < 0)
                {
                    Purge(code.Length);

                    funcOffset = Allocate(code.Length);
                }

                if (funcOffset < 0)
                {
                    throw new OutOfMemoryException("JIT Cache exhausted.");
                }

                IntPtr funcPtr = _jitRegion.Pointer + funcOffset;

                ReprotectAsWritable(funcOffset, code.Length);

                Marshal.Copy(code, 0, funcPtr, code.Length);

                ReprotectAsExecutable(funcOffset, code.Length);

                Add(funcOffset, code.Length, func.UnwindInfo);

                return funcPtr;
            }
        }

        public static void Unmap(IntPtr pointer)
        {
            lock (_lock)
            {
                Debug.Assert(_initialized);

                int funcOffset = (int)(pointer.ToInt64() - _jitRegion.Pointer.ToInt64());

                bool result = TryFind(funcOffset, out CacheEntry entry);
                Debug.Assert(result);

                _cacheAllocator.Free(funcOffset, AlignCodeSize(entry.Size));

                Remove(funcOffset);
            }
        }

        private static void ReprotectAsWritable(int offset, int size)
        {
            int endOffs = offset + size;

            int regionStart = offset & ~PageMask;
            int regionEnd = (endOffs + PageMask) & ~PageMask;

            _jitRegion.Block.MapAsRwx((ulong)regionStart, (ulong)(regionEnd - regionStart));
        }

        private static void ReprotectAsExecutable(int offset, int size)
        {
            int endOffs = offset + size;

            int regionStart = offset & ~PageMask;
            int regionEnd = (endOffs + PageMask) & ~PageMask;

            _jitRegion.Block.MapAsRx((ulong)regionStart, (ulong)(regionEnd - regionStart));
        }

        private static int Allocate(int codeSize)
        {
            codeSize = AlignCodeSize(codeSize);

            int allocOffset = _cacheAllocator.Allocate(codeSize);

            if (allocOffset < 0)
            {
                return allocOffset;
            }

            _jitRegion.ExpandIfNeeded((ulong)allocOffset + (ulong)codeSize);

            return allocOffset;
        }

        private static int AlignCodeSize(int codeSize)
        {
            return checked(codeSize + (CodeAlignment - 1)) & ~(CodeAlignment - 1);
        }

        private static void Add(int offset, int size, UnwindInfo unwindInfo)
        {
            CacheEntry entry = new CacheEntry(offset, size, unwindInfo);

            int index = _cacheEntries.BinarySearch(entry);

            if (index < 0)
            {
                index = ~index;
            }

            _cacheEntries.Insert(index, entry);
        }

        private static void Remove(int offset)
        {
            int index = _cacheEntries.BinarySearch(new CacheEntry(offset, 0, default));

            if (index < 0)
            {
                index = ~index - 1;
            }

            if (index >= 0)
            {
                _cacheEntries.RemoveAt(index);
            }
        }

        public static bool TryFind(int offset, out CacheEntry entry)
        {
            lock (_lock)
            {
                int index = _cacheEntries.BinarySearch(new CacheEntry(offset, 0, default));

                if (index < 0)
                {
                    index = ~index - 1;
                }

                if (index >= 0)
                {
                    entry = _cacheEntries[index];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public static void Purge(int size)
        {
            int count = 0;
            int freed = 0;

            lock (_lock)
            {
                _cacheAllocator.Dump();

                while (PurgeQueue.TryDequeue(out IntPtr ptr))
                {
                    int offset = (int)((long)ptr - (long)_jitRegion.Pointer);
                    bool found = TryFind(offset, out var entry);

                    Debug.Assert(found);

                    Unmap(ptr);

                    freed += entry.Size;
                    count += 1;
                }

                Console.WriteLine($"[JIT]: Purged cache for {size} bytes -- freed {freed} bytes from {count} functions.");

                _cacheAllocator.Dump();
            }
        }
    }
}