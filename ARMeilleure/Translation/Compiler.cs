using ARMeilleure.CodeGen;
using ARMeilleure.CodeGen.X86;
using ARMeilleure.Diagnostics;
using ARMeilleure.IntermediateRepresentation;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ARMeilleure.Translation
{
    static class Compiler
    {
        static int Id { get; set; }

        static bool Dumping { get; } = true;

        static bool IsBase { get; }
        static bool IsDiff => !IsBase;

        static Compiler()
        {
            Id = 0;

            Directory.CreateDirectory("base");
            Directory.CreateDirectory("diff");

            // If the base directory is empty it means we're dumping the base.
            IsBase = Directory.GetFiles("base").Length == 0;
        }

        public static T Compile<T>(ControlFlowGraph cfg, OperandType[] argTypes, OperandType retType, CompilerOptions options, string name = null)
        {
            CompiledFunction func = CompileAndGetCf(cfg, argTypes, retType, options);

            IntPtr codePtr = JitCache.Map(func);

            if (Dumping)
            {
                name = name ?? Id++.ToString();
                name = $"{name}.{(options == CompilerOptions.HighCq ? "hcq" : "lcq")}.asm";

                if (IsBase || (IsDiff && File.Exists($"./base/{name}")))
                {
                    name = $"./{(IsBase ? "base" : "diff")}/{name}";

                    // Do the IO on another thread.
                    var _ = File.WriteAllTextAsync(name, CodeDumper.GetCode(ref func));
                }
            }

            return Marshal.GetDelegateForFunctionPointer<T>(codePtr);
        }

        public static CompiledFunction CompileAndGetCf(ControlFlowGraph cfg, OperandType[] argTypes, OperandType retType, CompilerOptions options)
        {
            Logger.StartPass(PassName.Dominance);

            Dominance.FindDominators(cfg);
            Dominance.FindDominanceFrontiers(cfg);

            Logger.EndPass(PassName.Dominance);

            Logger.StartPass(PassName.SsaConstruction);

            if ((options & CompilerOptions.SsaForm) != 0)
            {
                Ssa.Construct(cfg);
            }
            else
            {
                RegisterToLocal.Rename(cfg);
            }

            Logger.EndPass(PassName.SsaConstruction, cfg);

            CompilerContext cctx = new CompilerContext(cfg, argTypes, retType, options);

            return CodeGenerator.Generate(cctx);
        }
    }
}