using System.Text;
using ProcessorSimulator.common;

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
            Registers = new int[Constants.NumberOfRegisters];
            for (int i = 0; i < Registers.Length; i++)
            {
                Registers[i] = 0;
            }
        }

        public int ProgramCounter { get; set; }

        public int[] Registers { get; set; }

        public int ThreadId { get; set; }

        public int NumberOfCycles { get; set; }

        public bool HasPriority { get; set; }

        public override string ToString()
        {
            // Gathers statistical information from each context.
            var builder = new StringBuilder();
            builder.AppendLine("Thread: " + ThreadId);
            builder.AppendLine("Number of Cycles consumed: " + NumberOfCycles);
            for (var i = 0; i < Registers.Length; i++)
            {
                builder.AppendLine("R" + i + " : " + Registers[i]);
            }
            return builder.ToString();
        }
    }
}