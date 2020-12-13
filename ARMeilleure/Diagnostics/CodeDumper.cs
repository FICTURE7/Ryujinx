using ARMeilleure.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ARMeilleure.Diagnostics
{
    static class CodeDumper
    {
        private static bool NDisasmAvailable { get; }
        private static string NDisasmPath { get; } = "ndisasm";

        private struct DisasmInstruction
        {
            public ulong Index;
            public bool IsJump;
            public ulong? Constant;
            public ReadOnlyMemory<char> Value;

            public DisasmInstruction(ulong index, bool isJump, ulong? constant, ReadOnlyMemory<char> value) =>
                (Index, IsJump, Constant, Value) = (index, isJump, constant, value);
        }

        static CodeDumper()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = NDisasmPath,
                    Arguments = $"-h",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }).WaitForExit();

                NDisasmAvailable = true;
            }
            catch
            {
                NDisasmAvailable = false;
            }
        }

        private static readonly string[] JumpInstructions = new string[]
        {
            "jmp", 
            "je" , "jz", 
            "jne", "jnz",
            "js" ,
            "jg" , "jnle",
            "jge", "jnl",
            "jl" , "jnge",
            "jle", "jng",
            "ja" , "jnbe",
            "jae", "jnb",
            "jb" , "jnae",
            "jbe", "jna",
        };

        public static async Task<string> GetCodeAsync(CompiledFunction func)
        {
            if (!NDisasmAvailable)
            {
                throw new InvalidOperationException($"ndisasm is not available.");
            }

            var tempFile = Path.GetTempFileName();

            await File.WriteAllBytesAsync(tempFile, func.Code);

            var ndisasm = Process.Start(new ProcessStartInfo
            {
                FileName = NDisasmPath,
                Arguments = $"-b 64 {tempFile}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            string ndisasmDisassembly = await ndisasm.StandardOutput.ReadToEndAsync();
            string disassembly = PostProcessNDisasm(ndisasmDisassembly);

            File.Delete(tempFile);

            return disassembly;
        }

        public static string PostProcessNDisasm(string disassembly)
        {
            static bool IsHexDigit(char c) =>
                char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            static ReadOnlyMemory<char> NextLine(ref ReadOnlyMemory<char> disasm)
            {
                int lineIndex = disasm.Span.IndexOf("\n");
                var line      = disasm.Slice(0, lineIndex).TrimEnd();

                disasm = disasm.Slice(lineIndex).TrimStart();

                return line;
            }

            static bool TryNextHex(ref ReadOnlyMemory<char> line, out ReadOnlyMemory<char> result)
            {
                var lineSpan = line.Span;
                int length   = 0;

                while (length < line.Length && IsHexDigit(lineSpan[length]))
                {
                    length++;
                }

                result = line.Slice(0, length);
                line   = line.Slice(length).TrimStart();

                return result.Length > 0;
            }

            ReadOnlyMemory<char> disasm = disassembly.AsMemory();

            var builder = new StringBuilder();
            var insts   = new List<DisasmInstruction>();
            var labels  = new List<ulong>();

            labels.Add(0);

            // Determines if the next instruction position is a label. This used to mark instruction
            // after a jump as a label.
            var nextLabel = false;

            // Parse output of ndisasm and get index of each instructions.
            while (disasm.Length != 0)
            {
                ReadOnlyMemory<char> line = NextLine(ref disasm);

                if (!TryNextHex(ref line, out ReadOnlyMemory<char> hex))
                {
                    continue;
                }

                if (!TryNextHex(ref line, out _))
                {
                    continue;
                }

                var index = ulong.Parse(hex.Span, NumberStyles.HexNumber, null);
                // Remainder is the instruction.
                var value = line;

                var    valueSpan = value.Span;
                ulong? constant  = null;
                bool   isJump    = false;

                // Check if the operand is a constant.
                int hexIndex = valueSpan.IndexOf("0x");

                if (hexIndex != -1)
                {
                    // Check if it is a jump instruction.
                    foreach (var jumpInst in JumpInstructions)
                    {
                        if (valueSpan.StartsWith(jumpInst))
                        {
                            isJump = true;

                            break;
                        }
                    }

                    if (ulong.TryParse(valueSpan.Slice(hexIndex + 2), NumberStyles.HexNumber, null, out ulong result))
                    {
                        if (isJump || Symbols.Get(result) != null)
                        {
                            constant = result;

                            // Trim out constant operand from instruction.
                            value = value.Slice(0, hexIndex);
                        }
                    }

                    if (isJump)
                    {
                        labels.Add(result);
                    }
                }

                if (nextLabel)
                {
                    if (!labels.Contains(index))
                    {
                        labels.Add(index);
                    }

                    nextLabel = false;
                }

                if (isJump || valueSpan.StartsWith("ret"))
                {
                    nextLabel = true;
                }

                insts.Add(new DisasmInstruction(index, isJump, constant, value));
            }

            labels.Sort();

            static bool AppendLabel(StringBuilder builder, List<ulong> labels, ulong jumpIndex)
            {
                int labelIndex = labels.BinarySearch(jumpIndex);

                if (labelIndex < 0)
                {
                    return false;
                }
                else
                {
                    builder.Append(".L").Append(labelIndex);

                    return true;
                }
            }

            const string Indent = "  ";

            // Write all instructions to the StringBuilder.
            foreach (var inst in insts)
            {
                if (AppendLabel(builder, labels, inst.Index))
                {
                    builder.AppendLine(":");
                }

                builder.Append(Indent).Append(inst.Value.Span);

                if (inst.Constant.HasValue)
                {
                    if (inst.IsJump)
                    {
                        AppendLabel(builder, labels, inst.Constant.Value);
                    }
                    else
                    {
                        builder.Append(Symbols.Get(inst.Constant.Value));
                    }
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
