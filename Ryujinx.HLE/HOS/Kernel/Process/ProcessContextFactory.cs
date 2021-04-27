using Ryujinx.Cpu;
using Ryujinx.Memory;

namespace Ryujinx.HLE.HOS.Kernel.Process
{
    class ProcessContextFactory : IProcessContextFactory
    {
        public IProcessContext Create(MemoryBlock backingMemory, ulong addressSpaceSize, InvalidAccessHandler invalidAccessHandler, bool for64Bits)
        {
            return new ProcessContext(new AddressSpaceManager(backingMemory, addressSpaceSize));
        }
    }
}
