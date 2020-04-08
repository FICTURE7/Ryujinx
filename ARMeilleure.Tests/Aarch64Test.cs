using ARMeilleure.Tests.Assemblers;
using ARMeilleure.Tests.Emulators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ARMeilleure.Tests
{
    // TODO: Figure out if we even need this class, I feel like doing this with just simple methods would work out better.

    class Aarch64Test : IDisposable
    {
        private bool _disposed = false;

        private readonly byte[] _code;
        private readonly TestEnvironmentConfig _envConfig;

        public UnicornEmulator ExpectEmulator { get; } = new UnicornEmulator(false);
        public ARMeilleureEmulator ActualEmulator { get; }  = new ARMeilleureEmulator(false);

        private Aarch64Test(TestEnvironmentConfig config, byte[] code)
        {
            _envConfig = config ?? throw new ArgumentNullException(nameof(config));
            _code = code ?? throw new ArgumentNullException(nameof(code));
        }

        public void Run()
        {
            foreach (var map in _envConfig.Memory.Map)
            {
                // TODO: Check that map has 2 elements when deserializing.

                ExpectEmulator.MapMemory(map[0], map[1]);
                ActualEmulator.MapMemory(map[0], map[1]);
            }

            // TODO: Use _envConfig entry point when we add label patching and stuff.

            ExpectEmulator.WriteMemory(0, _code);
            ActualEmulator.WriteMemory(0, _code);

            ExpectEmulator.Emulate(0, (ulong)_code.Length - 4);
            ActualEmulator.Emulate(0, (ulong)_code.Length);
        }

        public void Assert()
        {
            // TODO: Floating-point tolerance.

            var expectedContext = ExpectEmulator.GetContext();
            var actualContext   = ActualEmulator.GetContext();

            for (int i = 0; i < Aarch64Context.NumberOfXRegs; i++)
            {
                NUnit.Framework.Assert.AreEqual(expectedContext.X[i], actualContext.X[i]);
            }

            for (int i = 0; i < Aarch64Context.NumberOfQRegs; i++)
            {
                NUnit.Framework.Assert.AreEqual(expectedContext.Q[i], actualContext.Q[i]);
            }
        }

        public void Dump()
        {
            var expectedContext = ExpectEmulator.GetContext();
            var actualContext   = ActualEmulator.GetContext();

            for (int i = 0; i < Aarch64Context.NumberOfXRegs; i++)
            {
                Console.Write($"X{i}".PadRight(5));
                Console.Write(expectedContext.X[i].ToString().PadRight(35));
                Console.WriteLine(actualContext.X[i]);
            }

            for (int i = 0; i < Aarch64Context.NumberOfQRegs; i++)
            {
                Console.Write($"Q{i}".PadRight(5));
                Console.Write(expectedContext.Q[i].ToString().PadRight(35));
                Console.WriteLine(actualContext.Q[i]);
            }
        }

        // TODO: Figure out these classes below, they're quite verbose.
        // TODO: Custom JsonConverter things for this?

        private class TestEnvironmentMemoryConfig
        {
            public List<ulong[]> Map { get; set; }

            // TODO: List of writes?
            // TODO: List of assert reads? (what about floating point tolerance then?)
        }

        private class TestEnvironmentConfig
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Entry { get; set; }
            public TestEnvironmentMemoryConfig Memory { get; set; }
        }

        public static Aarch64Test Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            // Get the full path for better exception messages below.
            var assemblyCodePath = Path.GetFullPath(path + ".asm");
            var envConfigPath = Path.GetFullPath(path + ".json");

            if (!File.Exists(assemblyCodePath))
            {
                throw new ArgumentException($"Specified test path does not contain '{assemblyCodePath}'.");
            }

            if (!File.Exists(envConfigPath))
            {
                throw new ArgumentException($"Specified test path does not contain environment configuration '{envConfigPath}'.");
            }

            // TODO: Consider try-catching this bad boi.
            var envConfig = JsonSerializer.Deserialize<TestEnvironmentConfig>(File.ReadAllText(envConfigPath));

            using var assembler = new KeystoneAssembler(KeystoneArch.KS_ARCH_ARM64, KeystoneMode.KS_MODE_LITTLE_ENDIAN);

            string assemblyCode = File.ReadAllText(assemblyCodePath);
            byte[] machineCode  = assembler.Assemble(assemblyCode);

            return new Aarch64Test(envConfig, machineCode);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ExpectEmulator.Dispose();
            ActualEmulator.Dispose();

            _disposed = true;
        }
    }
}
