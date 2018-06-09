using System.Collections.Generic;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.processor;

namespace ProcessorSimulator.core
{
    public class DobleCore : Core
    {
        public DobleCore(int cacheSize, Mutex instructionMutexBus, Mutex dataMutexBus) : base(cacheSize, instructionMutexBus, dataMutexBus)
        {
            InstructionRegisterTwo = new Instruction();
            ContextTwo = new Context();
            RemainingThreadCyclesTwo = 0;
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