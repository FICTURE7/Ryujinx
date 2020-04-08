using System;
using System.Runtime.InteropServices;

namespace ARMeilleure.Tests.Emulators
{
    partial class UnicornEmulator
    {
        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint uc_version(out uint major, out uint minor);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_open(UnicornArch arch, UnicornMode mode, out IntPtr uc);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_close(IntPtr uc);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uc_strerror(UnicornError err);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_reg_write(IntPtr uc, int regid, byte[] value);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_reg_read(IntPtr uc, int regid, byte[] value);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_mem_write(IntPtr uc, ulong address, byte[] bytes, ulong size);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_mem_read(IntPtr uc, ulong address, byte[] bytes, ulong size);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_emu_start(IntPtr uc, ulong begin, ulong until, ulong timeout, ulong count);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_mem_map(IntPtr uc, ulong address, ulong size, uint perms);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_mem_unmap(IntPtr uc, ulong address, ulong size);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_mem_protect(IntPtr uc, ulong address, ulong size, uint perms);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern UnicornError uc_mem_regions(IntPtr uc, out IntPtr regions, out uint count);
    }
}
