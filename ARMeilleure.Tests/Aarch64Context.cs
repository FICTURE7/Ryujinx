using ARMeilleure.State;

namespace ARMeilleure.Tests
{
    class Aarch64Context
    {
        // TODO: Figure out something better? Re-use constants in ARMeilleure perhaps?
        public const int NumberOfXRegs = 31;
        public const int NumberOfQRegs = 32;

        public ulong[] X { get; } = new ulong[NumberOfXRegs];
        public V128[] Q { get; } = new V128[NumberOfQRegs];
    }
}
