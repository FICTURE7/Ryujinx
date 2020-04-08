using ARMeilleure.State;
using System;

namespace ARMeilleure.Tests.Emulators
{
    partial class UnicornEmulator : IAarch64Emulator
    {
        private bool _disposed = false;

        private readonly IntPtr _uc;

        public UnicornEmulator(bool aarch32)
        {
            if (aarch32)
            {
                throw new NotImplementedException();
            }
            else
            {
                ThrowIfError(uc_open(UnicornArch.UC_ARCH_ARM64, UnicornMode.UC_MODE_LITTLE_ENDIAN, out _uc));
            }
        }

        public void Emulate(ulong address, ulong size)
        {
            ThrowIfDisposed();

            // TODO: Maybe we should add a timeout here; in-case stuff starts falling apart.

            ThrowIfError(uc_emu_start(_uc, address, address + size, 0, 0));
        }

        public void MapMemory(ulong address, ulong size)
        {
            ThrowIfDisposed();

            // TODO: Implement MemoryPermissions; so 7 does not appear like a magic number.

            ThrowIfError(uc_mem_map(_uc, address, size, 7));
        }

        public void WriteMemory(ulong address, byte[] bytes)
        {
            ThrowIfDisposed();

            ThrowIfError(uc_mem_write(_uc, address, bytes, (ulong)bytes.Length));
        }

        public Aarch64Context GetContext()
        {
            ThrowIfDisposed();

            var context = new Aarch64Context();
            var buffer = new byte[16];

            for (int i = 0; i < Aarch64Context.NumberOfXRegs; i++)
            {
                ThrowIfError(uc_reg_read(_uc, (int)XRegisters[i], buffer));

                context.X[i] = BitConverter.ToUInt64(buffer, 0);
            }

            for (int i = 0; i < Aarch64Context.NumberOfQRegs; i++)
            {
                ThrowIfError(uc_reg_read(_uc, (int)QRegisters[i], buffer));

                context.Q[i] = new V128(buffer);
            }

            return context;
        }

        public void SetContext(Aarch64Context value)
        {
            ThrowIfDisposed();

            for (int i = 0; i < Aarch64Context.NumberOfXRegs; i++)
            {
                var buffer = BitConverter.GetBytes(value.X[i]);

                ThrowIfError(uc_reg_write(_uc, (int)XRegisters[i], buffer));
            }

            for (int i = 0; i < Aarch64Context.NumberOfQRegs; i++)
            {
                var buffer = value.Q[i].ToArray();

                ThrowIfError(uc_reg_read(_uc, (int)QRegisters[i], buffer));
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ThrowIfError(uc_close(_uc));

            _disposed = true;
        }

        ~UnicornEmulator() => Dispose();

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UnicornEmulator));
            }
        }

        private static void ThrowIfError(UnicornError error)
        {
            if (error != UnicornError.UC_ERR_OK)
            {
                throw new UnicornException(error);
            }
        }
    }
}
