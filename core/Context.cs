namespace ProcessorSimulator.core
{
    public class Context
    {
        public Context(int programCounter, int threadId)
        {
            ProgramCounter = programCounter;
            ThreadId = threadId;
            NumberOfCycles = 0;
            HasPriority = false;
            Registers = new int[32];
            Registers[0] = 0;
        }

        public int ProgramCounter { get; set; }

        public int[] Registers { get; set; }

        public int ThreadId { get; set; }

        public int NumberOfCycles { get; set; }

        public bool HasPriority { get; set; }

        public void Daddi(int source, int destiny, int inmediate)
        {
            Registers[destiny] = Registers[source] + inmediate;
        }

        public void Dadd(int firstSource, int secondSource, int destiny)
        {
            Registers[destiny] = Registers[firstSource] + Registers[secondSource];
        }

        public void Dsub(int firstSource, int secondSource, int destiny)
        {
            Registers[destiny] = Registers[firstSource] - Registers[secondSource];
        }

        public void Dmul(int firstSource, int secondSource, int destiny)
        {
            Registers[destiny] = Registers[firstSource] * Registers[secondSource];
        }

        public void Ddiv(int firstSource, int secondSource, int destiny)
        {
            Registers[destiny] = Registers[firstSource] / Registers[secondSource];
        }

        public void Beqz(int source, int inmediate)
        {
            if (Registers[source] == 0)
            {
                ProgramCounter += (4 * inmediate);
            }
        }

        public void Bnez(int source, int inmediate)
        {
            if (Registers[source] != 0)
            {
                ProgramCounter += (4 * inmediate);
            }
        }

        public void Jal(int inmediate)
        {
            Registers[31] = ProgramCounter;
            ProgramCounter += inmediate;
        }

        public void Jr(int source)
        {
            ProgramCounter = Registers[source];
        }

        public void Load(int source, int destiny)
        {
            Registers[destiny] = source;
        }

        public int Store(int source)
        {
            return Registers[source];
        }
    }
}