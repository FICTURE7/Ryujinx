using ARMeilleure.State;
using Ryujinx.Cpu;
using Ryujinx.Memory;
using System;

namespace ARMeilleure.Tests.Emulators
{
    class ARMeilleureEmulator : IAarch64Emulator
    {
        private bool _disposed = false;

        private readonly MemoryBlock _ram;
        private readonly MemoryManager _memory;

        private readonly CpuContext _cpuContext;
        private readonly ExecutionContext _context;

        public ARMeilleureEmulator(bool aarch32)
        {
            if (aarch32)
            {
                throw new NotImplementedException();
            }

            _ram = new MemoryBlock(4 * 1024);
            _memory = new MemoryManager(_ram, 1UL << 16);

            _context = CpuContext.CreateExecutionContext();
            _context.IsAarch32 = aarch32;

            _cpuContext = new CpuContext(_memory);
        }

        public void Emulate(ulong address, ulong size)
        {
            ThrowIfDisposed();

            _cpuContext.Execute(_context, address);
        }

        public void MapMemory(ulong address, ulong size)
        {
            ThrowIfDisposed();

            _memory.Map(address, address, size);
        }

        public void WriteMemory(ulong address, byte[] bytes)
        {
            ThrowIfDisposed();

            _memory.Write(address, bytes);
        }

        public Aarch64Context GetContext()
        {
            ThrowIfDisposed();

            var context = new Aarch64Context();

            for (int i = 0; i < Aarch64Context.NumberOfXRegs; i++)
            {
                context.X[i] = _context.GetX(i);
            }

            for (int i = 0; i < Aarch64Context.NumberOfQRegs; i++)
            {
                context.Q[i] = _context.GetV(i);
            }

            return context;
        }

        public void SetContext(Aarch64Context value)
        {
            ThrowIfDisposed();

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            for (int i = 0; i < Aarch64Context.NumberOfXRegs; i++)
            {
                _context.SetX(i, value.X[i]);
            }

            for (int i = 0; i < Aarch64Context.NumberOfQRegs; i++)
            {
                _context.SetV(i, value.Q[i]);
            }
        }

        public void Dispose() 
        {
            if (_disposed)
                return;

            _context.Dispose();
            _memory.Dispose();
            _context.Dispose();

            _disposed = false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UnicornEmulator));
            }
        }
    }
}
