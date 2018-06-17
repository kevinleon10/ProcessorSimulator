using System;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.common;
using ProcessorSimulator.memory;
using ProcessorSimulator.processor;

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
            
            //Execute every instruction in the thread until it obtains an end instruction
            while (ExecuteInstruction(InstructionRegister))
            {
                programCounter += Constants.BytesInWord;
                InstructionRegister = LoadInstruction(programCounter);
            }

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
            var blockNumberInCache = blockNumberInMemory % InstructionCache.CacheSize;
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
                        if (InstructionCache.Blocks[blockNumberInCache].Label == blockNumberInMemory)
                        {
                            instruction = InstructionCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                            Console.WriteLine("I could take the block");
                        }
                        else
                        {
                            var blockNumberInOtherCache = blockNumberInMemory % InstructionCache.OtherCache.CacheSize;
                            var hasGottenBus = false;
                            while (!hasGottenBus)
                            {
                                if (Monitor.TryEnter(InstructionBus.Instance))
                                {
                                    try
                                    {
                                        hasGottenBus = true;
                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                        var hasGottenOtherBlock = false;
                                        while (!hasGottenOtherBlock)
                                        {
                                            if (Monitor.TryEnter(InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                            {
                                                try
                                                {
                                                    hasGottenOtherBlock = true;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    if (InstructionCache.OtherCache.Blocks[blockNumberInOtherCache].Label == blockNumberInMemory)
                                                    {
                                                        InstructionCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] = InstructionCache.OtherCache.Blocks[blockNumberInOtherCache].Words[wordNumberInBlock];
                                                        instruction = InstructionCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Console.WriteLine("I could take the block from the other cache");
                                                    }
                                                    else
                                                    {
                                                        InstructionCache.Blocks[blockNumberInCache].Words = Memory.Instance.GetInstructionBlock(programCounter).Words;
                                                        instruction = InstructionCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    Monitor.Exit(InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        // Ensure that the lock is released.
                                        Monitor.Exit(InstructionBus.Instance);
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

        private bool ExecuteInstruction(Instruction actualInstruction)
        {
            var canExecuteMoreInstructions = true;
            switch (actualInstruction.OperationCode)
            {
                case 2: // JR
                    Context.ProgramCounter = Context.Registers[actualInstruction.Source];
                    break;
                case 3: //JAL
                    Context.Registers[31] = Context.ProgramCounter;
                    Context.ProgramCounter += actualInstruction.Inmediate;
                    break;
                case 4: // BEQZ
                    if (Context.Registers[actualInstruction.Source] == 0)
                    {
                        Context.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;
                case 5: // BNEZ
                    if (Context.Registers[actualInstruction.Source] != 0)
                    {
                        Context.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;
                case 8: //DADDI
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    break;
                case 12: //DMUL
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] +
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case 14: //DDIV
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] /
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case 32: //DADD
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] +
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case 34: //DSUB
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] -
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case 35:
                    var address = Context.Registers[actualInstruction.Source] +
                                  Context.Registers[actualInstruction.Inmediate];
                    Context.Registers[actualInstruction.Destiny] = LoadData(address);
                    break;
                case 43:
                    // TODO STORE
                    break;
                default:
                    canExecuteMoreInstructions = false;
                    break;
            }

            return canExecuteMoreInstructions;
        }
        
        /// <summary>
        /// Load data
        /// </summary>
        /// <returns>
        /// The resulting data
        /// </returns>
        private int LoadData(int  address)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            Console.WriteLine("Block number in memory: " + blockNumberInMemory);
            Console.WriteLine("Word number in block: " + wordNumberInBlock);
            var wordData = 0;
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
            Console.WriteLine("Block number in cache: " + blockNumberInCache);
            var hasGottenBlock = false;
            //while it has not gotten the block it continues asking for
            while (!hasGottenBlock)
            {
                if (Monitor.TryEnter(DataCache.Blocks[blockNumberInCache]))
                {
                    try
                    {
                        hasGottenBlock = true;
                        //if the label matches with the block number
                        if (DataCache.Blocks[blockNumberInCache].Label == blockNumberInMemory)
                        {
                            wordData = DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                            Console.WriteLine("I could take the block");
                        }
                        else
                        {
                            var blockNumberInOtherCache = blockNumberInMemory % DataCache.OtherCache.CacheSize;
                            var hasGottenBus = false;
                            while (!hasGottenBus)
                            {
                                if (Monitor.TryEnter(DataBus.Instance))
                                {
                                    try
                                    {
                                        hasGottenBus = true;
                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                        var hasGottenOtherBlock = false;
                                        while (!hasGottenOtherBlock)
                                        {
                                            if (Monitor.TryEnter(DataCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                            {
                                                try
                                                {
                                                    hasGottenOtherBlock = true;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    if (DataCache.OtherCache.Blocks[blockNumberInOtherCache].Label == blockNumberInMemory)
                                                    {
                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] = DataCache.OtherCache.Blocks[blockNumberInOtherCache].Words[wordNumberInBlock];
                                                        wordData = DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Console.WriteLine("I could take the block from the other cache");
                                                    }
                                                    else
                                                    {
                                                        DataCache.Blocks[blockNumberInCache].Words = Memory.Instance.GetDataBlock(address).Words;
                                                        wordData = DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    Monitor.Exit(DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        // Ensure that the lock is released.
                                        Monitor.Exit(DataBus.Instance);
                                    }
                                }
                            }
                        }

                        // The critical section.
                    }
                    finally
                    {
                        // Ensure that the lock is released.
                        Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                    }
                }
            }
            return wordData;
        }
    }
}