using System;
using System.ComponentModel;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.common;

namespace ProcessorSimulator.core
{
    public class Core
    {
        public Core(Cache<Instruction> instructionCache, Cache<int> dataCache, int cacheSize)
        {
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

        private int GetBlockNumberInMemory(int address)
        {
            return (address / Constants.BytesInBlock);
        }
        
        private int GetWordNumberInBlock(int address)
        {
            return (address % Constants.BytesInBlock) / Constants.WordsInBlock;
        }

        public void StartExecution(Context context, bool isDoubleCore)
        {
            Context = context;
            var programCounter = Context.ProgramCounter;
            
            //Instruction fetch
            var blockNumberInMemory = GetBlockNumberInMemory(programCounter);
            var wordNumberInBlock = GetWordNumberInBlock(programCounter);
            Console.WriteLine(blockNumberInMemory);
            Console.WriteLine(wordNumberInBlock);
            InstructionRegister = InstructionCache.GetWord(blockNumberInMemory, wordNumberInBlock, isDoubleCore);
        }

        private void ExecuteInstruction(Context actualContext, Instruction actualInstruction)
        {
            switch (actualInstruction.OperationCode)
            {
                case 2:
                    actualContext.Jr(actualInstruction.Source);
                    break;
                case 3:
                    actualContext.Jal(actualInstruction.Inmediate);
                    break;
                case 4:
                    actualContext.Beqz(actualInstruction.Source, actualInstruction.Inmediate);
                    break;
                case 5:
                    actualContext.Bnez(actualInstruction.Source, actualInstruction.Inmediate);
                    break;
                case 8:
                    actualContext.Daddi(actualInstruction.Source, actualInstruction.Destiny ,actualInstruction.Inmediate);
                    break;
                case 12:
                    actualContext.Dmul(actualInstruction.Source, actualInstruction.Destiny ,actualInstruction.Inmediate);
                    break;
                case 14:
                    actualContext.Ddiv(actualInstruction.Source, actualInstruction.Destiny ,actualInstruction.Inmediate);
                    break;
                case 32:
                    actualContext.Dadd(actualInstruction.Source, actualInstruction.Destiny ,actualInstruction.Inmediate);
                    break;
                case 34:
                    actualContext.Dsub(actualInstruction.Source, actualInstruction.Destiny ,actualInstruction.Inmediate);
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