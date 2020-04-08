namespace ARMeilleure.Tests
{
    public class Tests
    {
        public static void Main()
        {
            using var test = Aarch64Test.Load("../../../aarch64/sandbox");

            test.Run();
            test.Dump();
            test.Assert();
        }
    }
}