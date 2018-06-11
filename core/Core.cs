using System;
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

        private void ExecuteInstruction(int operationCode, int source, int destiny, int inmediate, Context actualContext)
        {
            switch (operationCode)
            {
                case 2:
                    actualContext.Jr(source);
                    break;
                case 3:
                    actualContext.Jal(inmediate);
                    break;
                case 4:
                    actualContext.Beqz(source, inmediate);
                    break;
                case 5:
                    actualContext.Bnez(source, inmediate);
                    break;
                case 8:
                    actualContext.Daddi(source, destiny, inmediate);
                    break;
                case 12:
                    actualContext.Dmul(source, destiny, inmediate);
                    break;
                case 14:
                    actualContext.Ddiv(source, destiny, inmediate);
                    break;
                case 32:
                    actualContext.Dadd(source, destiny, inmediate);
                    break;
                case 34:
                    actualContext.Dsub(source, destiny, inmediate);
                    break;
                case 35:
                    // TODO LOAD
                case 43:
                    // TODO STORE
                    break;
                default:
                    // TODO FIN
                    break;
            }
        }
    }
}