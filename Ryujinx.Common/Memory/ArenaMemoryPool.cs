using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;

namespace Ryujinx.Common.Memory
{
    public class ArenaMemoryPool<T> : MemoryPool<T> where T : unmanaged
    {
        private const int DefaultSize = 64 * 1024 * 1024;

        public static new ArenaMemoryPool<T> Shared { get; } = new();

        private int _index;
        private int _inuse;
        private int _version;
        private readonly T[] _buffer;

        public override int MaxBufferSize => int.MaxValue;

        public unsafe ArenaMemoryPool(int size = -1)
        {
            _index = 0;
            _inuse = 0;
            _version = 0;

            if (size == -1)
            {
                size = DefaultSize / sizeof(T);
            }

            _buffer = GC.AllocateUninitializedArray<T>(size, true);
        }

        private Memory<T> Allocate(int length)
        {
            lock (_buffer)
            {
                _inuse++;

                // We cannot allocate inside the arena buffer, fallback on a normal
                // GC tracked allocation.
                if (_index + length > _buffer.Length)
                {
                    return GC.AllocateUninitializedArray<T>(length);
                }

                Memory<T> result = new(_buffer, _index, length);

                _index += length;

                return result;
            }
        }

        public void Free()
        {
            lock (_buffer)
            {
                _inuse--;
            }
        }

        public override IMemoryOwner<T> Rent(int minBufferSize)
        {
            return new ArenaMemoryBlock(this, minBufferSize);
        }

        public bool Reset()
        {
            lock (_buffer)
            {
                // Reset arena only if not in use.
                if (_inuse == 0)
                {
                    _index = 0;
                    _version++;

                    return true;
                }

                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            // _buffer is already managed by the GC.
        }

        private sealed class ArenaMemoryBlock : IMemoryOwner<T>
        {
            private int _disposed;
            private readonly int _version;
            private readonly Memory<T> _memory;
            private readonly ArenaMemoryPool<T> _pool;

            public Memory<T> Memory
            {
                get
                {
                    // NOTE: This should never happen, because the arena is reset only after all memory block was
                    // disposed.
                    if (_version != _pool._version)
                    {
                        ThrowDisposed();
                    }

                    return _memory;
                }
            }

            public ArenaMemoryBlock(ArenaMemoryPool<T> pool, int length)
            {
                _pool = pool;
                _version = pool._version;
                _memory = pool.Allocate(length);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _pool.Free();
                }

                GC.SuppressFinalize(this);
            }

            ~ArenaMemoryBlock()
            {
                // Ideally this should happen, so we assert in debug.
                Debug.Fail("An ArenaMemoryBlock is not being disposed.");

                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _pool.Free();
                }
            }

            private static void ThrowDisposed()
            {
                throw new ObjectDisposedException(null, "Accessing ArenaMemoryBlock after arena was reset.");
            }
        }
    }
}
