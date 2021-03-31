using ARMeilleure.Common;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using System;
using System.Collections.Generic;

using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Translation
{
    static partial class Ssa
    {
        private class DefMap
        {
            private readonly Dictionary<int, Operand> _map;
            private readonly BitMap _phiMasks;

            public DefMap()
            {
                _map = new Dictionary<int, Operand>();
                _phiMasks = new BitMap(RegisterConsts.TotalCount);
            }

            public bool TryAddOperand(int key, Operand operand)
            {
                return _map.TryAdd(key, operand);
            }

            public bool TryGetOperand(int key, out Operand operand)
            {
                return _map.TryGetValue(key, out operand);
            }

            public bool AddPhi(int key)
            {
                return _phiMasks.Set(key);
            }

            public bool HasPhi(int key)
            {
                return _phiMasks.IsSet(key);
            }
        }

        public static void Construct(ControlFlowGraph cfg)
        {
            var globalDefs = new DefMap[cfg.Blocks.Count];
            var localDefs = new Operand[cfg.Locals.Count + RegisterConsts.TotalCount];

            var dfPhiBlocks = new Queue<BasicBlock>();

            for (BasicBlock block = cfg.Blocks.First; block != null; block = block.ListNext)
            {
                globalDefs[block.Index] = new DefMap();
            }

            // First pass, get all defs and locals uses.
            for (BasicBlock block = cfg.Blocks.First; block != null; block = block.ListNext)
            {
                for (Node node = block.Operations.First; node != null; node = node.ListNext)
                {
                    if (node is not Operation operation)
                    {
                        continue;
                    }

                    for (int index = 0; index < operation.SourcesCount; index++)
                    {
                        Operand src = operation.GetSource(index);

                        if (IsRegisterOrLocal(src))
                        {
                            Operand local = localDefs[GetId(src)] ?? src;

                            operation.SetSource(index, local);
                        }
                    }

                    Operand dest = operation.Destination;

                    if (IsRegisterOrLocal(dest))
                    {
                        Operand local = Local(dest.Type);

                        localDefs[GetId(dest)] = local;

                        operation.Destination = local;
                    }
                }

                for (int index = 0; index < localDefs.Length; index++)
                {
                    Operand local = localDefs[index];

                    if (local is null)
                    {
                        continue;
                    }

                    globalDefs[block.Index].TryAddOperand(index, local);

                    dfPhiBlocks.Enqueue(block);

                    while (dfPhiBlocks.TryDequeue(out BasicBlock dfPhiBlock))
                    {
                        foreach (BasicBlock domFrontier in dfPhiBlock.DominanceFrontiers)
                        {
                            if (globalDefs[domFrontier.Index].AddPhi(index))
                            {
                                dfPhiBlocks.Enqueue(domFrontier);
                            }
                        }
                    }
                }

                Array.Clear(localDefs, 0, localDefs.Length);
            }

            // Second pass, rename variables with definitions on different blocks.
            for (BasicBlock block = cfg.Blocks.First; block != null; block = block.ListNext)
            {
                for (Node node = block.Operations.First; node != null; node = node.ListNext)
                {
                    if (node is not Operation operation)
                    {
                        continue;
                    }

                    for (int index = 0; index < operation.SourcesCount; index++)
                    {
                        Operand src = operation.GetSource(index);

                        if (IsRegisterOrLocal(src))
                        {
                            int key = GetId(src);

                            Operand local = localDefs[key];

                            if (local is null)
                            {
                                local = FindDef(globalDefs, block, src);
                                localDefs[key] = local;
                            }

                            operation.SetSource(index, local);
                        }
                    }
                }

                Array.Clear(localDefs, 0, localDefs.Length);
            }
        }

        private static Operand FindDef(DefMap[] globalDefs, BasicBlock current, Operand operand)
        {
            if (globalDefs[current.Index].HasPhi(GetId(operand)))
            {
                return InsertPhi(globalDefs, current, operand);
            }

            if (current != current.ImmediateDominator)
            {
                return FindDefOnPred(globalDefs, current.ImmediateDominator, operand);
            }

            return Undef();
        }

        private static Operand FindDefOnPred(DefMap[] globalDefs, BasicBlock current, Operand operand)
        {
            BasicBlock previous;

            do
            {
                DefMap defMap = globalDefs[current.Index];

                int key = GetId(operand);

                if (defMap.TryGetOperand(key, out Operand lastDef))
                {
                    return lastDef;
                }

                if (defMap.HasPhi(key))
                {
                    return InsertPhi(globalDefs, current, operand);
                }

                previous = current;
                current  = current.ImmediateDominator;
            }
            while (previous != current);

            return Undef();
        }

        private static Operand InsertPhi(DefMap[] globalDefs, BasicBlock block, Operand operand)
        {
            // This block has a Phi that has not been materialized yet, but that
            // would define a new version of the variable we're looking for. We need
            // to materialize the Phi, add all the block/operand pairs into the Phi, and
            // then use the definition from that Phi.
            Operand local = Local(operand.Type);

            PhiNode phi = new PhiNode(local, block.Predecessors.Count);

            AddPhi(block, phi);

            globalDefs[block.Index].TryAddOperand(GetId(operand), local);

            for (int index = 0; index < block.Predecessors.Count; index++)
            {
                BasicBlock predecessor = block.Predecessors[index];

                phi.SetBlock(index, predecessor);
                phi.SetSource(index, FindDefOnPred(globalDefs, predecessor, operand));
            }

            return local;
        }

        private static void AddPhi(BasicBlock block, PhiNode phi)
        {
            Node node = block.Operations.First;

            if (node != null)
            {
                while (node.ListNext is PhiNode)
                {
                    node = node.ListNext;
                }
            }

            if (node is PhiNode)
            {
                block.Operations.AddAfter(node, phi);
            }
            else
            {
                block.Operations.AddFirst(phi);
            }
        }

        private static int GetId(Operand operand)
        {
            if (operand.Kind == OperandKind.Register)
            {
                Register reg = operand.GetRegister();

                if (reg.Type == RegisterType.Integer)
                {
                    return reg.Index;
                }
                else if (reg.Type == RegisterType.Vector)
                {
                    return RegisterConsts.IntRegsCount + reg.Index;
                }
                else if (reg.Type == RegisterType.Flag)
                {
                    return RegisterConsts.IntAndVecRegsCount + reg.Index;
                }
                else /* if (reg.Type == RegisterType.FpFlag) */
                {
                    return RegisterConsts.FpFlagsOffset + reg.Index;
                }
            }
            else /* if (operand.Kind == OperandKind.LocalVariable && operand.AsInt32() > 0) */
            {
                return RegisterConsts.TotalCount + operand.AsInt32() - 1;
            }
        }

        private static bool IsRegisterOrLocal(Operand operand)
        {
            if (operand is not null)
            {
                if (operand.Kind == OperandKind.Register)
                {
                    return true;
                }
                else if (operand.Kind == OperandKind.LocalVariable && operand.AsInt32() > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}