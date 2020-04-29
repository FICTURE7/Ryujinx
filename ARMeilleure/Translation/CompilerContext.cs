using ARMeilleure.IntermediateRepresentation;

namespace ARMeilleure.Translation
{
    struct CompilerContext
    {
        public string Name { get; }
        public ControlFlowGraph Cfg { get; }

        public OperandType[] FuncArgTypes   { get; }
        public OperandType   FuncReturnType { get; }

        public CompilerOptions Options { get; }

        public CompilerContext(
            string           name,
            ControlFlowGraph cfg,
            OperandType[]    funcArgTypes,
            OperandType      funcReturnType,
            CompilerOptions  options)
        {
            Name           = name;
            Cfg            = cfg;
            FuncArgTypes   = funcArgTypes;
            FuncReturnType = funcReturnType;
            Options        = options;
        }
    }
}