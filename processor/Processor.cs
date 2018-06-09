using System.Collections.Generic;
using System.Threading;
using ProcessorSimulator.core;

namespace ProcessorSimulator.processor
{
    public class Processor
    {
        public Processor(int quantum)
        {
            Mutex dataMutexBus = new Mutex();
            Mutex instructionMutexBus = new Mutex();
            CoreOne = new Core(quantum, instructionMutexBus, dataMutexBus, this);
            CoreZero = new DobleCore(quantum, instructionMutexBus, dataMutexBus, this);
            Clock = 0;
            ClockBarrier = new Barrier(0);
            ContextQueue = new Queue<Context>();
            Quantum = quantum;
        }
        
        public DobleCore CoreZero { get; set; }

        public Core CoreOne { get; set; }

        public int Clock { get; set; }

        public Barrier ClockBarrier { get; set; }

        public Queue<Context> ContextQueue { get; set; }

        public int Quantum { get; set; }
    }
}