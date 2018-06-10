﻿namespace ProcessorSimulator.core
{
    public class Context
    {
        public Context(int programCounter, int threadId)
        {
            ProgramCounter = programCounter;
            ThreadId = threadId;
            NumberOfCycles = 0;
            HasPriority = false;
        }

        public int ProgramCounter { get; set; }

        public int[] Registers { get; set; }

        public int ThreadId { get; set; }

        public int NumberOfCycles { get; set; }

        public bool HasPriority { get; set; }
    }
}