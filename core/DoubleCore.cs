﻿using System.Collections.Generic;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;

namespace ProcessorSimulator.core
{
    public class DobleCore : Core
    {
        public DobleCore(Cache<Instruction> instructionCache, Cache<int> dataCache, int quantum) : base(instructionCache, dataCache, quantum)
        {
            InstructionRegisterTwo = null;
            RemainingThreadCyclesTwo = quantum;
            ThreadStatusTwo = ThreadStatus.Stopped;
            Reservations = new List<Reservation>();
        }

        public Instruction InstructionRegisterTwo { get; set; }

        public Context ContextTwo { get; set; }

        public int RemainingThreadCyclesTwo { get; set; }

        public ThreadStatus ThreadStatusTwo { get; set; }
        
        public List<Reservation> Reservations { get; set; }

    }
}