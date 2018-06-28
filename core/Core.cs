﻿using System;
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
            Context = null;
            ThereAreContexts = true;
        }

        private Instruction InstructionRegister { get; set; }

        public Context Context { get; set; }

        private Cache<Instruction> InstructionCache { get; set; }

        public Cache<int> DataCache { get; set; }

        public int RemainingThreadCycles { get; set; }

        public bool ThreadHasEnded { get; set; }

        public bool ThereAreContexts { get; set; }

        private static int GetBlockNumberInMemory(int address)
        {
            return (address / Constants.BytesInBlock);
        }

        private static int GetWordNumberInBlock(int address)
        {
            return (address % Constants.BytesInBlock) / Constants.WordsInBlock;
        }

        public void StartExecution(Context context)
        {
            Context = context;
            RemainingThreadCycles = Processor.Instance.Quantum;

            while (ThereAreContexts)
            {

                ThreadHasEnded = false;
                //First instruction fetch
                InstructionRegister = LoadInstruction();

                //Execute every instruction in the thread until it obtains an end instruction
                while (InstructionRegister.OperationCode != (int) Operation.End)
                {
                    Context.ProgramCounter += Constants.BytesInWord;
                    ExecuteInstruction(InstructionRegister);
                    //Instruction fetch
                    InstructionRegister = LoadInstruction();
                }

                ThreadHasEnded = true;
                ThereAreContexts = false;
                Processor.Instance.ClockBarrier.SignalAndWait();
                Processor.Instance.ProcessorBarrier.SignalAndWait();
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
            var instruction = new Instruction();
            var blockNumberInCache = blockNumberInMemory % InstructionCache.CacheSize;
            var hasFinishedLoad = false;
            // Wwhile it has not finished the load it continues trying
            while (!hasFinishedLoad)
            {
                // Try lock
                if (Monitor.TryEnter(InstructionCache.Blocks[blockNumberInCache]))
                {
                    try
                    {
                        // If the label matches with the block number and it is not invalid
                        var currentBlock = InstructionCache.Blocks[blockNumberInCache];
                        if (currentBlock.Label == blockNumberInMemory)
                        {
                            instruction = currentBlock.Words[wordNumberInBlock];
                            hasFinishedLoad = true;
                            Processor.Instance.ClockBarrier.SignalAndWait();
                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                        }
                        else // It tryes to get the bus
                        {
                            // Try lock
                            if (Monitor.TryEnter(InstructionBus.Instance))
                            {
                                try
                                {
                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                    Processor.Instance.ProcessorBarrier.SignalAndWait();

                                    var blockNumberInOtherCache =
                                        blockNumberInMemory % InstructionCache.OtherCache.CacheSize;
                                    // Try lock
                                    var hasTakenOtherBlock = false;
                                    while (!hasTakenOtherBlock)
                                    {
                                        if (Monitor.TryEnter(
                                            InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                        {
                                            try
                                            {
                                                hasTakenOtherBlock = true;
                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                    hasFinishedLoad = true;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }

                                                    hasFinishedLoad = true;                            
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                }
                                            }
                                            finally
                                            {
                                                // Ensure that the lock is released.
                                                if (Monitor.IsEntered(
                                                    InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                                {
                                                    Monitor.Exit(
                                                        InstructionCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                Processor.Instance.ClockBarrier.SignalAndWait();
                                Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                    Processor.Instance.ClockBarrier.SignalAndWait();
                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            return instruction;
        }

        private void ExecuteInstruction(Instruction actualInstruction)
        {
            int address;
            switch (actualInstruction.OperationCode)
            {
                case (int) Operation.Jr:
                    Context.ProgramCounter = Context.Registers[actualInstruction.Source];
                    break;

                case (int) Operation.Jal:
                    Context.Registers[31] = Context.ProgramCounter;
                    Context.ProgramCounter += actualInstruction.Inmediate;
                    break;

                case (int) Operation.Beqz:
                    if (Context.Registers[actualInstruction.Source] == 0)
                    {
                        Context.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;

                case (int) Operation.Bnez:
                    if (Context.Registers[actualInstruction.Source] != 0)
                    {
                        Context.ProgramCounter += (4 * actualInstruction.Inmediate);
                    }

                    break;

                case (int) Operation.Daddi:
                    Context.Registers[actualInstruction.Destiny] =
                        Context.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    break;
                case (int) Operation.Dmul:
                    Context.Registers[actualInstruction.Inmediate] =
                        Context.Registers[actualInstruction.Source] *
                        Context.Registers[actualInstruction.Destiny];
                    break;
                case (int) Operation.Ddiv:
                    Context.Registers[actualInstruction.Inmediate] =
                        Context.Registers[actualInstruction.Source] /
                        Context.Registers[actualInstruction.Destiny];
                    break;
                case (int) Operation.Dadd:
                    Context.Registers[actualInstruction.Inmediate] =
                        Context.Registers[actualInstruction.Source] +
                        Context.Registers[actualInstruction.Destiny];
                    break;
                case (int) Operation.Dsub:
                    Context.Registers[actualInstruction.Inmediate] =
                        Context.Registers[actualInstruction.Source] -
                        Context.Registers[actualInstruction.Destiny];
                    break;
                case (int) Operation.Lw:
                    address = Context.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    if (address >= 0 && address < Constants.BytesInMemoryDataBlocks)
                    {
                        Context.Registers[actualInstruction.Destiny] = LoadData(address);
                    }
                    else
                    {
                        Console.WriteLine(Constants.AddressError + actualInstruction);
                    }

                    break;
                case (int) Operation.Sw:
                    address = Context.Registers[actualInstruction.Source] + actualInstruction.Inmediate;
                    if (address >= 0 && address < Constants.BytesInMemoryDataBlocks)
                    {
                        StoreData(address, Context.Registers[actualInstruction.Destiny]);
                    }
                    else
                    {
                        Console.WriteLine(Constants.AddressError + actualInstruction);
                    }

                    break;
                default:
                    Console.WriteLine("Instruction " + actualInstruction.OperationCode + " has not been recognized.");
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
                            hasFinishedLoad = true;
                            Processor.Instance.ClockBarrier.SignalAndWait();
                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                        }
                        else // It tryes to get the bus
                        {
                            // Try lock
                            if (Monitor.TryEnter(DataBus.Instance))
                            {
                                try
                                {
                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                    // If the label does not match with the block number and it is modified it will store the block in memory
                                    if (currentBlock.Label != blockNumberInMemory &&
                                        currentBlock.BlockState == BlockState.Modified)
                                    {
                                        Memory.Instance.StoreDataBlock(currentBlock.Label,
                                            currentBlock.Words);
                                        // Add forty cycles
                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                        {
                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }
                                                    
                                                    hasFinishedLoad = true;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }
                                                   
                                                    hasFinishedLoad = true;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                Processor.Instance.ClockBarrier.SignalAndWait();
                                Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                    Processor.Instance.ClockBarrier.SignalAndWait();
                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            RemainingThreadCycles--;
            return wordData;
        }

        private void StoreData(int address, int newData)
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
                            hasFinishedStore = true;
                            Processor.Instance.ClockBarrier.SignalAndWait();
                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                        }
                        else // It tries to get the bus
                        {
                            // Try lock
                            if (Monitor.TryEnter(DataBus.Instance))
                            {
                                try
                                {
                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                    Processor.Instance.ProcessorBarrier.SignalAndWait();

                                    // If the label does not match with the block number and it is modified it will store the block in memory
                                    if (currentBlock.Label != blockNumberInMemory &&
                                        currentBlock.BlockState == BlockState.Modified)
                                    {
                                        Memory.Instance.StoreDataBlock(currentBlock.Label,
                                            currentBlock.Words);
                                        // Add forty cycles
                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                        {
                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                Processor.Instance.ProcessorBarrier.SignalAndWait();

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
                                                    hasFinishedStore = true;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                }
                                                //If it is invalid or it is another label
                                                else if (currentBlock.BlockState == BlockState.Invalid ||
                                                         currentBlock.Label != blockNumberInMemory)
                                                {
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
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }

                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        //Just for follow the process
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                .BlockState =
                                                            BlockState.Invalid;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;                                                        
                                                        hasFinishedStore = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }

                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;                                                        
                                                        hasFinishedStore = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }

                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;                                                        
                                                        hasFinishedStore = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                Processor.Instance.ClockBarrier.SignalAndWait();
                                Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                    Processor.Instance.ClockBarrier.SignalAndWait();
                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            RemainingThreadCycles--;
        }
    }
}