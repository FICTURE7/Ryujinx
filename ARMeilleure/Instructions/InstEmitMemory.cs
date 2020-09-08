using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using ARMeilleure.Translation;
using System;
using System.Collections.Generic;
using System.Reflection;
using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.Instructions.InstEmitMemoryHelper;
using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Instructions
{
    static partial class InstEmit
    {
        public static void Adr(ArmEmitterContext context)
        {
            OpCodeAdr op = (OpCodeAdr)context.CurrOp;

            SetIntOrZR(context, op.Rd, Const(op.Address + (ulong)op.Immediate));
        }

        public static void Adrp(ArmEmitterContext context)
        {
            OpCodeAdr op = (OpCodeAdr)context.CurrOp;

            ulong address = (op.Address & ~0xfffUL) + ((ulong)op.Immediate << 12);

            SetIntOrZR(context, op.Rd, Const(address));
        }

        public static void Ldr(ArmEmitterContext context)  => EmitLdr(context, signed: false);
        public static void Ldrs(ArmEmitterContext context) => EmitLdr(context, signed: true);

        private static void EmitLdr(ArmEmitterContext context, bool signed)
        {
            OpCodeMem op = (OpCodeMem)context.CurrOp;

            Operand address = GetAddress(context);

            if (signed && op.Extend64)
            {
                EmitLoadSx64(context, address, op.Rt, op.Size);
            }
            else if (signed)
            {
                EmitLoadSx32(context, address, op.Rt, op.Size);
            }
            else
            {
                EmitLoadZx(context, address, op.Rt, op.Size);
            }

            EmitWBackIfNeeded(context, address);
        }

        public static void Ldr_Literal(ArmEmitterContext context)
        {
            IOpCodeLit op = (IOpCodeLit)context.CurrOp;

            if (op.Prefetch)
            {
                return;
            }

            if (op.Signed)
            {
                EmitLoadSx64(context, Const(op.Immediate), op.Rt, op.Size);
            }
            else
            {
                EmitLoadZx(context, Const(op.Immediate), op.Rt, op.Size);
            }
        }

        public static void Ldp(ArmEmitterContext context)
        {
            OpCodeMemPair op = (OpCodeMemPair)context.CurrOp;

            void EmitLoad(int rt, Operand ldAddr)
            {
                if (op.Extend64)
                {
                    EmitLoadSx64(context, ldAddr, rt, op.Size);
                }
                else
                {
                    EmitLoadZx(context, ldAddr, rt, op.Size);
                }
            }

            Operand address = GetAddress(context);

            Operand address2 = context.Add(address, Const(1L << op.Size));

            EmitLoad(op.Rt,  address);
            EmitLoad(op.Rt2, address2);

            EmitWBackIfNeeded(context, address);
        }

        public static void Str(ArmEmitterContext context)
        {
            OpCodeMem op = (OpCodeMem)context.CurrOp;

            Operand address = GetAddress(context);

            InstEmitMemoryHelper.EmitStore(context, address, op.Rt, op.Size);

            EmitWBackIfNeeded(context, address);
        }

        class MemoryAccessEmitter
        {
            enum MemoryOperationType
            {
                Load,
                Store
            }

            struct MemoryOperation
            {
                public MemoryOperationType Type;
                public int Size;
                public int Offset;
                public int Register;

                public MemoryOperation(MemoryOperationType type, int offset, int size, int reg) =>
                    (Type, Size, Offset, Register) = (type, size, offset, reg);
            }

            private readonly ArmEmitterContext _emitter;
            private readonly List<MemoryOperation> _operations;

            public Operand BaseAddress { get; set; }

            public MemoryAccessEmitter(ArmEmitterContext emitter)
            {
                _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
                _operations = new List<MemoryOperation>();
            }

            public void AddStore(int reg, int offset, int size)
            {
                // TODO: Sort by offset.
                _operations.Add(new MemoryOperation(MemoryOperationType.Store, offset, 1 << size, reg));
            }

            public void Emit()
            {
                MemoryOperation lastOp = _operations[^1];

                // TODO: Figure out negative offsets & stuff like that.
                uint wholeSize = (uint)lastOp.Offset + (uint)lastOp.Size;

                Operand lblSlowPath = Label();
                Operand lblEnd = Label();

                Operand isUnalignedAddr = EmitAddressCheck2(_emitter, BaseAddress, wholeSize);

                _emitter.BranchIfTrue(lblSlowPath, isUnalignedAddr);

                Operand hostBaseAddr = EmitPtPointerLoad(_emitter, BaseAddress, lblSlowPath, write: true);

                foreach (var op in _operations)
                {
                    // TODO: Implement Loads, figure out sign/zero extension and such.
                    // TODO: Implement vectors. 
                    
                    Operand hostAddr = _emitter.Add(hostBaseAddr, Const(hostBaseAddr.Type, op.Offset));

                    Operand value = GetInt(_emitter, op.Register);

                    if (op.Size < 8 && value.Type == OperandType.I64)
                    {
                        value = _emitter.ConvertI64ToI32(value);
                    }

                    switch (op.Size)
                    {
                        case 1: _emitter.Store8 (hostAddr, value); break;
                        case 2: _emitter.Store16(hostAddr, value); break;
                        case 4: _emitter.Store  (hostAddr, value); break;
                        case 8: _emitter.Store  (hostAddr, value); break;
                    }
                }

                _emitter.Branch(lblEnd);

                _emitter.MarkLabel(lblSlowPath);

                Operand guestBaseAddr = BaseAddress;

                foreach (var op in _operations)
                {
                    Operand guestAddr = _emitter.Add(guestBaseAddr, Const(guestBaseAddr.Type, op.Offset));

                    MethodInfo info = null;

                    switch (op.Size)
                    {
                        case 1: info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteByte));   break;
                        case 2: info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt16)); break;
                        case 4: info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt32)); break;
                        case 8: info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt64)); break;
                    }

                    Operand value = GetInt(_emitter, op.Register);

                    if (op.Size < 8 && value.Type == OperandType.I64)
                    {
                        value = _emitter.ConvertI64ToI32(value);
                    }

                    _emitter.Call(info, guestAddr, value);
                }

                _emitter.MarkLabel(lblEnd);
            }
        }

        public static void Stp(ArmEmitterContext context)
        {
            var op = (OpCodeMemPair)context.CurrOp;

            if (IsSimd(context))
            {
                Operand address = GetAddress(context);

                Operand address2 = context.Add(address, Const(1L << op.Size));

                EmitStore(context, address, op.Rt, op.Size);
                EmitStore(context, address2, op.Rt2, op.Size);

                EmitWBackIfNeeded(context, address);
            }
            else
            {
                var mem = new MemoryAccessEmitter(context);

                Operand address = GetAddress(context);

                mem.BaseAddress = address;

                mem.AddStore(op.Rt, 0, op.Size);
                mem.AddStore(op.Rt2, 1 << op.Size, op.Size);

                mem.Emit();

                EmitWBackIfNeeded(context, address);
            }
        }

        private static Operand GetAddress(ArmEmitterContext context)
        {
            Operand address = null;

            switch (context.CurrOp)
            {
                case OpCodeMemImm op:
                {
                    address = context.Copy(GetIntOrSP(context, op.Rn));

                    // Pre-indexing.
                    if (!op.PostIdx)
                    {
                        address = context.Add(address, Const(op.Immediate));
                    }

                    break;
                }

                case OpCodeMemReg op:
                {
                    Operand n = GetIntOrSP(context, op.Rn);

                    Operand m = GetExtendedM(context, op.Rm, op.IntType);

                    if (op.Shift)
                    {
                        m = context.ShiftLeft(m, Const(op.Size));
                    }

                    address = context.Add(n, m);

                    break;
                }
            }

            return address;
        }

        private static void EmitWBackIfNeeded(ArmEmitterContext context, Operand address)
        {
            // Check whenever the current OpCode has post-indexed write back, if so write it.
            if (context.CurrOp is OpCodeMemImm op && op.WBack)
            {
                if (op.PostIdx)
                {
                    address = context.Add(address, Const(op.Immediate));
                }

                SetIntOrSP(context, op.Rn, address);
            }
        }
    }
}