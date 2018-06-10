﻿using System;
 using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;

namespace ProcessorSimulator.core
{
    public class Core
    {
        public Core(Cache<Instruction> instructionCache, Cache<int> dataCache, int cacheSize)
        {
            CacheSize = cacheSize;
            InstructionRegister = null;
            InstructionCache = instructionCache;
            DataCache = dataCache;
            RemainingThreadCycles = 0;
            ThreadStatus = ThreadStatus.Stopped;
        }

        public Instruction InstructionRegister { get; set; }

        public Context Context { get; set; }

        public Cache<Instruction> InstructionCache { get; set; }

        public Cache<int> DataCache { get; set; }

        public int RemainingThreadCycles { get; set; }

        public ThreadStatus ThreadStatus { get; set; }

        public int CacheSize { get; set; }
        
        public void StartExecution(Context context)
        {
            Context = context;
            int programCounter = Context.ProgramCounter;
            int blockNumberInCache = (programCounter / 16) % CacheSize;
            Console.WriteLine(blockNumberInCache);
        }
    }
}