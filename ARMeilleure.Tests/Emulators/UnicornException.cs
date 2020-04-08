using System;
using System.Runtime.InteropServices;

namespace ARMeilleure.Tests.Emulators
{
    class UnicornException : Exception
    {
        public UnicornError Error { get; }

        public UnicornException(UnicornError error)
        {
            Error = error;
        }

        public override string Message
        {
            get => Marshal.PtrToStringAnsi(UnicornEmulator.uc_strerror(Error));
        }
    }
}
