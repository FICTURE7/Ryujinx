using System;
using System.Runtime.InteropServices;

namespace ARMeilleure.Tests.Assemblers
{
    partial class KeystoneAssembler : IDisposable
    {
        private bool _disposed = false;

        private readonly IntPtr _ks;

        public KeystoneAssembler(KeystoneArch arch, KeystoneMode mode)
        {
            ThrowIfError(ks_open(arch, mode, out _ks));
        }

        public byte[] Assemble(string code)
        {
            if (ks_asm(_ks, code, 0, out IntPtr encoding, out ulong size, out _) != 0)
            {
                // We'll just roll with it for now.
                throw new KeystoneException(KeystoneError.KS_ERR_ASM_UNSUPPORTED);
            }

            byte[] buffer = new byte[size];

            Marshal.Copy(encoding, buffer, 0, (int)size);

            ks_free(encoding);

            return buffer;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ks_close(_ks);

            _disposed = true;
        }

        ~KeystoneAssembler() => Dispose();

        private static void ThrowIfError(KeystoneError error)
        {
            if (error != KeystoneError.KS_ERR_OK)
            {
                throw new KeystoneException(error);
            }
        }
    }
}
