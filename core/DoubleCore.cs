using System;
using System.Collections.Generic;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.common;
using ProcessorSimulator.memory;
using ProcessorSimulator.processor;

namespace ProcessorSimulator.core
{
    public class DobleCore : Core
    {
        public DobleCore(Cache<Instruction> instructionCache, Cache<int> dataCache) : base(instructionCache, dataCache)
        {
            InstructionRegister = null;
            RemainingThreadCycles = Constants.NotRunningAnyThread;
            ThreadStatusTwo = ThreadStatus.Stopped;
            Reservations = new List<Reservation>();
        }

        public Instruction InstructionRegisterTwo { get; set; }

        public Context ContextTwo { get; set; }

        public int RemainingThreadCyclesTwo { get; set; }

        public ThreadStatus ThreadStatusTwo { get; set; }

        public List<Reservation> Reservations { get; set; }
        
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
                        var block = DataCache.Blocks[blockNumberInCache];
                        if (block.Label == blockNumberInMemory &&
                            block.BlockState != BlockState.Invalid)
                        {
                            wordData = block.Words[wordNumberInBlock];
                            Context.NumberOfCycles++;
                            RemainingThreadCycles--;
                            Processor.Instance.ClockBarrier.SignalAndWait();
                            Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                    // If the label does not match with the block number and it is modified it will store the block in memory
                                    block = DataCache.Blocks[blockNumberInCache];
                                    if (block.Label != blockNumberInMemory &&
                                        block.BlockState == BlockState.Modified)
                                    {
                                        Memory.Instance.StoreDataBlock(address,
                                            block.Words);
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
                                                block = DataCache.OtherCache.Blocks[blockNumberInOtherCache];
                                                if (block.Label ==
                                                    blockNumberInMemory &&
                                                    block.BlockState ==
                                                    BlockState.Modified)
                                                {
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache].BlockState =
                                                        BlockState.Shared;
                                                    Memory.Instance.StoreDataBlock(address, block.Words);
                                                    DataCache.Blocks[blockNumberInCache].Words =
                                                        Memory.Instance.LoadDataBlock(address).Words;
                                                    DataCache.Blocks[blockNumberInCache].BlockState = BlockState.Shared;
                                                    wordData = DataCache.Blocks[blockNumberInCache]
                                                        .Words[wordNumberInBlock];
                                                    // Add forty cycles
                                                    for (var i = 0; i < Constants.CyclesMemory; i++)
                                                    {
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }

                                                    Context.NumberOfCycles++;
                                                    RemainingThreadCycles--;
                                                    ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    hasFinishedLoad = true;
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
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    }
                                                    
                                                    Context.NumberOfCycles++;
                                                    RemainingThreadCycles--;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    hasFinishedLoad = true;
                                                }
                                            }
                                            finally
                                            {
                                                // Ensure that the lock is released.
                                                Monitor.Exit(DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
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
                                    Monitor.Exit(DataBus.Instance);
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
                        Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                    }
                }
                else
                {
                    Processor.Instance.ClockBarrier.SignalAndWait();
                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            return wordData;
        }

        private void StoreData(int address, int newData)
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
                        var block = DataCache.Blocks[blockNumberInCache];
                        if (block.Label == blockNumberInMemory &&
                            block.BlockState == BlockState.Modified)
                        {
                            DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] = newData;
                            Context.NumberOfCycles++;
                            RemainingThreadCycles--;
                            Processor.Instance.ClockBarrier.SignalAndWait();
                            Processor.Instance.ProcessorBarrier.SignalAndWait();
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
                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                    Processor.Instance.ProcessorBarrier.SignalAndWait();

                                    // If the label does not match with the block number and it is modified it will store the block in memory
                                    if (block.Label != blockNumberInMemory &&
                                        block.BlockState == BlockState.Modified)
                                    {
                                        Memory.Instance.StoreDataBlock(address,
                                            block.Words);
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

                                                //If it is shared it will invalidate other cache block
                                                if (block.BlockState == BlockState.Shared &&
                                                    block.Label == blockNumberInMemory && DataCache.OtherCache.Blocks[blockNumberInOtherCache].Label == blockNumberInMemory)
                                                {
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache].BlockState =
                                                        BlockState.Invalid;
                                                    DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                        newData;
                                                    DataCache.Blocks[blockNumberInCache].BlockState =
                                                        BlockState.Modified;
                                                    Context.NumberOfCycles++;
                                                    RemainingThreadCycles--;
                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                    hasFinishedStore = true;
                                                    Console.WriteLine("I could write the block");
                                                }
                                                //If it is invalid or it is another label
                                                else if (block.BlockState == BlockState.Invalid ||
                                                         block.Label != blockNumberInMemory)
                                                {
                                                    ThreadStatus = ThreadStatus.CacheFail;
                                                    DataCache.Blocks[blockNumberInCache].Label = blockNumberInMemory;
                                                    // If the label matches with the block number and it is modified it will be replaced with the current block
                                                    block =
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache];
                                                    if (block.Label ==
                                                        blockNumberInMemory &&
                                                        block.BlockState ==
                                                        BlockState.Modified)
                                                    {
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                .BlockState =
                                                            BlockState.Shared;
                                                        Memory.Instance.StoreDataBlock(address, block.Words);
                                                        DataCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadDataBlock(address).Words;
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
                                                        ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                        Context.NumberOfCycles++;
                                                        RemainingThreadCycles--;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        hasFinishedStore = true;
                                                        Console.WriteLine("I could write the block");
                                                    }
                                                    else if (block.Label ==
                                                             blockNumberInMemory &&
                                                             block.BlockState ==
                                                             BlockState.Shared)
                                                    {
                                                        DataCache.OtherCache.Blocks[blockNumberInOtherCache].BlockState
                                                            = BlockState.Invalid;
                                                        DataCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadDataBlock(address).Words;
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;
                                                        ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                        Context.NumberOfCycles++;
                                                        RemainingThreadCycles--;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        hasFinishedStore = true;
                                                        Console.WriteLine("I could write the block");
                                                        
                                                    }
                                                    else
                                                    {
                                                        DataCache.Blocks[blockNumberInCache].Words =
                                                            Memory.Instance.LoadDataBlock(address).Words;
                                                        for (var i = 0; i < Constants.CyclesMemory; i++)
                                                        {
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                        DataCache.Blocks[blockNumberInCache].Words[wordNumberInBlock] =
                                                            newData;
                                                        DataCache.Blocks[blockNumberInCache].BlockState =
                                                            BlockState.Modified;
                                                        ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                        Context.NumberOfCycles++;
                                                        RemainingThreadCycles--;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        hasFinishedStore = true;
                                                        Console.WriteLine("I could write the block");
                                                    }
                                                }
                                            }
                                            finally
                                            {
                                                // Ensure that the lock is released.
                                                Monitor.Exit(DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
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
                                    Monitor.Exit(DataBus.Instance);
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
                        Monitor.Exit(DataCache.Blocks[blockNumberInCache]);
                    }
                }
                else
                {
                    Processor.Instance.ClockBarrier.SignalAndWait();
                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }
        }
    }
}