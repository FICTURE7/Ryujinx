using System;

namespace ARMeilleure.Tests
{
    // TODO: Use the new abstractions to handle memory?

    interface IAarch64Emulator : IDisposable
    {
        void Emulate(ulong address, ulong size);

        void MapMemory(ulong address, ulong size);
        void WriteMemory(ulong address, byte[] bytes);

        Aarch64Context GetContext();
        void SetContext(Aarch64Context value);
    }
}
