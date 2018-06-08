namespace ProcessorSimulator.core
{
    public class Context
    {
        public Context()
        {
            ProgramCounter = 0;
            Registers = new int[32];
            ThreadId = 0;
            NumberOfCycles = 0;
            HasPriority = false;
        }

        public Context(int programCounter, int[] registers, int threadId, int numberOfCycles, bool hasPriority)
        {
            ProgramCounter = programCounter;
            Registers = registers;
            ThreadId = threadId;
            NumberOfCycles = numberOfCycles;
            HasPriority = hasPriority;
        }

        public int ProgramCounter { get; set; }

        public int[] Registers { get; set; }

        public int ThreadId { get; set; }

        public int NumberOfCycles { get; set; }

        public bool HasPriority { get; set; }
    }
}