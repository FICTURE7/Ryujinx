using ARMeilleure.CodeGen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ARMeilleure.Diagnostics
{
    static class CodeDumper
    {
        private static Dictionary<long, string> Symbols { get; } = new Dictionary<long, string>(); 
        private static List<(long startPtr, long endPtr, string symbolName)> RangedSymbols { get; } = new List<(long, long, string)>();

        public static void AddSymbol(long ptr, string name)
        {
            if (!Symbols.ContainsKey(ptr))
                Symbols.Add(ptr, name);
        }

        public static void AddSymbol(long startPtr, long endPtr, string name)
        {
            RangedSymbols.Add((startPtr, endPtr, name));
        }

        public static string GetCode(ref CompiledFunction func)
        {
            string tempFile = Path.GetTempFileName();

            File.WriteAllBytes(tempFile, func.Code);

            Process objdump = Process.Start(new ProcessStartInfo
            {
                FileName = "objdump",
                Arguments = $"-Mintel,x86-64 -D -b binary --no-show-raw-insn -m i386 {tempFile}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });

            // Skip over the first 7 lines, because we do not use them.
            for (int index = 0; index < 7; index++)
                objdump.StandardOutput.ReadLine();

            StringBuilder dumpBuilder = new StringBuilder();

            while (!objdump.StandardOutput.EndOfStream)
            {
                string line = objdump.StandardOutput.ReadLine().TrimEnd();

                line = line.Remove(0, line.IndexOf(':') + 2);
                line = ReplaceSymbols(line);

                // Strip the address of each instruction (improves diffs quality).
                dumpBuilder.AppendLine(line);
            }

            File.Delete(tempFile);

            return dumpBuilder.ToString();
        }

        private static string ReplaceSymbols(string line)
        {
            if (!TryParseAddress(line, out string address, out long addressValue))
                return line;

            if (Symbols.TryGetValue(addressValue, out string symbolName))
            {
                line = line.Replace(address, symbolName);
            }
            else
            {
                foreach ((long start, long end, string rSymbolName) in RangedSymbols)
                {
                    if (addressValue >= start && addressValue <= end)
                    {
                        // TODO: Perhaps we can improve PAGE_TABLE and friends with
                        // index. i.e PAGE_TABLE[54] etc.
                        line = line.Replace(address, rSymbolName);

                        break;
                    }
                }
            }

            return line;
        }

        private static bool TryParseAddress(string line, out string address, out long addressValue)
        {
            static bool IsHexDigit(char c)
            {
                return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            }

            static int HexDigitToInt(char c)
            {
                if (char.IsDigit(c))
                    return c - '0';
                if ((c >= 'a' && c <= 'f'))
                    return c - 'a' + 10;
                if ((c >= 'A' && c <= 'F'))
                    return c - 'A' + 10;

                Debug.Fail("This should never happen, we check c with IsHexDigit before.");
                return 0;
            }

            address = null;
            addressValue = 0;

            int index = line.IndexOf("0x");
            if (index == -1)
                return false;

            index += 2;

            if (index < line.Length && !IsHexDigit(line[index]))
                return false;

            StringBuilder builder = new StringBuilder(16);

            builder.Append("0x");

            while (index < line.Length && IsHexDigit(line[index]))
            {
                builder.Append(line[index]);

                addressValue *= 16;
                addressValue += HexDigitToInt(line[index]);

                index++;
            }

            address = builder.ToString();

            return true;
        }
    }
}
