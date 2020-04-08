using System;
using System.Runtime.InteropServices;

namespace ARMeilleure.Tests.Assemblers
{
    partial class KeystoneAssembler
    {
        [DllImport("keystone", CallingConvention = CallingConvention.Cdecl)]
        public static extern KeystoneError ks_open(KeystoneArch arch, KeystoneMode mode, out IntPtr ks);

        [DllImport("keystone", CallingConvention = CallingConvention.Cdecl)]
        public static extern KeystoneError ks_close(IntPtr ks);

        [DllImport("keystone", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ks_asm(IntPtr ks, [MarshalAs(UnmanagedType.LPStr)] string @string, ulong address, out IntPtr code, out ulong size, out ulong count);

        [DllImport("keystone", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ks_free(IntPtr ptr);
    }
}
