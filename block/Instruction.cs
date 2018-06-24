namespace ProcessorSimulator.block
{
    public class Instruction
    {
        public Instruction()
        {
            OperationCode = 0;
            Source = 0;
            Destiny = 0;
            Inmediate = 0;
        }

        public Instruction(int operationCode, int source, int destiny, int inmediate)
        {
            OperationCode = operationCode;
            Source = source;
            Destiny = destiny;
            Inmediate = inmediate;
        }

        public int OperationCode { get; set; }

        public int Source { get; set; }

        public int Destiny { get; set; }

        public int Inmediate { get; set; }

        public override string ToString()
        {
            var instruction = "OP: " + OperationCode + ", Source: " + Source + ", Destiny: " + Destiny +
                              ", Inmediate: " + Inmediate + ".";
            return instruction;
        }
    }
}