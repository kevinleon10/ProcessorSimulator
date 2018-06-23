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

        protected Instruction InstructionRegister { get; set; }

        public Context Context { get; set; }

        protected Cache<Instruction> InstructionCache { get; set; }

        public Cache<int> DataCache { get; set; }

        public int RemainingThreadCycles { get; set; }

        public ThreadStatus ThreadStatus { get; set; }

        protected int GetBlockNumberInMemory(int address)
        {
            return (address / Constants.BytesInBlock);
        }

        protected int GetWordNumberInBlock(int address)
        {
            return (address % Constants.BytesInBlock) / Constants.WordsInBlock;
        }

        public void StartExecution(Context context)
        {
            Context = context;
            RemainingThreadCycles = Processor.Instance.Quantum;

            //First instruction fetch
            InstructionRegister = LoadInstruction();

            //Execute every instruction in the thread until it obtains an end instruction
            while (InstructionRegister.OperationCode != (int) Operation.END)
            {
                Context.ProgramCounter += Constants.BytesInWord;
                ExecuteInstruction(InstructionRegister);
                //Instruction fetch
                InstructionRegister = LoadInstruction();
            }
        }

        /// <summary>
        /// Load a instruction
        /// </summary>
        /// <returns>
        /// The resulting instruction
        /// </returns>
        private Instruction LoadInstruction()
        {
            var blockNumberInMemory = GetBlockNumberInMemory(Context.ProgramCounter);
            var wordNumberInBlock = GetWordNumberInBlock(Context.ProgramCounter);
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
                            Context.NumberOfCycles++;
                            RemainingThreadCycles--;
                            //Processor.Instance.ClockBarrier.SignalAndWait();
                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                            Console.WriteLine("I could take the instruction block");
                        }
                        else
                        {
                            ThreadStatus = ThreadStatus.CacheFail;
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
                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                            .Words = Memory.Instance.LoadInstructionBlock(blockNumberInMemory
                                                            );
                                                        instruction = InstructionCache.Blocks[blockNumberInCache]
                                                            .Words[wordNumberInBlock];
                                                        Context.NumberOfCycles++;
                                                        RemainingThreadCycles--;
                                                        ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        Console.WriteLine(
                                                            "I could take the instruction block from the other cache");
                                                    }
                                                    else // It has to bring it from memory
                                                    {
                                                        //Release the lock in other cache because it is not needed
                                                        Monitor.Exit(InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                        InstructionCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadInstructionBlock(
                                                                blockNumberInMemory);
                                                        instruction = InstructionCache.Blocks[blockNumberInCache]
                                                            .Words[wordNumberInBlock];
                                                        // Add forty cycles
                                                        /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }*/

                                                        Context.NumberOfCycles++;
                                                        RemainingThreadCycles--;
                                                        ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        Console.WriteLine(
                                                            "I could take the instruction block from memory");
                                                    }
                                                }
                                                finally
                                                {
                                                    // Ensure that the lock is released.
                                                    if(Monitor.IsEntered(InstructionCache.OtherCache.Blocks[blockNumberInOtherCache])){
                                                        Monitor.Exit(
                                                        InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]);
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
                                        Monitor.Exit(InstructionBus.Instance);
                                    }
                                }
                                else
                                {
                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                    //Instance.ProcessorBarrier.SignalAndWait();
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
                else
                {
                    //Processor.Instance.ClockBarrier.SignalAndWait();
                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            return instruction;
        }

        private void ExecuteInstruction(Instruction actualInstruction)
        {
            int address;
            switch (actualInstruction.OperationCode)
            {
                case (int) Operation.JR:
                    Context.ProgramCounter = Context.Registers[actualInstruction.Source];
                    break;

                case (int) Operation.JAL:
                    Context.Registers[31] = Context.ProgramCounter;
                    Context.ProgramCounter += actualInstruction.Inmediate;
                    break;

                case (int) Operation.BEQZ:
                    if (Context.Registers[actualInstruction.Source] == 0)
                    {
                        Context.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;

                case (int) Operation.BNEZ:
                    if (Context.Registers[actualInstruction.Source] != 0)
                    {
                        Context.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;

                case (int) Operation.DADDI:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    break;
                case (int) Operation.DMUL:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] +
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case (int) Operation.DDIV:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] /
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case (int) Operation.DADD:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] +
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case (int) Operation.DSUB:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] -
                        Context.Registers[actualInstruction.Inmediate];
                    break;
                case (int) Operation.LW:
                    address = Context.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    Context.Registers[actualInstruction.Destiny] = LoadData(address);
                    break;
                case (int) Operation.SW:
                    address = Context.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    StoreData(address, Context.Registers[actualInstruction.Destiny]);
                    break;
                default:
                    Console.WriteLine("Instruction " + actualInstruction.OperationCode + " has not been recognised");
                    break;
            }
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <returns>
        /// The resulting data
        /// </returns>
        protected virtual int LoadData(int address)
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
                        var currentBlock = DataCache.Blocks[blockNumberInCache];
                        if (currentBlock.Label == blockNumberInMemory &&
                            currentBlock.BlockState != BlockState.Invalid)
                        {
                            wordData = currentBlock.Words[wordNumberInBlock];
                            Context.NumberOfCycles++;
                            RemainingThreadCycles--;
                            //Processor.Instance.ClockBarrier.SignalAndWait();
                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                    ThreadStatus = ThreadStatus.CacheFail;
                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                    // If the label does not match with the block number and it is modified it will store the block in memory
                                    if (currentBlock.Label != blockNumberInMemory &&
                                        currentBlock.BlockState == BlockState.Modified)
                                    {
                                        var newAddress = currentBlock.Label * Constants.BytesInBlock;
                                        Memory.Instance.StoreDataBlock(newAddress,
                                            currentBlock.Words);
                                        // Add forty cycles
                                        /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                        {
                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                        }*/
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
                                                    Memory.Instance.StoreDataBlock(address, otherCacheBlock.Words);
                                                    DataCache.Blocks[blockNumberInCache].Words =
                                                        Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                    DataCache.Blocks[blockNumberInCache].BlockState = BlockState.Shared;
                                                    wordData = DataCache.Blocks[blockNumberInCache]
                                                        .Words[wordNumberInBlock];
                                                    // Add forty cycles
                                                    /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                    {
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }*/

                                                    Context.NumberOfCycles++;
                                                    RemainingThreadCycles--;
                                                    ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    hasFinishedLoad = true;
                                                    Console.WriteLine("I could take the block from the other cache");
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
                                                    /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                    {
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }*/

                                                    Context.NumberOfCycles++;
                                                    RemainingThreadCycles--;
                                                    ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    hasFinishedLoad = true;
                                                    Console.WriteLine("I could take the block from memory");
                                                }
                                            }
                                            finally
                                            {
                                                // Ensure that the lock is released.
                                                if(Monitor.IsEntered(DataCache.OtherCache.Blocks[blockNumberInOtherCache])){
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
                                    Monitor.Exit(DataBus.Instance);
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
                        Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
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

        protected virtual void StoreData(int address, int newData)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            Console.WriteLine("Block number in memory: " + blockNumberInMemory);
            Console.WriteLine("Word number in block: " + wordNumberInBlock);
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
            Console.WriteLine("Block number in cache: " + blockNumberInCache);
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
                            Context.NumberOfCycles++;
                            RemainingThreadCycles--;
                            //Processor.Instance.ClockBarrier.SignalAndWait();
                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                            hasFinishedStore = true;
                            Console.WriteLine("I could write the block");
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
                                        var newAddress = currentBlock.Label * Constants.BytesInBlock;
                                        Memory.Instance.StoreDataBlock(newAddress,
                                            currentBlock.Words);
                                        // Add forty cycles
                                        /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                        {
                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                        }*/
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

                                                //If it is shared it will invalidate other cache block
                                                if (currentBlock.BlockState == BlockState.Shared &&
                                                    currentBlock.Label == blockNumberInMemory &&
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache].Label ==
                                                    blockNumberInMemory)
                                                {
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache].BlockState =
                                                        BlockState.Invalid;
                                                    DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                        newData;
                                                    DataCache.Blocks[blockNumberInCache].BlockState =
                                                        BlockState.Modified;
                                                    Context.NumberOfCycles++;
                                                    RemainingThreadCycles--;
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    hasFinishedStore = true;
                                                    Console.WriteLine("I could write the block");
                                                }
                                                //If it is invalid or it is another label
                                                else if (currentBlock.BlockState == BlockState.Invalid ||
                                                         currentBlock.Label != blockNumberInMemory)
                                                {
                                                    ThreadStatus = ThreadStatus.CacheFail;
                                                    DataCache.Blocks[blockNumberInCache].Label = blockNumberInMemory;
                                                    // If the label matches with the block number and it is modified it will be replaced with the current block
                                                    var otherCacheBlock =
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]; // CUIDADO
                                                    if (otherCacheBlock.Label ==
                                                        blockNumberInMemory &&
                                                        otherCacheBlock.BlockState ==
                                                        BlockState.Modified)
                                                    {
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                .BlockState =
                                                            BlockState.Shared;
                                                        Memory.Instance.StoreDataBlock(address, otherCacheBlock.Words);
                                                        DataCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Shared;
                                                        // Add forty cycles
                                                        /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }*/
                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        //Just for follow the process
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                .BlockState =
                                                            BlockState.Invalid;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;
                                                        ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                        Context.NumberOfCycles++;
                                                        RemainingThreadCycles--;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        hasFinishedStore = true;
                                                        Console.WriteLine("I could write the block");
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
                                                        /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }*/
                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;
                                                        ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                        Context.NumberOfCycles++;
                                                        RemainingThreadCycles--;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        hasFinishedStore = true;
                                                        Console.WriteLine("I could write the block");
                                                    }
                                                    else //it has to bring it from memory
                                                    {
                                                        //Release the lock in other cache because it is not needed
                                                        Monitor.Exit(DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                        DataCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                        /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }*/
                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;
                                                        ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                        Context.NumberOfCycles++;
                                                        RemainingThreadCycles--;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        hasFinishedStore = true;
                                                        Console.WriteLine("I could write the block");
                                                    }
                                                }
                                            }
                                            finally
                                            {
                                                // Ensure that the lock is released.
                                                if(Monitor.IsEntered(DataCache.OtherCache.Blocks[blockNumberInOtherCache])){
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
                                    Monitor.Exit(DataBus.Instance);
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
                        Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
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