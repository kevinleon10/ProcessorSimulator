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

        public void Daddi(int destiny, int source, int inmediate)
        {
            Registers[destiny] = Registers[source] + inmediate;
        }

        public void Dadd(int destiny, int firstSource, int secondSource)
        {
            Registers[destiny] = Registers[firstSource] + Registers[secondSource];
        }

        public void Dsub(int destiny, int firstSource, int secondSource)
        {
            Registers[destiny] = Registers[firstSource] - Registers[secondSource];
        }
        
        public void Dmul(int destiny, int firstSource, int secondSource)
        {
            Registers[destiny] = Registers[firstSource] * Registers[secondSource];
        }
        
        public void Ddiv(int destiny, int firstSource, int secondSource)
        {
            Registers[destiny] = Registers[firstSource] / Registers[secondSource];
        }
        
        public bool Beqz(int source, int inmediate)
        {
            return Registers[source] == inmediate;
        }
        
        public bool Bnez(int source, int inmediate)
        {
            return Registers[source] != inmediate;
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
    }
}