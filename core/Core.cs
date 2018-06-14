using System;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.common;

namespace ProcessorSimulator.core
{
    public class Core
    {
        public Core(Cache<Instruction> instructionCache, Cache<int> dataCache)
        {
            InstructionRegister = null;
            InstructionCache = instructionCache;
            DataCache = dataCache;
            RemainingThreadCycles = Constants.NotRunningAnyThread;
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

        public void StartExecution(Context context)
        {
            Context = context;
            var programCounter = Context.ProgramCounter;

            //Instruction fetch
            InstructionRegister = LoadInstruction(programCounter);
        }

        /// <summary>
        /// Load a instruction
        /// </summary>
        /// <returns>
        /// The resulting instruction
        /// </returns>
        private Instruction LoadInstruction(int  programCounter)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(programCounter);
            var wordNumberInBlock = GetWordNumberInBlock(programCounter);
            Console.WriteLine("Block number in memory: " + blockNumberInMemory);
            Console.WriteLine("Word number in block: " + wordNumberInBlock);
            var instruction = new Instruction();
            var blockNumberInCache = blockNumberInMemory % Constants.CoreOneCacheSize;
            Console.WriteLine("Block number in cache: " + blockNumberInCache);
            var hasGottenBlock = false;
            //while it has not gotten the block it continues asking for
            while (!hasGottenBlock)
            {
                if (Monitor.TryEnter(InstructionCache.Blocks[blockNumberInCache]))
                {
                    try
                    {
                        hasGottenBlock = true;
                        //if the label matches with the block number
                        var localBlock = InstructionCache.Blocks[blockNumberInCache];
                        if (localBlock.Label == blockNumberInMemory)
                        {
                            instruction = InstructionCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                            Console.WriteLine("I could take the block");
                        }
                        else
                        {
                            var blockNumberInOtherCache = blockNumberInMemory % Constants.CoreZeroCacheSize;
                            var hasGottenOtherBlock = false;
                            while (!hasGottenOtherBlock)
                            {
                                if (Monitor.TryEnter(InstructionCache.OtherCache.Bus))
                                {
                                    try
                                    {
                                    }
                                    finally
                                    {
                                        // Ensure that the lock is released.
                                        Monitor.Exit(InstructionCache.OtherCache.Bus);
                                    }
                                }
                            }
                        }

                        // The critical section.
                    }
                    finally
                    {
                        // Ensure that the lock is released.
                        Monitor.Exit(InstructionCache.Blocks[blockNumberInCache]);
                    }
                }
            }

            return instruction;
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
                    actualContext.Registers[actualInstruction.Destiny] =
                        actualContext.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    break;
                case 12: //DMUL
                    actualContext.Registers[actualInstruction.Destiny] =
                        actualContext.Registers[actualInstruction.Source] +
                        actualContext.Registers[actualInstruction.Inmediate];
                    break;
                case 14: //DDIV
                    actualContext.Registers[actualInstruction.Destiny] =
                        actualContext.Registers[actualInstruction.Source] /
                        actualContext.Registers[actualInstruction.Inmediate];
                    break;
                case 32: //DADD
                    actualContext.Registers[actualInstruction.Destiny] =
                        actualContext.Registers[actualInstruction.Source] +
                        actualContext.Registers[actualInstruction.Inmediate];
                    break;
                case 34: //DSUB
                    actualContext.Registers[actualInstruction.Destiny] =
                        actualContext.Registers[actualInstruction.Source] -
                        actualContext.Registers[actualInstruction.Inmediate];
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