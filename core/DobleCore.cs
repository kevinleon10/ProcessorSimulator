using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.processor;

namespace ProcessorSimulator.core
{
    public class DobleCore : Core
    {
        public DobleCore(int cacheSize, Mutex instructionMutexBus, Mutex dataMutexBus, Processor processor) : base(cacheSize, instructionMutexBus, dataMutexBus, processor)
        {
            InstructionRegisterTwo = new Instruction();
            ContextTwo = new Context();
            RemainingThreadCyclesTwo = 0;
            ThreadStatusTwo = ThreadStatus.Stopped;
        }

        public Instruction InstructionRegisterTwo { get; set; }

        public Context ContextTwo { get; set; }

        public int RemainingThreadCyclesTwo { get; set; }

        public ThreadStatus ThreadStatusTwo { get; set; }
        
        public Reservation[] Reservations { get; set; }

    }
}