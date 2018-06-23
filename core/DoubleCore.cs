using System;
using System.Collections.Generic;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.common;
using ProcessorSimulator.memory;
using ProcessorSimulator.processor;

namespace ProcessorSimulator.core
{
    public class DobleCore : Core
    {
        public DobleCore(Cache<Instruction> instructionCache, Cache<int> dataCache) : base(instructionCache, dataCache)
        {
            InstructionRegister = null;
            RemainingThreadCycles = Constants.NotRunningAnyThread;
            ThreadStatusTwo = ThreadStatus.Stopped;
            Reservations = new List<Reservation>();
        }

        public Instruction InstructionRegisterTwo { get; set; }

        public Context ContextTwo { get; set; }

        public int RemainingThreadCyclesTwo { get; set; }

        public ThreadStatus ThreadStatusTwo { get; set; }

        public List<Reservation> Reservations { get; set; }

        protected override int LoadData(int address)
        {
            Console.WriteLine("LOAAAAAAAAAAAD");
            return address;
        }

        protected override void StoreData(int address, int newData)
        {
            Console.WriteLine("STOREEEEEEEE");
        }
    }
}