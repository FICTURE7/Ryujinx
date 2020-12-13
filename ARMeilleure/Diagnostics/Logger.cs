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

        [Conditional("M_DEBUG")]
        public static void StartPass(PassName name)
        {
            WriteOutput(name + " pass started...");

            _startTime = Stopwatch.GetTimestamp();
        }

        [Conditional("M_DEBUG")]
        public static void EndPass(PassName name, ControlFlowGraph cfg, string unitName)
        {
            EndPass(name);

            if (Compiler.Dumping && unitName != null)
            {
                string fileName = $"{unitName}-{name}.ir";

                if (Compiler.IsBase || (Compiler.IsDiff && File.Exists($"./base-ir/{fileName}")))
                {
                    // Do the IO on another thread.
                    File.WriteAllTextAsync($"./{(Compiler.IsBase ? "base-ir" : "diff-ir")}/{fileName}", IRDumper.GetDump(cfg));
                }
            }
        }

        [Conditional("M_DEBUG")]
        public static void EndPass(PassName name)
        {
            long elapsedTime = Stopwatch.GetTimestamp() - _startTime;

            _accumulatedTime[(int)name] += elapsedTime;

            WriteOutput($"{name} pass ended after {GetMilliseconds(_accumulatedTime[(int)name])} ms...");
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