using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ARMeilleure.Translation
{
    /// <summary>
    /// Represents a table of guest addresses to an unmanaged type.
    /// </summary>
    /// <typeparam name="TValue">Value type of table</typeparam>
    unsafe class AddressTable<TValue> where TValue : unmanaged
    {
        // Sync object.
        private readonly object _sync = new();
        // List of all pinned arrays allocated. We need this otherwise the GC will collect them.
        private readonly List<object> _allocations = new(capacity: 16);
        // Multi-level table (4 deep).
        private readonly TValue**** _table;

        /// <summary>
        /// Gets the base pointer of the <see cref="AddressTable{TValue}"/> instance.
        /// </summary>
        public IntPtr Base => (IntPtr)_table;

        /// <summary>
        /// Initializes a new instance of the <see cref="AddressTable{TValue}"/> class.
        /// </summary>
        public AddressTable()
        {
            _table = (TValue****)Allocate<nint>(1 << 9);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guestAddress"></param>
        /// <param name="value"></param>
        public void SetValue(ulong guestAddress, TValue value)
        {
            lock (_sync)
            {
                GetValue(guestAddress) = value;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="guestAddress"></param>
        /// <returns></returns>
        public ref TValue GetValue(ulong guestAddress)
        {
            lock (_sync)
            {
                return ref GetTable(guestAddress)[(int)(guestAddress >> 2 & 0x7FFFF)];
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guestAddress"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TValue* GetTable(ulong guestAddress)
        {
            Debug.Assert((guestAddress & 0x3) == 0);

            var level3 = _table;

            var level2 = (TValue***)GetNextTable<nint>((void**)level3, (int)(guestAddress >> 39 & 0x1FF), 1 << 9);
            var level1 = (TValue**)GetNextTable<nint>((void**)level2, (int)(guestAddress >> 30 & 0x1FF), 1 << 9);
            var level0 = (TValue*)GetNextTable<TValue>((void**)level1, (int)(guestAddress >> 21 & 0x1FF), 1 << 19);

            return level0;
        }

        /// <summary>
        /// Gets the next table at the specified index in the specified current table. If the next table is <c>null</c>,
        /// it is initialized to an array of type <typeparamref name="T"/> of the specified size.
        /// </summary>
        /// <param name="level">Current table</param>
        /// <param name="index">Index in the current table</param>
        /// <param name="size">Size of next table</param>
        /// <returns>Next table</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void* GetNextTable<T>(void** level, int index, int size) where T : unmanaged
        {
            ref var result = ref level[index];

            if (result == null)
            {
                result = Allocate<T>(size);
            }

            return result;
        }

        /// <summary>
        /// Allocates a pinned array of the specified size and returns a pointer to the first element of the array.
        /// </summary>
        /// <param name="length">Length of the array to allocate.</param>
        /// <returns>Pointer to the first element of the array.</returns>
        private T* Allocate<T>(int length) where T : unmanaged
        {
            Debug.Assert(length > 0);

            var table = GC.AllocateArray<T>(length, true);

            _allocations.Add(table);

            return (T*)Unsafe.AsPointer(ref table[0]);
        }
    }
}
