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

            //First instruction fetch
            InstructionRegister = LoadInstruction(programCounter);

            //Execute every instruction in the thread until it obtains an end instruction
            while (InstructionRegister.OperationCode != Constants.EndOperationCode)
            {
                programCounter += Constants.BytesInWord;
                ExecuteInstruction(InstructionRegister);
                //Instruction fetch
                InstructionRegister = LoadInstruction(programCounter);
            }
        }

        /// <summary>
        /// Load a instruction
        /// </summary>
        /// <returns>
        /// The resulting instruction
        /// </returns>
        private Instruction LoadInstruction(int programCounter)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(programCounter);
            var wordNumberInBlock = GetWordNumberInBlock(programCounter);
            Console.WriteLine("Block number in memory: " + blockNumberInMemory);
            Console.WriteLine("Word number in block: " + wordNumberInBlock);
            var instruction = new Instruction();
            var blockNumberInCache = blockNumberInMemory % InstructionCache.CacheSize;
            Console.WriteLine("Block number in cache: " + blockNumberInCache);
            var hasTakenBlock = false;
            // While it has not gotten the block it continues asking for
            while (!hasTakenBlock)
            {
                // Try lock
                if (Monitor.TryEnter(InstructionCache.Blocks[blockNumberInCache]))
                {
                    try
                    {
                        hasTakenBlock = true;
                        // If the label matches with the block number
                        if (InstructionCache.Blocks[blockNumberInCache].Label == blockNumberInMemory)
                        {
                            instruction = InstructionCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                            Console.WriteLine("I could take the block");
                        }
                        else
                        {
                            var blockNumberInOtherCache = blockNumberInMemory % InstructionCache.OtherCache.CacheSize;
                            var hasTakenBus = false;
                            // While it has not gotten the bus it continues asking for
                            while (!hasTakenBus)
                            {
                                // Try lock
                                if (Monitor.TryEnter(InstructionBus.Instance))
                                {
                                    try
                                    {
                                        hasTakenBus = true;
                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                        var hasTakenOtherCacheBlock = false;
                                        // While it has not gotten the other cache block it continues asking for
                                        while (!hasTakenOtherCacheBlock)
                                        {
                                            // Try lock
                                            if (Monitor.TryEnter(
                                                InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                            {
                                                try
                                                {
                                                    hasTakenOtherCacheBlock = true;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();

                                                    // If the label matches with the block number it will be replaced the current block
                                                    if (InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                            .Label == blockNumberInMemory)
                                                    {
                                                        InstructionCache.Blocks[blockNumberInCache]
                                                            .Words = InstructionCache.OtherCache
                                                            .Blocks[blockNumberInOtherCache].Words;
                                                        instruction = InstructionCache.Blocks[blockNumberInCache]
                                                            .Words[wordNumberInBlock];
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Console.WriteLine(
                                                            "I could take the block from the other cache");
                                                    }
                                                    else // It has to bring it from memory
                                                    {
                                                        InstructionCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadInstructionBlock(programCounter).Words;
                                                        instruction = InstructionCache.Blocks[blockNumberInCache]
                                                            .Words[wordNumberInBlock];
                                                        // Add forty cycles
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    // Ensure that the lock is released.
                                                    Monitor.Exit(
                                                        InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]);
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

        private void ExecuteInstruction(Instruction actualInstruction)
        {
            switch (actualInstruction.OperationCode)
            {
                case Constants.JROperationCode:
                    Context.ProgramCounter = Context.Registers[actualInstruction.Source];
                    break;

                case Constants.JALOperationCode:
                    Context.Registers[31] = Context.ProgramCounter;
                    Context.ProgramCounter += actualInstruction.Inmediate;
                    break;

                case Constants.BEQZOperationCode:
                    if (Context.Registers[actualInstruction.Source] == 0)
                    {
                        Context.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;

                case Constants.BNEZOperationCode:
                    if (Context.Registers[actualInstruction.Source] != 0)
                    {
                        Context.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;

                case Constants.DADDIOperationCode:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    break;
                case Constants.DMULOperationCode:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] +
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case Constants.DDIVOperationCode:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] /
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case Constants.DADDOperationCode:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] +
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case Constants.DSUBOperationCode:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] -
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case Constants.LWOperationCode:
                    var address = Context.Registers[actualInstruction.Source] +
                                  Context.Registers[actualInstruction.Inmediate];
                    Context.Registers[actualInstruction.Destiny] = LoadData(address);
                    break;
                case Constants.SWOperationCode:
                    // TODO STORE
                    break;
            }
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <returns>
        /// The resulting data
        /// </returns>
        private int LoadData(int address)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            Console.WriteLine("Block number in memory: " + blockNumberInMemory);
            Console.WriteLine("Word number in block: " + wordNumberInBlock);
            var wordData = 0;
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
            Console.WriteLine("Block number in cache: " + blockNumberInCache);
            var hasFinishedLoad = false;
            // Wwhile it has not finished the load it continues trying
            while (!hasFinishedLoad)
            {
                // Try lock
                if (Monitor.TryEnter(DataCache.Blocks[blockNumberInCache]))
                {
                    try
                    {
                        // If the label matches with the block number and it is not invalid
                        if (DataCache.Blocks[blockNumberInCache].Label == blockNumberInMemory &&
                            DataCache.Blocks[blockNumberInCache].BlockState != BlockState.Invalid)
                        {
                            wordData = DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock];
                            hasFinishedLoad = true;
                            Console.WriteLine("I could take the block");
                        }
                        else // It tryes to get the bus
                        {
                            // Try lock
                            if (Monitor.TryEnter(DataBus.Instance))
                            {
                                try
                                {
                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                    // If the label does not match with the block number and it is invalid it wil store the block in memory
                                    if (DataCache.Blocks[blockNumberInCache].Label != blockNumberInMemory &&
                                        DataCache.Blocks[blockNumberInCache].BlockState == BlockState.Invalid)
                                    {
                                        Memory.Instance.StoreDataBlock(address,
                                            DataCache.Blocks[blockNumberInCache].Words);
                                        // Add forty cycles
                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                        {
                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                        }
                                    }

                                    var blockNumberInOtherCache = blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                    // Try lock
                                    if (Monitor.TryEnter(DataCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                    {
                                        try
                                        {
                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                            // If the label matches with the block number it will be replaced the current block
                                            var currentOtherCacheBlock =
                                                DataCache.OtherCache.Blocks[blockNumberInOtherCache];
                                            if (currentOtherCacheBlock.Label ==
                                                blockNumberInMemory &&
                                                currentOtherCacheBlock.BlockState ==
                                                BlockState.Modified)
                                            {
                                                DataCache.OtherCache.Blocks[blockNumberInOtherCache].BlockState = BlockState.Shared;
                                                Memory.Instance.StoreDataBlock(address, currentOtherCacheBlock.Words);
                                                // Add forty cycles
                                                for (var i = 0; i < Constants.CyclesMemory; i++)
                                                {
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                }

                                                DataCache.Blocks[blockNumberInCache].Words =
                                                    Memory.Instance.LoadDataBlock(address).Words;
                                                wordData = DataCache.Blocks[blockNumberInCache]
                                                    .Words[wordNumberInBlock];
                                                hasFinishedLoad = true;
                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                Console.WriteLine("I could take the block from the other cache");
                                            }
                                            else // It will bring it from memory
                                            {
                                                DataCache.Blocks[blockNumberInCache].Words =
                                                    Memory.Instance.LoadDataBlock(address).Words;
                                                wordData = DataCache.Blocks[blockNumberInCache]
                                                    .Words[wordNumberInBlock];
                                                for (var i = 0; i < Constants.CyclesMemory; i++)
                                                {
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                }
                                                hasFinishedLoad = true;
                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                            }
                                        }
                                        finally
                                        {
                                            // Ensure that the lock is released.
                                            Monitor.Exit(DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
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