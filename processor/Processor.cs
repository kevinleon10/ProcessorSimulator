using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using ProcessorSimulator.core;

namespace ProcessorSimulator.processor
{
    public class Processor
    {
        public Processor(int quantum)
        {
            Mutex DataMutexBus = new Mutex();
            Mutex InstructionMutexBus = new Mutex();
            CoreOne = new Core(quantum, InstructionMutexBus, DataMutexBus, this);
            DobleCore CoreZero = new DobleCore(quantum, InstructionMutexBus, DataMutexBus, this);
            Clock = 0;
            ClockBarrier = new Barrier(0);
            ContextQueue = new Queue<Context>();
            Quantum = quantum;
        }

        public Core CoreZero { get; }

        public Core CoreOne { get; }

        public int Clock { get; }

        public Barrier ClockBarrier { get; }

        public Queue<Context> ContextQueue { get; }

        public int Quantum { get; set; }
    }
}