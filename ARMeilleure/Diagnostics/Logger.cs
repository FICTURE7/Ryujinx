#define M_DEBUG

using ARMeilleure.Translation;
using System;
using System.Diagnostics;
using System.IO;

namespace ARMeilleure.Diagnostics
{
    static class Logger
    {
        private static long _startTime;

        private static long[] _accumulatedTime;

        static Logger()
        {
            _accumulatedTime = new long[(int)PassName.Count];
        }

        public static void StartPass(PassName name)
        {
#if M_DEBUG
            WriteOutput(name + " pass started...");

            _startTime = Stopwatch.GetTimestamp();
#endif
        }

        public static void EndPass(PassName name, ControlFlowGraph cfg, string unitName = null)
        {
#if M_DEBUG
            EndPass(name);

            if (Compiler.Dumping && unitName != null)
            {
                string fileName = $"{unitName}-{name}.ir";

                if (Compiler.IsBase || (Compiler.IsDiff && File.Exists($"./base-ir/{fileName}")))
                {
                    fileName = $"./{(Compiler.IsBase ? "base-ir" : "diff-ir")}/{fileName}";

                    // Do the IO on another thread.
                    File.WriteAllTextAsync(fileName, IRDumper.GetDump(cfg));
                }
            }
#endif
        }

        public static void EndPass(PassName name)
        {
#if M_DEBUG
            long elapsedTime = Stopwatch.GetTimestamp() - _startTime;

            _accumulatedTime[(int)name] += elapsedTime;

            WriteOutput($"{name} pass ended after {GetMilliseconds(_accumulatedTime[(int)name])} ms...");
#endif
        }

        private static long GetMilliseconds(long ticks)
        {
            return (long)(((double)ticks / Stopwatch.Frequency) * 1000);
        }

        private static void WriteOutput(string text)
        {
            Console.WriteLine(text);
        }
    }
}