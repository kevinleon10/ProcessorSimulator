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
                case 2: // JR
                    actualContext.ProgramCounter = actualContext.Registers[actualInstruction.Source];
                    break;
                case 3: //JAL
                    actualContext.Registers[31] = actualContext.ProgramCounter;
                    actualContext.ProgramCounter += actualInstruction.Inmediate;
                    break;
                case 4: // BEQZ
                    if (actualContext.Registers[actualInstruction.Source] == 0)
                    {
                        actualContext.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }
                    break;
                case 5: // BNEZ
                    if (actualContext.Registers[actualInstruction.Source] != 0)
                    {
                        actualContext.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }
                    break;
                case 8: //DADDI
                    actualContext.Registers[actualInstruction.Destiny] = actualContext.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    break;
                case 12: //DMUL
                    actualContext.Registers[actualInstruction.Destiny] = 
                        actualContext.Registers[actualInstruction.Source] + actualContext.Registers[actualInstruction.Inmediate];
                    break;
                case 14: //DDIV
                    actualContext.Registers[actualInstruction.Destiny] = 
                        actualContext.Registers[actualInstruction.Source] / actualContext.Registers[actualInstruction.Inmediate];
                    break;
                case 32: //DADD
                    actualContext.Registers[actualInstruction.Destiny] = 
                        actualContext.Registers[actualInstruction.Source] + actualContext.Registers[actualInstruction.Inmediate];
                    break;
                case 34: //DSUB
                    actualContext.Registers[actualInstruction.Destiny] = 
                        actualContext.Registers[actualInstruction.Source] - actualContext.Registers[actualInstruction.Inmediate];
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