using ARMeilleure.CodeGen;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ARMeilleure.Diagnostics
{
    static class CodeDumper
    {
        // TODO: Add non-diffable code dumping.

        public static string GetCode(ref CompiledFunction func)
        {
            string tempFile = Path.GetTempFileName();

            File.WriteAllBytes(tempFile, func.Code);

            var objdump = Process.Start(new ProcessStartInfo
            {
                FileName = "objdump",
                Arguments = $"-Mintel,x86-64 -D -b binary --no-show-raw-insn -m i386 {tempFile}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });

            // Skip over the first 7 lines, because we do not use them.
            for (int index = 0; index < 7; index++)
                objdump.StandardOutput.ReadLine();

            var builder = new StringBuilder();

            while (!objdump.StandardOutput.EndOfStream)
            {
                string line = objdump.StandardOutput.ReadLine().TrimEnd();

                line = line.Remove(0, line.IndexOf(':') + 2);
                line = ReplaceSymbols(line);

                // Strip the address of each instruction (improves diffs quality).
                builder.AppendLine(line);
            }

            File.Delete(tempFile);

            return builder.ToString();
        }

        private static string ReplaceSymbols(string line)
        {
            if (!TryParseAddress(line, out string address, out long addressValue))
                return line;

            string symbol = Symbols.Get((ulong)addressValue);

            if (symbol != null)
            {
                return line.Replace(address, symbol);
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

            var builder = new StringBuilder(16);

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
