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
    public class DoubleCore : Core
    {
        public DoubleCore(Cache<Instruction> instructionCache, Cache<int> dataCache) : base(instructionCache, dataCache)
        {
            Reservations = new List<Reservation>();
            RemainingThreadCycles = new int[Constants.ThreadsInCoreZero];
            ThreadStatuses = new ThreadStatus[Constants.ThreadsInCoreZero];
            Contexts = new Context[Constants.ThreadsInCoreZero];
            RemainingThreadCycles[Constants.FirstContextIndex] = Constants.NotRunningAnyThread;
            RemainingThreadCycles[Constants.SecondContextIndex] = Constants.NotRunningAnyThread;
        }

        public List<Reservation> Reservations { get; set; }

        private bool IsBusReserved(bool isInDataCache)
        {
            var i = 0;
            while (i < Reservations.Count)
            {
                if (Reservations[i].IsUsingBus &&
                    Reservations[i].IsInDataCache == isInDataCache)
                {
                    return true;
                }

                ++i;
            }

            return false;
        }

        private bool IsBlockReserved(int blockNumberInCache, bool isInDataCache)
        {
            var i = 0;
            while (i < Reservations.Count)
            {
                if (blockNumberInCache == Reservations[i].BlockNumberInCache &&
                    Reservations[i].IsInDataCache == isInDataCache)
                {
                    return true;
                }

                ++i;
            }

            return false;
        }

        protected override Instruction LoadInstruction(int contextIndex)
        {
            if (contextIndex == 1)
            {
                Console.WriteLine("me cago en la puta");
            }

            var blockNumberInMemory = GetBlockNumberInMemory(Contexts[contextIndex].ProgramCounter);
            var wordNumberInBlock = GetWordNumberInBlock(Contexts[contextIndex].ProgramCounter);
            var instruction = new Instruction();
            var blockNumberInCache = blockNumberInMemory % InstructionCache.CacheSize;
            var hasFinishedLoad = false;
            var hasReserved = false;
            var hasAccesedBlockReservations = false;
            // While it has not finished the load it continues trying
            while (!hasFinishedLoad)
            {
                if (hasReserved || !IsBlockReserved(blockNumberInCache, false))
                {
                    // Try lock
                    if (contextIndex == 1)
                    {
                        Console.WriteLine("me cago en la puta");
                    }

                    if (Monitor.TryEnter(InstructionCache.Blocks[blockNumberInCache]))
                    {
                        try
                        {
                            // If the label matches with the block number
                            var currentBlock = InstructionCache.Blocks[blockNumberInCache];
                            if (currentBlock.Label == blockNumberInMemory)
                            {
                                instruction = currentBlock.Words[wordNumberInBlock];
                                Contexts[contextIndex].NumberOfCycles++;
                                RemainingThreadCycles[contextIndex]--;
                                if (RemainingThreadCycles[contextIndex] == 0)
                                {
                                    Monitor.Exit(InstructionCache.Blocks[blockNumberInCache]);
                                }

                                Processor.Instance.ClockBarrier.SignalAndWait();
                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                hasFinishedLoad = true;
                            }
                            else // It tries to get the bus
                            {
                                ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;

                                if (!hasReserved && !IsBusReserved(false))
                                {
                                    var hasAccesedReservations = false;
                                    while (!hasAccesedReservations)
                                    {
                                        if (Monitor.TryEnter(
                                            Reservations))
                                        {
                                            try
                                            {
                                                hasAccesedReservations = true;
                                                hasReserved = true;
                                                Reservations.Add(new Reservation(false, true, false,
                                                    blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                                Reservations.Add(new Reservation(false, false, false,
                                                    blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                            }
                                            finally
                                            {
                                                Monitor.Exit(
                                                    Reservations);
                                            }
                                        }
                                    }

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
                                                    InstructionCache.OtherCache.Blocks
                                                        [blockNumberInOtherCache]))
                                                {
                                                    try
                                                    {
                                                        hasTakenOtherBlock = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        InstructionCache.Blocks[blockNumberInCache].Label =
                                                            blockNumberInMemory;
                                                        // If the label matches with the block number it will be replaced the current block
                                                        if (InstructionCache.OtherCache
                                                                .Blocks[blockNumberInOtherCache]
                                                                .Label == blockNumberInMemory)
                                                        {
                                                            InstructionCache.Blocks[blockNumberInCache]
                                                                .Words = Memory.Instance.LoadInstructionBlock(
                                                                blockNumberInMemory
                                                            );
                                                            instruction = InstructionCache
                                                                .Blocks[blockNumberInCache]
                                                                .Words[wordNumberInBlock];
                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0)
                                                            {
                                                                Monitor.Exit(
                                                                    InstructionCache.Blocks[blockNumberInCache]);
                                                                Monitor.Exit(InstructionBus.Instance);
                                                                Monitor.Exit(
                                                                    InstructionCache.OtherCache.Blocks[
                                                                        blockNumberInOtherCache]);
                                                            }

                                                            ThreadStatuses[contextIndex] =
                                                                ThreadStatus.SolvedCacheFail;
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
                                                            instruction = InstructionCache
                                                                .Blocks[blockNumberInCache]
                                                                .Words[wordNumberInBlock];
                                                            // Add forty cycles
                                                            for (var i = 0; i < Constants.CyclesMemory; i++)
                                                            {
                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }

                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0)
                                                            {
                                                                Monitor.Exit(
                                                                    InstructionCache.Blocks[blockNumberInCache]);
                                                                Monitor.Exit(InstructionBus.Instance);
                                                            }

                                                            ThreadStatuses[contextIndex] =
                                                                ThreadStatus.SolvedCacheFail;
                                                            hasFinishedLoad = true;
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        // Ensure that the lock is released.
                                                        if (Monitor.IsEntered(
                                                            InstructionCache.OtherCache.Blocks
                                                                [blockNumberInOtherCache]))
                                                        {
                                                            Monitor.Exit(
                                                                InstructionCache.OtherCache.Blocks
                                                                    [blockNumberInOtherCache]);
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
                                else if (hasReserved)
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
                                                    InstructionCache.OtherCache.Blocks
                                                        [blockNumberInOtherCache]))
                                                {
                                                    try
                                                    {
                                                        hasTakenOtherBlock = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        InstructionCache.Blocks[blockNumberInCache].Label =
                                                            blockNumberInMemory;
                                                        // If the label matches with the block number it will be replaced the current block
                                                        if (InstructionCache.OtherCache
                                                                .Blocks[blockNumberInOtherCache]
                                                                .Label == blockNumberInMemory)
                                                        {
                                                            InstructionCache.Blocks[blockNumberInCache]
                                                                .Words = Memory.Instance.LoadInstructionBlock(
                                                                blockNumberInMemory
                                                            );
                                                            instruction = InstructionCache
                                                                .Blocks[blockNumberInCache]
                                                                .Words[wordNumberInBlock];
                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0)
                                                            {
                                                                Monitor.Exit(
                                                                    InstructionCache.Blocks[blockNumberInCache]);
                                                                Monitor.Exit(InstructionBus.Instance);
                                                                Monitor.Exit(
                                                                    InstructionCache.OtherCache.Blocks[
                                                                        blockNumberInOtherCache]);
                                                            }

                                                            ThreadStatuses[contextIndex] =
                                                                ThreadStatus.SolvedCacheFail;
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
                                                            instruction = InstructionCache
                                                                .Blocks[blockNumberInCache]
                                                                .Words[wordNumberInBlock];
                                                            // Add forty cycles
                                                            for (var i = 0; i < Constants.CyclesMemory; i++)
                                                            {
                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }

                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0)
                                                            {
                                                                Monitor.Exit(
                                                                    InstructionCache.Blocks[blockNumberInCache]);
                                                                Monitor.Exit(InstructionBus.Instance);
                                                            }

                                                            ThreadStatuses[contextIndex] =
                                                                ThreadStatus.SolvedCacheFail;
                                                            hasFinishedLoad = true;
                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        // Ensure that the lock is released.
                                                        if (Monitor.IsEntered(
                                                            InstructionCache.OtherCache.Blocks
                                                                [blockNumberInOtherCache]))
                                                        {
                                                            Monitor.Exit(
                                                                InstructionCache.OtherCache.Blocks
                                                                    [blockNumberInOtherCache]);
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
                                else
                                {
                                    var hasAccesedReservations = false;
                                    while (!hasAccesedReservations)
                                    {
                                        if (Monitor.TryEnter(
                                            Reservations))
                                        {
                                            try
                                            {
                                                hasAccesedReservations = true;
                                                Reservations.Add(new Reservation(true, true, false, blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                            }
                                            finally
                                            {
                                                Monitor.Exit(
                                                    Reservations);
                                            }
                                        }
                                    }
                                    
                                    ThreadStatuses[contextIndex] = ThreadStatus.Waiting;
                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                }
                            }
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

                else
                {
                    while (!hasAccesedBlockReservations)
                    {
                        if (Monitor.TryEnter(
                            Reservations))
                        {
                            try
                            {
                                hasAccesedBlockReservations = true;
                                Reservations.Add(new Reservation(true, false, false, blockNumberInCache,
                                    Contexts[contextIndex].ThreadId));
                            }
                            finally
                            {
                                Monitor.Exit(
                                    Reservations);
                            }
                        }
                    }

                    ThreadStatuses[contextIndex] = ThreadStatus.Waiting;
                    Processor.Instance.ClockBarrier.SignalAndWait();
                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            return instruction;
        }

        protected override int LoadData(int address, int contextIndex)
        {
            if (contextIndex == 1)
            {
                Console.WriteLine("me cago en la puta");
            }
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            var wordData = 0;
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
            var hasFinishedLoad = false;
            var hasReserved = false;
            var hasAccesedBlockReservations = false;
            // Wwhile it has not finished the load it continues trying
            while (!hasFinishedLoad)
            {
                if (hasReserved || !IsBlockReserved(blockNumberInCache, true))
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
                                Processor.Instance.ClockBarrier.SignalAndWait();
                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                            }
                            else // It tries to get the bus
                            {
                                ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;

                                if (!hasReserved && !IsBusReserved(false))
                                {
                                    var hasAccesedReservations = false;
                                    while (!hasAccesedReservations)
                                    {
                                        if (Monitor.TryEnter(
                                            Reservations))
                                        {
                                            try
                                            {
                                                hasAccesedReservations = true;
                                                hasReserved = true;
                                                Reservations.Add(new Reservation(false, true, true,
                                                    blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                                Reservations.Add(new Reservation(false, false, true,
                                                    blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                            }
                                            finally
                                            {
                                                Monitor.Exit(
                                                    Reservations);
                                            }
                                        }
                                    }

                                    // Try lock
                                    if (Monitor.TryEnter(DataBus.Instance))
                                    {
                                        try
                                        {
                                            //ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;
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

                                            var blockNumberInOtherCache =
                                                blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                            // Try lock
                                            var hasTakenOtherBlock = false;
                                            while (!hasTakenOtherBlock)
                                            {
                                                if (Monitor.TryEnter(
                                                    DataCache.OtherCache.Blocks
                                                        [blockNumberInOtherCache]))
                                                {
                                                    try
                                                    {
                                                        hasTakenOtherBlock = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        DataCache.Blocks[blockNumberInCache].Label =
                                                            blockNumberInMemory;
                                                        // If the label matches with the block number it will be replaced the current block
                                                        var otherCacheBlock =
                                                            DataCache.OtherCache.Blocks[
                                                                blockNumberInOtherCache];
                                                        if (otherCacheBlock.Label ==
                                                            blockNumberInMemory &&
                                                            otherCacheBlock.BlockState ==
                                                            BlockState.Modified)
                                                        {
                                                            DataCache.OtherCache
                                                                    .Blocks[blockNumberInOtherCache]
                                                                    .BlockState =
                                                                BlockState.Shared;
                                                            Memory.Instance.StoreDataBlock(
                                                                blockNumberInMemory,
                                                                otherCacheBlock.Words);
                                                            DataCache.Blocks[blockNumberInCache].Words =
                                                                Memory.Instance.LoadDataBlock(
                                                                    blockNumberInMemory);
                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .BlockState =
                                                                BlockState.Shared;
                                                            wordData = DataCache
                                                                .Blocks[blockNumberInCache]
                                                                .Words[wordNumberInBlock];
                                                            // Add forty cycles
                                                            for (var i = 0;
                                                                i < Constants.CyclesMemory;
                                                                i++)
                                                            {
                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }

                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0
                                                            )
                                                            {
                                                                Monitor.Exit(
                                                                    DataCache.Blocks
                                                                        [blockNumberInCache]);
                                                                Monitor.Exit(DataBus.Instance);
                                                                Monitor.Exit(
                                                                    DataCache.OtherCache.Blocks[
                                                                        blockNumberInOtherCache]);
                                                            }

                                                            ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                            hasFinishedLoad = true;

                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                        else // It will bring it from memory
                                                        {
                                                            //Release the lock in other cache because it is not needed
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks
                                                                    [blockNumberInOtherCache]);
                                                            DataCache.Blocks[blockNumberInCache].Words =
                                                                Memory.Instance.LoadDataBlock(
                                                                    blockNumberInMemory);
                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .BlockState =
                                                                BlockState.Shared;
                                                            wordData = DataCache
                                                                .Blocks[blockNumberInCache]
                                                                .Words[wordNumberInBlock];
                                                            for (var i = 0;
                                                                i < Constants.CyclesMemory;
                                                                i++)
                                                            {
                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }

                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0
                                                            )
                                                            {
                                                                Monitor.Exit(
                                                                    DataCache.Blocks
                                                                        [blockNumberInCache]);
                                                                Monitor.Exit(DataBus.Instance);
                                                            }

                                                            ThreadStatuses[contextIndex] =
                                                                ThreadStatus.SolvedCacheFail;
                                                            hasFinishedLoad = true;


                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        // Ensure that the lock is released.
                                                        if (Monitor.IsEntered(
                                                            DataCache.OtherCache.Blocks
                                                                [blockNumberInOtherCache]))
                                                        {
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks
                                                                    [blockNumberInOtherCache]);
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
                                else if (hasReserved)
                                {
                                    if (Monitor.TryEnter(DataBus.Instance))
                                    {
                                        try
                                        {
                                            //ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;
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

                                            var blockNumberInOtherCache =
                                                blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                            // Try lock
                                            var hasTakenOtherBlock = false;
                                            while (!hasTakenOtherBlock)
                                            {
                                                if (Monitor.TryEnter(
                                                    DataCache.OtherCache.Blocks
                                                        [blockNumberInOtherCache]))
                                                {
                                                    try
                                                    {
                                                        hasTakenOtherBlock = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        DataCache.Blocks[blockNumberInCache].Label =
                                                            blockNumberInMemory;
                                                        // If the label matches with the block number it will be replaced the current block
                                                        var otherCacheBlock =
                                                            DataCache.OtherCache.Blocks[
                                                                blockNumberInOtherCache];
                                                        if (otherCacheBlock.Label ==
                                                            blockNumberInMemory &&
                                                            otherCacheBlock.BlockState ==
                                                            BlockState.Modified)
                                                        {
                                                            DataCache.OtherCache
                                                                    .Blocks[blockNumberInOtherCache]
                                                                    .BlockState =
                                                                BlockState.Shared;
                                                            Memory.Instance.StoreDataBlock(
                                                                blockNumberInMemory,
                                                                otherCacheBlock.Words);
                                                            DataCache.Blocks[blockNumberInCache].Words =
                                                                Memory.Instance.LoadDataBlock(
                                                                    blockNumberInMemory);
                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .BlockState =
                                                                BlockState.Shared;
                                                            wordData = DataCache
                                                                .Blocks[blockNumberInCache]
                                                                .Words[wordNumberInBlock];
                                                            // Add forty cycles
                                                            for (var i = 0;
                                                                i < Constants.CyclesMemory;
                                                                i++)
                                                            {
                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }

                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0
                                                            )
                                                            {
                                                                Monitor.Exit(
                                                                    DataCache.Blocks
                                                                        [blockNumberInCache]);
                                                                Monitor.Exit(DataBus.Instance);
                                                                Monitor.Exit(
                                                                    DataCache.OtherCache.Blocks[
                                                                        blockNumberInOtherCache]);
                                                            }

                                                            ThreadStatuses[contextIndex] = ThreadStatus.SolvedCacheFail;
                                                            hasFinishedLoad = true;

                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                        else // It will bring it from memory
                                                        {
                                                            //Release the lock in other cache because it is not needed
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks
                                                                    [blockNumberInOtherCache]);
                                                            DataCache.Blocks[blockNumberInCache].Words =
                                                                Memory.Instance.LoadDataBlock(
                                                                    blockNumberInMemory);
                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .BlockState =
                                                                BlockState.Shared;
                                                            wordData = DataCache
                                                                .Blocks[blockNumberInCache]
                                                                .Words[wordNumberInBlock];
                                                            for (var i = 0;
                                                                i < Constants.CyclesMemory;
                                                                i++)
                                                            {
                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }

                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0
                                                            )
                                                            {
                                                                Monitor.Exit(
                                                                    DataCache.Blocks
                                                                        [blockNumberInCache]);
                                                                Monitor.Exit(DataBus.Instance);
                                                            }

                                                            ThreadStatuses[contextIndex] =
                                                                ThreadStatus.SolvedCacheFail;
                                                            hasFinishedLoad = true;


                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        // Ensure that the lock is released.
                                                        if (Monitor.IsEntered(
                                                            DataCache.OtherCache.Blocks
                                                                [blockNumberInOtherCache]))
                                                        {
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks
                                                                    [blockNumberInOtherCache]);
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
                                else
                                {
                                    var hasAccesedReservations = false;
                                    while (!hasAccesedReservations)
                                    {
                                        if (Monitor.TryEnter(
                                            Reservations))
                                        {
                                            try
                                            {
                                                hasAccesedReservations = true;
                                                Reservations.Add(new Reservation(true, true, true, blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                            }
                                            finally
                                            {
                                                Monitor.Exit(
                                                    Reservations);
                                            }
                                        }
                                    }

                                    ThreadStatuses[contextIndex] = ThreadStatus.Waiting;
                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                }
                            }
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

                else
                {
                    hasAccesedBlockReservations = false;
                    while (!hasAccesedBlockReservations)
                    {
                        if (Monitor.TryEnter(
                            Reservations))
                        {
                            try
                            {
                                hasAccesedBlockReservations = true;
                                Reservations.Add(new Reservation(true, false, true, blockNumberInCache,
                                    Contexts[contextIndex].ThreadId));
                            }
                            finally
                            {
                                Monitor.Exit(
                                    Reservations);
                            }
                        }
                    }

                    ThreadStatuses[contextIndex] = ThreadStatus.Waiting;
                    Processor.Instance.ClockBarrier.SignalAndWait();
                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }

            return wordData;
        }

        protected override void StoreData(int address, int newData, int contextIndex)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
            var hasReserved = false;    
            var hasFinishedStore = false;
            var hasAccesedBlockReservations = false;
            while (!hasFinishedStore)
            {
                if (hasReserved || !IsBlockReserved(blockNumberInCache, true))
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
                                Processor.Instance.ClockBarrier.SignalAndWait();
                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                            }
                            else // It tries to get the bus
                            {
                                if (!hasReserved && !IsBusReserved(false))
                                {
                                    var hasAccesedReservations = false;
                                    while (!hasAccesedReservations)
                                    {
                                        if (Monitor.TryEnter(
                                            Reservations))
                                        {
                                            try
                                            {
                                                hasAccesedReservations = true;
                                                hasReserved = true;
                                                Reservations.Add(new Reservation(false, true, true,
                                                    blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                                Reservations.Add(new Reservation(false, false, true,
                                                    blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                            }
                                            finally
                                            {
                                                Monitor.Exit(
                                                    Reservations);
                                            }
                                        }
                                    }

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

                                            var blockNumberInOtherCache =
                                                blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                            // Try lock
                                            var hasTakenOtherBlock = false;
                                            while (!hasTakenOtherBlock)
                                            {
                                                if (Monitor.TryEnter(
                                                    DataCache.OtherCache.Blocks
                                                        [blockNumberInOtherCache]))
                                                {
                                                    try
                                                    {
                                                        hasTakenOtherBlock = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();

                                                        //If it is shared and the other cache block coincides it will invalidate other cache block
                                                        if (currentBlock.BlockState ==
                                                            BlockState.Shared &&
                                                            currentBlock.Label == blockNumberInMemory)
                                                        {
                                                            if (DataCache.OtherCache
                                                                    .Blocks[blockNumberInOtherCache]
                                                                    .Label ==
                                                                blockNumberInMemory)
                                                            {
                                                                DataCache.OtherCache
                                                                        .Blocks[blockNumberInOtherCache]
                                                                        .BlockState =
                                                                    BlockState.Invalid;
                                                            }

                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks[
                                                                    blockNumberInOtherCache]);

                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .Words[wordNumberInBlock] =
                                                                newData;
                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .BlockState =
                                                                BlockState.Modified;
                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0
                                                            )
                                                            {
                                                                Monitor.Exit(
                                                                    DataCache.Blocks
                                                                        [blockNumberInCache]);
                                                                Monitor.Exit(DataBus.Instance);
                                                            }

                                                            hasFinishedStore = true;

                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                        //If it is invalid or it is another label
                                                        else if (currentBlock.BlockState ==
                                                                 BlockState.Invalid ||
                                                                 currentBlock.Label !=
                                                                 blockNumberInMemory)
                                                        {
                                                            //ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;
                                                            DataCache.Blocks[blockNumberInCache].Label =
                                                                blockNumberInMemory;
                                                            // If the label matches with the block number and it is modified it will be replaced with the current block
                                                            var otherCacheBlock =
                                                                DataCache.OtherCache.Blocks[
                                                                    blockNumberInOtherCache];
                                                            if (otherCacheBlock.Label ==
                                                                blockNumberInMemory &&
                                                                otherCacheBlock.BlockState ==
                                                                BlockState.Modified)
                                                            {
                                                                DataCache.OtherCache
                                                                        .Blocks[blockNumberInOtherCache]
                                                                        .BlockState =
                                                                    BlockState.Shared;
                                                                Memory.Instance.StoreDataBlock(
                                                                    blockNumberInMemory,
                                                                    otherCacheBlock.Words);
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words =
                                                                    Memory.Instance.LoadDataBlock(
                                                                        blockNumberInMemory);
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .BlockState =
                                                                    BlockState.Shared;
                                                                // Add forty cycles
                                                                for (var i = 0;
                                                                    i < Constants.CyclesMemory;
                                                                    i++)
                                                                {
                                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }

                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
                                                                    newData;
                                                                //Just for follow the process
                                                                DataCache.OtherCache
                                                                        .Blocks[blockNumberInOtherCache]
                                                                        .BlockState =
                                                                    BlockState.Invalid;
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .BlockState =
                                                                    BlockState.Modified;
                                                                ThreadStatuses[contextIndex] =
                                                                    ThreadStatus.SolvedCacheFail;
                                                                Contexts[contextIndex].NumberOfCycles++;
                                                                RemainingThreadCycles[contextIndex]--;
                                                                if (RemainingThreadCycles
                                                                        [contextIndex] == 0)
                                                                {
                                                                    Monitor.Exit(
                                                                        DataCache.Blocks[
                                                                            blockNumberInCache]);
                                                                    Monitor.Exit(DataBus.Instance);
                                                                    Monitor.Exit(
                                                                        DataCache.OtherCache.Blocks[
                                                                            blockNumberInOtherCache]);
                                                                }

                                                                hasFinishedStore = true;

                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }
                                                            else if (otherCacheBlock.Label ==
                                                                     blockNumberInMemory &&
                                                                     otherCacheBlock.BlockState ==
                                                                     BlockState.Shared)
                                                            {
                                                                DataCache.OtherCache
                                                                        .Blocks[blockNumberInOtherCache]
                                                                        .BlockState
                                                                    = BlockState.Invalid;
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words =
                                                                    Memory.Instance.LoadDataBlock(
                                                                        blockNumberInMemory);
                                                                for (var i = 0;
                                                                    i < Constants.CyclesMemory;
                                                                    i++)
                                                                {
                                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }

                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
                                                                    newData;
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .BlockState =
                                                                    BlockState.Modified;
                                                                ThreadStatuses[contextIndex] =
                                                                    ThreadStatus.SolvedCacheFail;
                                                                Contexts[contextIndex].NumberOfCycles++;
                                                                RemainingThreadCycles[contextIndex]--;
                                                                if (RemainingThreadCycles
                                                                        [contextIndex] == 0)
                                                                {
                                                                    Monitor.Exit(
                                                                        DataCache.Blocks[
                                                                            blockNumberInCache]);
                                                                    Monitor.Exit(DataBus.Instance);
                                                                    Monitor.Exit(
                                                                        DataCache.OtherCache.Blocks[
                                                                            blockNumberInOtherCache]);
                                                                }

                                                                hasFinishedStore = true;

                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }
                                                            else //it has to bring it from memory
                                                            {
                                                                //Release the lock in other cache because it is not needed
                                                                Monitor.Exit(
                                                                    DataCache.OtherCache.Blocks
                                                                        [blockNumberInOtherCache]);
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words =
                                                                    Memory.Instance.LoadDataBlock(
                                                                        blockNumberInMemory);
                                                                for (var i = 0;
                                                                    i < Constants.CyclesMemory;
                                                                    i++)
                                                                {
                                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }

                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
                                                                    newData;
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .BlockState =
                                                                    BlockState.Modified;
                                                                ThreadStatuses[contextIndex] =
                                                                    ThreadStatus.SolvedCacheFail;
                                                                Contexts[contextIndex].NumberOfCycles++;
                                                                RemainingThreadCycles[contextIndex]--;
                                                                if (RemainingThreadCycles
                                                                        [contextIndex] == 0)
                                                                {
                                                                    Monitor.Exit(
                                                                        DataCache.Blocks[
                                                                            blockNumberInCache]);
                                                                    Monitor.Exit(DataBus.Instance);
                                                                }

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
                                                            DataCache.OtherCache.Blocks
                                                                [blockNumberInOtherCache]))
                                                        {
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks
                                                                    [blockNumberInOtherCache]);
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
                                else if (hasReserved)
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

                                            var blockNumberInOtherCache =
                                                blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                            // Try lock
                                            var hasTakenOtherBlock = false;
                                            while (!hasTakenOtherBlock)
                                            {
                                                if (Monitor.TryEnter(
                                                    DataCache.OtherCache.Blocks
                                                        [blockNumberInOtherCache]))
                                                {
                                                    try
                                                    {
                                                        hasTakenOtherBlock = true;
                                                        Processor.Instance.ClockBarrier.SignalAndWait();
                                                        Processor.Instance.ProcessorBarrier.SignalAndWait();

                                                        //If it is shared and the other cache block coincides it will invalidate other cache block
                                                        if (currentBlock.BlockState ==
                                                            BlockState.Shared &&
                                                            currentBlock.Label == blockNumberInMemory)
                                                        {
                                                            if (DataCache.OtherCache
                                                                    .Blocks[blockNumberInOtherCache]
                                                                    .Label ==
                                                                blockNumberInMemory)
                                                            {
                                                                DataCache.OtherCache
                                                                        .Blocks[blockNumberInOtherCache]
                                                                        .BlockState =
                                                                    BlockState.Invalid;
                                                            }

                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks[
                                                                    blockNumberInOtherCache]);

                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .Words[wordNumberInBlock] =
                                                                newData;
                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .BlockState =
                                                                BlockState.Modified;
                                                            Contexts[contextIndex].NumberOfCycles++;
                                                            RemainingThreadCycles[contextIndex]--;
                                                            if (RemainingThreadCycles[contextIndex] == 0
                                                            )
                                                            {
                                                                Monitor.Exit(
                                                                    DataCache.Blocks
                                                                        [blockNumberInCache]);
                                                                Monitor.Exit(DataBus.Instance);
                                                            }

                                                            hasFinishedStore = true;

                                                            Processor.Instance.ClockBarrier.SignalAndWait();
                                                            Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        }
                                                        //If it is invalid or it is another label
                                                        else if (currentBlock.BlockState ==
                                                                 BlockState.Invalid ||
                                                                 currentBlock.Label !=
                                                                 blockNumberInMemory)
                                                        {
                                                            //ThreadStatuses[contextIndex] = ThreadStatus.CacheFail;
                                                            DataCache.Blocks[blockNumberInCache].Label =
                                                                blockNumberInMemory;
                                                            // If the label matches with the block number and it is modified it will be replaced with the current block
                                                            var otherCacheBlock =
                                                                DataCache.OtherCache.Blocks[
                                                                    blockNumberInOtherCache];
                                                            if (otherCacheBlock.Label ==
                                                                blockNumberInMemory &&
                                                                otherCacheBlock.BlockState ==
                                                                BlockState.Modified)
                                                            {
                                                                DataCache.OtherCache
                                                                        .Blocks[blockNumberInOtherCache]
                                                                        .BlockState =
                                                                    BlockState.Shared;
                                                                Memory.Instance.StoreDataBlock(
                                                                    blockNumberInMemory,
                                                                    otherCacheBlock.Words);
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words =
                                                                    Memory.Instance.LoadDataBlock(
                                                                        blockNumberInMemory);
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .BlockState =
                                                                    BlockState.Shared;
                                                                // Add forty cycles
                                                                for (var i = 0;
                                                                    i < Constants.CyclesMemory;
                                                                    i++)
                                                                {
                                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }

                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
                                                                    newData;
                                                                //Just for follow the process
                                                                DataCache.OtherCache
                                                                        .Blocks[blockNumberInOtherCache]
                                                                        .BlockState =
                                                                    BlockState.Invalid;
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .BlockState =
                                                                    BlockState.Modified;
                                                                ThreadStatuses[contextIndex] =
                                                                    ThreadStatus.SolvedCacheFail;
                                                                Contexts[contextIndex].NumberOfCycles++;
                                                                RemainingThreadCycles[contextIndex]--;
                                                                if (RemainingThreadCycles
                                                                        [contextIndex] == 0)
                                                                {
                                                                    Monitor.Exit(
                                                                        DataCache.Blocks[
                                                                            blockNumberInCache]);
                                                                    Monitor.Exit(DataBus.Instance);
                                                                    Monitor.Exit(
                                                                        DataCache.OtherCache.Blocks[
                                                                            blockNumberInOtherCache]);
                                                                }

                                                                hasFinishedStore = true;

                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }
                                                            else if (otherCacheBlock.Label ==
                                                                     blockNumberInMemory &&
                                                                     otherCacheBlock.BlockState ==
                                                                     BlockState.Shared)
                                                            {
                                                                DataCache.OtherCache
                                                                        .Blocks[blockNumberInOtherCache]
                                                                        .BlockState
                                                                    = BlockState.Invalid;
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words =
                                                                    Memory.Instance.LoadDataBlock(
                                                                        blockNumberInMemory);
                                                                for (var i = 0;
                                                                    i < Constants.CyclesMemory;
                                                                    i++)
                                                                {
                                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }

                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
                                                                    newData;
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .BlockState =
                                                                    BlockState.Modified;
                                                                ThreadStatuses[contextIndex] =
                                                                    ThreadStatus.SolvedCacheFail;
                                                                Contexts[contextIndex].NumberOfCycles++;
                                                                RemainingThreadCycles[contextIndex]--;
                                                                if (RemainingThreadCycles
                                                                        [contextIndex] == 0)
                                                                {
                                                                    Monitor.Exit(
                                                                        DataCache.Blocks[
                                                                            blockNumberInCache]);
                                                                    Monitor.Exit(DataBus.Instance);
                                                                    Monitor.Exit(
                                                                        DataCache.OtherCache.Blocks[
                                                                            blockNumberInOtherCache]);
                                                                }

                                                                hasFinishedStore = true;

                                                                Processor.Instance.ClockBarrier.SignalAndWait();
                                                                Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            }
                                                            else //it has to bring it from memory
                                                            {
                                                                //Release the lock in other cache because it is not needed
                                                                Monitor.Exit(
                                                                    DataCache.OtherCache.Blocks
                                                                        [blockNumberInOtherCache]);
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words =
                                                                    Memory.Instance.LoadDataBlock(
                                                                        blockNumberInMemory);
                                                                for (var i = 0;
                                                                    i < Constants.CyclesMemory;
                                                                    i++)
                                                                {
                                                                    Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }

                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
                                                                    newData;
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .BlockState =
                                                                    BlockState.Modified;
                                                                ThreadStatuses[contextIndex] =
                                                                    ThreadStatus.SolvedCacheFail;
                                                                Contexts[contextIndex].NumberOfCycles++;
                                                                RemainingThreadCycles[contextIndex]--;
                                                                if (RemainingThreadCycles
                                                                        [contextIndex] == 0)
                                                                {
                                                                    Monitor.Exit(
                                                                        DataCache.Blocks[
                                                                            blockNumberInCache]);
                                                                    Monitor.Exit(DataBus.Instance);
                                                                }

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
                                                            DataCache.OtherCache.Blocks
                                                                [blockNumberInOtherCache]))
                                                        {
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks
                                                                    [blockNumberInOtherCache]);
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
                                else
                                {
                                    var hasAccesedReservations = false;
                                    while (!hasAccesedReservations)
                                    {
                                        if (Monitor.TryEnter(
                                            Reservations))
                                        {
                                            try
                                            {
                                                hasAccesedReservations = true;
                                                Reservations.Add(new Reservation(true, true, true, blockNumberInCache,
                                                    Contexts[contextIndex].ThreadId));
                                            }
                                            finally
                                            {
                                                Monitor.Exit(
                                                    Reservations);
                                            }
                                        }
                                    }

                                    ThreadStatuses[contextIndex] = ThreadStatus.Waiting;
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

                else
                {
                    hasAccesedBlockReservations = false;
                    while (!hasAccesedBlockReservations)
                    {
                        if (Monitor.TryEnter(
                            Reservations))
                        {
                            try
                            {
                                hasAccesedBlockReservations = true;
                                Reservations.Add(new Reservation(true, false, true, blockNumberInCache,
                                    Contexts[contextIndex].ThreadId));
                            }
                            finally
                            {
                                Monitor.Exit(
                                    Reservations);
                            }
                        }
                    }

                    ThreadStatuses[contextIndex] = ThreadStatus.Waiting;
                    Processor.Instance.ClockBarrier.SignalAndWait();
                    Processor.Instance.ProcessorBarrier.SignalAndWait();
                }
            }
        }
    }
}