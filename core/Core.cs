﻿using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.processor;

namespace ProcessorSimulator.core
{
    public class Core
    {
        public Core(int cacheSize, Mutex instructionMutexBus, Mutex dataMutexBus, Processor processor)
        {
            InstructionRegister = new Instruction();
            Context = new Context();
            InstructionCache = new Cache<Instruction>(cacheSize, instructionMutexBus);
            DataCache = new Cache<int>(cacheSize, dataMutexBus);
            RemainingThreadCycles = 0;
            ThreadStatus = ThreadStatus.Stopped;
            Processor = processor;
        }

        public Instruction InstructionRegister { get; set; }

        public Context Context { get; set; }

        public Cache<Instruction> InstructionCache { get; set; }

        public Cache<int> DataCache { get; set; }

        public int RemainingThreadCycles { get; set; }

        public ThreadStatus ThreadStatus { get; set; }

        public Processor Processor { get; set; }
    }
}