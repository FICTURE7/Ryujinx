using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using ARMeilleure.Translation;
using ARMeilleure.Translation.Cache;

using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Instructions
{
    static class InstEmitFlowHelper
    {
        public static void EmitCondBranch(ArmEmitterContext context, Operand target, Condition cond)
        {
            if (cond != Condition.Al)
            {
                context.BranchIfTrue(target, GetCondTrue(context, cond));
            }
            else
            {
                context.Branch(target);
            }
        }

        public static Operand GetCondTrue(ArmEmitterContext context, Condition condition)
        {
            Operand cmpResult = context.TryGetComparisonResult(condition);

            if (cmpResult != null)
            {
                return cmpResult;
            }

            Operand value = Const(1);

            Operand Inverse(Operand val)
            {
                return context.BitwiseExclusiveOr(val, Const(1));
            }

            switch (condition)
            {
                case Condition.Eq:
                    value = GetFlag(PState.ZFlag);
                    break;

                case Condition.Ne:
                    value = Inverse(GetFlag(PState.ZFlag));
                    break;

                case Condition.GeUn:
                    value = GetFlag(PState.CFlag);
                    break;

                case Condition.LtUn:
                    value = Inverse(GetFlag(PState.CFlag));
                    break;

                case Condition.Mi:
                    value = GetFlag(PState.NFlag);
                    break;

                case Condition.Pl:
                    value = Inverse(GetFlag(PState.NFlag));
                    break;

                case Condition.Vs:
                    value = GetFlag(PState.VFlag);
                    break;

                case Condition.Vc:
                    value = Inverse(GetFlag(PState.VFlag));
                    break;

                case Condition.GtUn:
                {
                    Operand c = GetFlag(PState.CFlag);
                    Operand z = GetFlag(PState.ZFlag);

                    value = context.BitwiseAnd(c, Inverse(z));

                    break;
                }

                case Condition.LeUn:
                {
                    Operand c = GetFlag(PState.CFlag);
                    Operand z = GetFlag(PState.ZFlag);

                    value = context.BitwiseOr(Inverse(c), z);

                    break;
                }

                case Condition.Ge:
                {
                    Operand n = GetFlag(PState.NFlag);
                    Operand v = GetFlag(PState.VFlag);

                    value = context.ICompareEqual(n, v);

                    break;
                }

                case Condition.Lt:
                {
                    Operand n = GetFlag(PState.NFlag);
                    Operand v = GetFlag(PState.VFlag);

                    value = context.ICompareNotEqual(n, v);

                    break;
                }

                case Condition.Gt:
                {
                    Operand n = GetFlag(PState.NFlag);
                    Operand z = GetFlag(PState.ZFlag);
                    Operand v = GetFlag(PState.VFlag);

                    value = context.BitwiseAnd(Inverse(z), context.ICompareEqual(n, v));

                    break;
                }

                case Condition.Le:
                {
                    Operand n = GetFlag(PState.NFlag);
                    Operand z = GetFlag(PState.ZFlag);
                    Operand v = GetFlag(PState.VFlag);

                    value = context.BitwiseOr(z, context.ICompareNotEqual(n, v));

                    break;
                }
            }

            return value;
        }

        public static void EmitCall(ArmEmitterContext context, ulong immediate)
        {
            bool isRecursive = immediate == context.EntryAddress;

            EmitAddressTableBranch(context, Const(immediate), isJump: isRecursive);
        }

        public static void EmitVirtualCall(ArmEmitterContext context, Operand target)
        {
            EmitAddressTableBranch(context, target, isJump: false);
        }

        public static void EmitVirtualJump(ArmEmitterContext context, Operand target, bool isReturn)
        {
            if (isReturn)
            {
                context.Return(target);
            }
            else
            {
                EmitAddressTableBranch(context, target, isJump: true);
            }
        }

        public static void EmitTailContinue(ArmEmitterContext context, Operand address, bool hintRejit)
        {
            // Left option here as it may be useful if we need to return to managed rather than tail call in future.
            // (eg. for debug)
            bool useTailContinue = true;

            if (useTailContinue)
            {
                EmitAddressTableBranch(context, address, isJump: true, hintRejit);
            }
            else
            {
                context.Return(address);
            }
        }

        private static void EmitAddressTableBranch(ArmEmitterContext c, Operand guestAddress, bool isJump, bool hintRejit = true)
        {
            if (guestAddress.Type == OperandType.I32)
            {
                guestAddress = c.ZeroExtend32(OperandType.I64, guestAddress);
            }

            Operand hostAddress;

            Operand lblFallback = Label();
            Operand lblEnd = Label();

            Operand index3 = c.BitwiseAnd(c.ShiftRightUI(guestAddress, Const(39)), Const(0x1FFul));
            Operand index2 = c.BitwiseAnd(c.ShiftRightUI(guestAddress, Const(30)), Const(0x1FFul));
            Operand index1 = c.BitwiseAnd(c.ShiftRightUI(guestAddress, Const(21)), Const(0x1FFul));
            Operand index0 = c.BitwiseAnd(c.ShiftRightUI(guestAddress, Const(2)),  Const(0x7FFFFul));

            Operand level3 = Const((long)c.FunctionTable.Base);
            Operand level2 = c.Load(OperandType.I64, c.Add(level3, c.ShiftLeft(index3, Const(3))));

            c.BranchIfFalse(lblFallback, level2);

            Operand level1 = c.Load(OperandType.I64, c.Add(level2, c.ShiftLeft(index2, Const(3))));

            c.BranchIfFalse(lblFallback, level1);

            Operand level0 = c.Load(OperandType.I64, c.Add(level1, c.ShiftLeft(index1, Const(3))));

            c.BranchIfFalse(lblFallback, level0);

            Operand offset = c.ZeroExtend32(OperandType.I64, c.Load(OperandType.I32, c.Add(level0, c.ShiftLeft(index0, Const(2)))));

            c.BranchIfFalse(lblFallback, offset);

            hostAddress = c.Add(Const((long)JitCache.Base), offset);

            EmitGuestCall(c, hostAddress, isJump);

            c.Branch(lblEnd);

            c.MarkLabel(lblFallback, BasicBlockFrequency.Cold);

            hostAddress = c.Call(typeof(NativeInterface).GetMethod(hintRejit
                        ? nameof(NativeInterface.GetFunctionAddress)
                        : nameof(NativeInterface.GetFunctionAddressWithoutRejit)), guestAddress);

            EmitGuestCall(c, hostAddress, isJump);

            c.MarkLabel(lblEnd);
        }

        private static void EmitGuestCall(ArmEmitterContext c, Operand hostAddress, bool isJump)
        {
            Operand nativeContext = c.LoadArgument(OperandType.I64, 0);

            c.StoreToContext();

            if (isJump)
            {
                c.Tailcall(hostAddress, nativeContext);
            }
            else
            {
                OpCode op = c.CurrOp;

                Operand returnAddress = c.Call(hostAddress, OperandType.I64, nativeContext);

                c.LoadFromContext();

                // Note: The return value of a translated function is always an Int64 with the address execution has
                // returned to. We expect this address to be immediately after the current instruction, if it isn't we
                // keep returning until we reach the dispatcher.
                Operand nextAddr = Const((long)op.Address + op.OpCodeSizeInBytes);

                // Try to continue within this block.
                // If the return address isn't to our next instruction, we need to return so the JIT can figure out
                // what to do.
                Operand lblContinue = c.GetLabel(nextAddr.Value);

                // We need to clear out the call flag for the return address before comparing it.
                c.BranchIf(lblContinue, returnAddress, nextAddr, Comparison.Equal, BasicBlockFrequency.Cold);

                c.Return(returnAddress);
            }
        }
    }
}
