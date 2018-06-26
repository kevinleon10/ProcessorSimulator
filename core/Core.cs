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
            RemainingThreadCycles = new int[Constants.ThreadsInCoreOne];
            Contexts = new Context[Constants.ThreadsInCoreOne];
            ThreadStatuses = new ThreadStatus[Constants.ThreadsInCoreOne];
            RemainingThreadCycles[Constants.FirstContextIndex] = Constants.NotRunningAnyThread;
        }

        protected Instruction InstructionRegister { get; set; }

        public Context[] Contexts { get; set; }

        protected Cache<Instruction> InstructionCache { get; set; }

        public Cache<int> DataCache { get; set; }

        public int[] RemainingThreadCycles { get; set; }

        public ThreadStatus[] ThreadStatuses { get; set; }

        protected int GetBlockNumberInMemory(int address)
        {
            return (address / Constants.BytesInBlock);
        }

        protected int GetWordNumberInBlock(int address)
        {
            return (address % Constants.BytesInBlock) / Constants.WordsInBlock;
        }

        public void StartExecution(Context context, int contextIndex)
        {
            Contexts[contextIndex] = context;
            RemainingThreadCycles[contextIndex] = Processor.Instance.Quantum;

            //First instruction fetch
            InstructionRegister = LoadInstruction(contextIndex);

            //Execute every instruction in the thread until it obtains an end instruction
            while (InstructionRegister.OperationCode != (int) Operation.End)
            {
                Contexts[contextIndex].ProgramCounter += Constants.BytesInWord;
                ExecuteInstruction(InstructionRegister, contextIndex);
                //Instruction fetch
                InstructionRegister = LoadInstruction(contextIndex);
            }

            ThreadStatuses[contextIndex] = ThreadStatus.Ended;
        }

        /// <summary>
        /// Load a instruction
        /// </summary>
        /// <returns>
        /// The resulting instruction
        /// </returns>
        private Instruction LoadInstruction(int contextIndex)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(Contexts[contextIndex].ProgramCounter);
            var wordNumberInBlock = GetWordNumberInBlock(Contexts[contextIndex].ProgramCounter);
            var instruction = new Instruction();
            var blockNumberInCache = blockNumberInMemory % InstructionCache.CacheSize;
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
                            Contexts[contextIndex].NumberOfCycles++;
                            RemainingThreadCycles[contextIndex]--;
                            if (RemainingThreadCycles[contextIndex] == 0)
                            {
                                Monitor.Exit(InstructionCache.Blocks[blockNumberInCache]);
                            }

                            //Processor.Instance.ClockBarrier.SignalAndWait();
                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                        }
                        else
                        {
                            ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;
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
                                        //ProcessorInstance.ClockBarrier.SignalAndWait();
                                        //ProcessorInstance.ProcessorBarrier.SignalAndWait();
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
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    InstructionCache.Blocks[blockNumberInCache].Label =
                                                        blockNumberInMemory;
                                                    // If the label matches with the block number it will be replaced the current block
                                                    if (InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                            .Label == blockNumberInMemory)
                                                    {
                                                        InstructionCache.Blocks[blockNumberInCache]
                                                            .Words = Memory.Instance.LoadInstructionBlock(
                                                            blockNumberInMemory
                                                        );
                                                        instruction = InstructionCache.Blocks[blockNumberInCache]
                                                            .Words[wordNumberInBlock];
                                                        Contexts[contextIndex].NumberOfCycles++;
                                                        RemainingThreadCycles[contextIndex]--;
                                                        if (RemainingThreadCycles[contextIndex] == 0)
                                                        {
                                                            Monitor.Exit(InstructionCache.Blocks[blockNumberInCache]);
                                                            Monitor.Exit(DataBus.Instance);
                                                            Monitor.Exit(
                                                                InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                        }

                                                        ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }
                                                    else // It has to bring it from memory
                                                    {
                                                        //Release the lock in other cache because it is not needed
                                                        Monitor.Exit(
                                                            InstructionCache.OtherCache.Blocks[
                                                                blockNumberInOtherCache]);
                                                        InstructionCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadInstructionBlock(
                                                                blockNumberInMemory);
                                                        instruction = InstructionCache.Blocks[blockNumberInCache]
                                                            .Words[wordNumberInBlock];
                                                        // Add forty cycles
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }

                                                        Contexts[contextIndex].NumberOfCycles++;
                                                        RemainingThreadCycles[contextIndex]--;
                                                        if (RemainingThreadCycles[contextIndex] == 0)
                                                        {
                                                            Monitor.Exit(InstructionCache.Blocks[blockNumberInCache]);
                                                            Monitor.Exit(DataBus.Instance);
                                                        }

                                                        ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }
                                                }
                                                finally
                                                {
                                                    // Ensure that the lock is released.
                                                    if (Monitor.IsEntered(
                                                        InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                                    {
                                                        Monitor.Exit(
                                                            InstructionCache.OtherCache.Blocks[
                                                                blockNumberInOtherCache]);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                //Processor.Instance.ClockBarrier.SignalAndWait();
                                                //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        // Ensure that the lock is released.
                                        if (Monitor.IsEntered(InstructionBus.Instance))
                                        {
                                            Monitor.Exit(InstructionBus.Instance);
                                        }
                                    }
                                }
                                else
                                {
                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                }
                            }
                        }

                        // The critical section.
                    }
                    finally
                    {
                        // Ensure that the lock is released.
                        if (Monitor.IsEntered(InstructionCache.Blocks[blockNumberInCache]))
                        {
                            Monitor.Exit(InstructionCache.Blocks[blockNumberInCache]);
                        }
                    }
                }
                else
                {
                    //Processor.Instance.ClockBarrier.SignalAndWait();
                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            return instruction;
        }

        private void ExecuteInstruction(Instruction actualInstruction, int contextIndex)
        {
            int address;
            switch (actualInstruction.OperationCode)
            {
                case (int) Operation.Jr:
                    Contexts[contextIndex].ProgramCounter = Contexts[contextIndex].Registers[actualInstruction.Source];
                    break;

                case (int) Operation.Jal:
                    Contexts[contextIndex].Registers[31] = Contexts[contextIndex].ProgramCounter;
                    Contexts[contextIndex].ProgramCounter += actualInstruction.Inmediate;
                    break;

                case (int) Operation.Beqz:
                    if (Contexts[contextIndex].Registers[actualInstruction.Source] == 0)
                    {
                        Contexts[contextIndex].ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;

                case (int) Operation.Bnez:
                    if (Contexts[contextIndex].Registers[actualInstruction.Source] != 0)
                    {
                        Contexts[contextIndex].ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;

                case (int) Operation.Daddi:
                    Contexts[contextIndex].Registers[actualInstruction.Destiny] =
                        Contexts[contextIndex].Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    break;
                case (int) Operation.Dmul:
                    Contexts[contextIndex].Registers[actualInstruction.Inmediate] =
                        Contexts[contextIndex].Registers[actualInstruction.Source] *
                        Contexts[contextIndex].Registers[actualInstruction.Destiny];
                    break;
                case (int) Operation.Ddiv:
                    Contexts[contextIndex].Registers[actualInstruction.Inmediate] =
                        Contexts[contextIndex].Registers[actualInstruction.Source] /
                        Contexts[contextIndex].Registers[actualInstruction.Destiny];
                    break;
                case (int) Operation.Dadd:
                    Contexts[contextIndex].Registers[actualInstruction.Inmediate] =
                        Contexts[contextIndex].Registers[actualInstruction.Source] +
                        Contexts[contextIndex].Registers[actualInstruction.Destiny];
                    break;
                case (int) Operation.Dsub:
                    Contexts[contextIndex].Registers[actualInstruction.Inmediate] =
                        Contexts[contextIndex].Registers[actualInstruction.Source] -
                        Contexts[contextIndex].Registers[actualInstruction.Destiny];
                    break;
                case (int) Operation.Lw:
                    address = Contexts[contextIndex].Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    if (address >= 0 && address < Constants.BytesInMemoryDataBlocks)
                    {
                        Contexts[contextIndex].Registers[actualInstruction.Destiny] = LoadData(address, contextIndex);
                    }
                    else
                    {
                        Console.WriteLine(Constants.AddressError + actualInstruction);
                    }

                    break;
                case (int) Operation.Sw:
                    address = Contexts[contextIndex].Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    if (address >= 0 && address < Constants.BytesInMemoryDataBlocks)
                    {
                        StoreData(address, Contexts[contextIndex].Registers[actualInstruction.Destiny], contextIndex);
                    }
                    else
                    {
                        Console.WriteLine(Constants.AddressError + actualInstruction);
                    }

                    break;
                default:
                    Console.WriteLine("Instruction " + actualInstruction.OperationCode + " has not been recognised.");
                    break;
            }
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <returns>
        /// The resulting data
        /// </returns>
        protected virtual int LoadData(int address, int contextIndex)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            var wordData = 0;
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
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
                        var currentBlock = DataCache.Blocks[blockNumberInCache];
                        if (currentBlock.Label == blockNumberInMemory &&
                            currentBlock.BlockState != BlockState.Invalid)
                        {
                            wordData = currentBlock.Words[wordNumberInBlock];
                            Contexts[contextIndex].NumberOfCycles++;
                            RemainingThreadCycles[contextIndex]--;
                            if (RemainingThreadCycles[contextIndex] == 0)
                            {
                                Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                            }

                            hasFinishedLoad = true;
                            //Processor.Instance.ClockBarrier.SignalAndWait();
                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                        }
                        else // It tryes to get the bus
                        {
                            // Try lock
                            if (Monitor.TryEnter(DataBus.Instance))
                            {
                                try
                                {
                                    ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;
                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                    // If the label does not match with the block number and it is modified it will store the block in memory
                                    if (currentBlock.Label != blockNumberInMemory &&
                                        currentBlock.BlockState == BlockState.Modified)
                                    {
                                        Memory.Instance.StoreDataBlock(currentBlock.Label,
                                            currentBlock.Words);
                                        // Add forty cycles
                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                        {
                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                        }
                                    }

                                    var blockNumberInOtherCache = blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                    // Try lock
                                    var hasTakenOtherBlock = false;
                                    while (!hasTakenOtherBlock)
                                    {
                                        if (Monitor.TryEnter(DataCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                        {
                                            try
                                            {
                                                hasTakenOtherBlock = true;
                                                //Processor.Instance.ClockBarrier.SignalAndWait();
                                                //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                DataCache.Blocks[blockNumberInCache].Label = blockNumberInMemory;
                                                // If the label matches with the block number it will be replaced the current block
                                                var otherCacheBlock =
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache];
                                                if (otherCacheBlock.Label ==
                                                    blockNumberInMemory &&
                                                    otherCacheBlock.BlockState ==
                                                    BlockState.Modified)
                                                {
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache].BlockState =
                                                        BlockState.Shared;
                                                    Memory.Instance.StoreDataBlock(blockNumberInMemory,
                                                        otherCacheBlock.Words);
                                                    DataCache.Blocks[blockNumberInCache].Words =
                                                        Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                    DataCache.Blocks[blockNumberInCache].BlockState = BlockState.Shared;
                                                    wordData = DataCache.Blocks[blockNumberInCache]
                                                        .Words[wordNumberInBlock];
                                                    // Add forty cycles
                                                    for (var i = 0; i < Constants.CyclesMemory; i++)
                                                    {
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }

                                                    Contexts[contextIndex].NumberOfCycles++;
                                                    RemainingThreadCycles[contextIndex]--;
                                                    if (RemainingThreadCycles[contextIndex] == 0)
                                                    {
                                                        Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                                                        Monitor.Exit(DataBus.Instance);
                                                        Monitor.Exit(
                                                            DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                    }

                                                    ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                    hasFinishedLoad = true;
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                }
                                                else // It will bring it from memory
                                                {
                                                    //Release the lock in other cache because it is not needed
                                                    Monitor.Exit(DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                    DataCache.Blocks[blockNumberInCache].Words =
                                                        Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                    DataCache.Blocks[blockNumberInCache].BlockState = BlockState.Shared;
                                                    wordData = DataCache.Blocks[blockNumberInCache]
                                                        .Words[wordNumberInBlock];
                                                    for (var i = 0; i < Constants.CyclesMemory; i++)
                                                    {
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }

                                                    Contexts[contextIndex].NumberOfCycles++;
                                                    RemainingThreadCycles[contextIndex]--;
                                                    if (RemainingThreadCycles[contextIndex] == 0)
                                                    {
                                                        Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                                                        Monitor.Exit(DataBus.Instance);
                                                    }

                                                    ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                    hasFinishedLoad = true;
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                }
                                            }
                                            finally
                                            {
                                                // Ensure that the lock is released.
                                                if (Monitor.IsEntered(
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                                {
                                                    Monitor.Exit(
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                        }
                                    }
                                }
                                finally
                                {
                                    // Ensure that the lock is released.
                                    if (Monitor.IsEntered(DataBus.Instance))
                                    {
                                        Monitor.Exit(DataBus.Instance);
                                    }
                                }
                            }
                            else
                            {
                                //Processor.Instance.ClockBarrier.SignalAndWait();
                                //Processor.Instance.ProcessorBarrier.SignalAndWait();
                            }
                        }

                        // The critical section.
                    }
                    finally
                    {
                        // Ensure that the lock is released.
                        // Ensure that the lock is released.
                        if (Monitor.IsEntered(DataCache.Blocks[blockNumberInCache]))
                        {
                            Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                        }
                    }
                }
                else
                {
                    //Processor.Instance.ClockBarrier.SignalAndWait();
                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            return wordData;
        }

        protected virtual void StoreData(int address, int newData, int contextIndex)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
            var hasFinishedStore = false;
            while (!hasFinishedStore)
            {
                // Try lock
                if (Monitor.TryEnter(DataCache.Blocks[blockNumberInCache]))
                {
                    try
                    {
                        // If the label matches with the block number and it is already modified
                        var currentBlock = DataCache.Blocks[blockNumberInCache];
                        if (currentBlock.Label == blockNumberInMemory &&
                            currentBlock.BlockState == BlockState.Modified)
                        {
                            DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] = newData;
                            Contexts[contextIndex].NumberOfCycles++;
                            RemainingThreadCycles[contextIndex]--;
                            if (RemainingThreadCycles[contextIndex] == 0)
                            {
                                Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                            }

                            hasFinishedStore = true;
                            //Processor.Instance.ClockBarrier.SignalAndWait();
                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                        }
                        else // It tries to get the bus
                        {
                            // Try lock
                            if (Monitor.TryEnter(DataBus.Instance))
                            {
                                try
                                {
                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();

                                    // If the label does not match with the block number and it is modified it will store the block in memory
                                    if (currentBlock.Label != blockNumberInMemory &&
                                        currentBlock.BlockState == BlockState.Modified)
                                    {
                                        Memory.Instance.StoreDataBlock(currentBlock.Label,
                                            currentBlock.Words);
                                        // Add forty cycles
                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                        {
                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                        }
                                    }

                                    var blockNumberInOtherCache = blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                    // Try lock
                                    var hasTakenOtherBlock = false;
                                    while (!hasTakenOtherBlock)
                                    {
                                        if (Monitor.TryEnter(DataCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                        {
                                            try
                                            {
                                                hasTakenOtherBlock = true;
                                                //Processor.Instance.ClockBarrier.SignalAndWait();
                                                //Processor.Instance.ProcessorBarrier.SignalAndWait();

                                                //If it is shared and the other cache block coincides it will invalidate other cache block
                                                if (currentBlock.BlockState == BlockState.Shared &&
                                                    currentBlock.Label == blockNumberInMemory)
                                                {
                                                    if (DataCache.OtherCache.Blocks[blockNumberInOtherCache].Label ==
                                                        blockNumberInMemory)
                                                    {
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                .BlockState =
                                                            BlockState.Invalid;
                                                    }
                                                    Monitor.Exit(DataCache.OtherCache.Blocks[blockNumberInOtherCache]);

                                                    DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                        newData;
                                                    DataCache.Blocks[blockNumberInCache].BlockState =
                                                        BlockState.Modified;
                                                    Contexts[contextIndex].NumberOfCycles++;
                                                    RemainingThreadCycles[contextIndex]--;
                                                    if (RemainingThreadCycles[contextIndex] == 0)
                                                    {
                                                        Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                                                        Monitor.Exit(DataBus.Instance);
                                                    }

                                                    hasFinishedStore = true;
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                }
                                                //If it is invalid or it is another label
                                                else if (currentBlock.BlockState == BlockState.Invalid ||
                                                         currentBlock.Label != blockNumberInMemory)
                                                {
                                                    ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;
                                                    DataCache.Blocks[blockNumberInCache].Label = blockNumberInMemory;
                                                    // If the label matches with the block number and it is modified it will be replaced with the current block
                                                    var otherCacheBlock =
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]; 
                                                    if (otherCacheBlock.Label ==
                                                        blockNumberInMemory &&
                                                        otherCacheBlock.BlockState ==
                                                        BlockState.Modified)
                                                    {
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                .BlockState =
                                                            BlockState.Shared;
                                                        Memory.Instance.StoreDataBlock(blockNumberInMemory,
                                                            otherCacheBlock.Words);
                                                        DataCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Shared;
                                                        // Add forty cycles
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }

                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        //Just for follow the process
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                .BlockState =
                                                            BlockState.Invalid;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;
                                                        ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                        Contexts[contextIndex].NumberOfCycles++;
                                                        RemainingThreadCycles[contextIndex]--;
                                                        if (RemainingThreadCycles[contextIndex] == 0)
                                                        {
                                                            Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                                                            Monitor.Exit(DataBus.Instance);
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                        }

                                                        hasFinishedStore = true;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }
                                                    else if (otherCacheBlock.Label ==
                                                             blockNumberInMemory &&
                                                             otherCacheBlock.BlockState ==
                                                             BlockState.Shared)
                                                    {
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache].BlockState
                                                            = BlockState.Invalid;
                                                        DataCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }

                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;
                                                        ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                        Contexts[contextIndex].NumberOfCycles++;
                                                        RemainingThreadCycles[contextIndex]--;
                                                        if (RemainingThreadCycles[contextIndex] == 0)
                                                        {
                                                            Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                                                            Monitor.Exit(DataBus.Instance);
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                        }

                                                        hasFinishedStore = true;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }
                                                    else //it has to bring it from memory
                                                    {
                                                        //Release the lock in other cache because it is not needed
                                                        Monitor.Exit(
                                                            DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                        DataCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }

                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;
                                                        ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                        Contexts[contextIndex].NumberOfCycles++;
                                                        RemainingThreadCycles[contextIndex]--;
                                                        if (RemainingThreadCycles[contextIndex] == 0)
                                                        {
                                                            Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                                                            Monitor.Exit(DataBus.Instance);
                                                        }

                                                        hasFinishedStore = true;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }
                                                }
                                            }
                                            finally
                                            {
                                                // Ensure that the lock is released.
                                                if (Monitor.IsEntered(
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                                {
                                                    Monitor.Exit(
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                        }
                                    }
                                }
                                finally
                                {
                                    // Ensure that the lock is released.
                                    if (Monitor.IsEntered(DataBus.Instance))
                                    {
                                        Monitor.Exit(DataBus.Instance);
                                    }
                                }
                            }
                            else
                            {
                                //Processor.Instance.ClockBarrier.SignalAndWait();
                                //Processor.Instance.ProcessorBarrier.SignalAndWait();
                            }
                        }

                        // The critical section.
                    }
                    finally
                    {
                        // Ensure that the lock is released.
                        if (Monitor.IsEntered(DataCache.Blocks[blockNumberInCache]))
                        {
                            Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                        }
                    }
                }
                else
                {
                    //Processor.Instance.ClockBarrier.SignalAndWait();
                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }
        }
    }
}