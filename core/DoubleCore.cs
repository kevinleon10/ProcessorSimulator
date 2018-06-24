using System;
using System.Collections.Generic;
using System.Data;
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
            InstructionRegisterTwo = null;
            RemainingThreadCycles = Constants.NotRunningAnyThread;
            Reservations = new List<Reservation>();
        }

        public Instruction InstructionRegisterTwo { get; set; }

        public Context ContextTwo { get; set; }

        public int RemainingThreadCyclesTwo { get; set; }

        public ThreadStatus ThreadStatusTwo { get; set; }

        public List<Reservation> Reservations { get; set; }

        private int BlockPositionInReservations(int blockNumberInCache)
        {
            var blockPositionInReservations = 0;
            while (blockPositionInReservations < Reservations.Count)
            {
                if (blockNumberInCache == Reservations[blockPositionInReservations].BlockLabel)
                {
                    return blockPositionInReservations;
                }

                ++blockPositionInReservations;
            }

            Reservations.Add(new Reservation(true, false, false, blockNumberInCache, Context.ThreadId));
            return blockPositionInReservations;
        }

        public override void StartExecution(Context context)
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

            ThreadStatus = ThreadStatus.Ended;
        }

        protected override int LoadData(int address)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            var wordData = 0;
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
            var hasFinishedLoad = false;
            // Wwhile it has not finished the load it continues trying
            while (!hasFinishedLoad)
            {
                var blockPositionInReservations = BlockPositionInReservations(blockNumberInCache);
                // If it is not reserved it can try to lock
                if (!Reservations[blockPositionInReservations].IsInDateCache)
                {
                    Reservations[blockPositionInReservations].IsInDateCache = true;
                    Reservations[blockPositionInReservations].IsWaiting = false;

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
                            }
                            else // It tryes to get the bus
                            {
                                // It checks if it is not reserved
                                if (!Reservations[blockPositionInReservations].IsUsingBus)
                                {
                                    Reservations[blockPositionInReservations].IsUsingBus = true;
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
                                                Memory.Instance.StoreDataBlock(currentBlock.Label,
                                                    currentBlock.Words);
                                                // Add forty cycles
                                                /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                {
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                }*/
                                            }

                                            var blockNumberInOtherCache =
                                                blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                            // Try lock
                                            var hasTakenOtherBlock = false;
                                            while (!hasTakenOtherBlock)
                                            {
                                                if (Monitor.TryEnter(
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                                {
                                                    try
                                                    {
                                                        hasTakenOtherBlock = true;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                        DataCache.Blocks[blockNumberInCache].Label =
                                                            blockNumberInMemory;
                                                        // If the label matches with the block number it will be replaced the current block
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
                                                        }
                                                        else // It will bring it from memory
                                                        {
                                                            //Release the lock in other cache because it is not needed
                                                            Monitor.Exit(
                                                                DataCache.OtherCache.Blocks[blockNumberInOtherCache]);
                                                            DataCache.Blocks[blockNumberInCache].Words =
                                                                Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                            DataCache.Blocks[blockNumberInCache].BlockState =
                                                                BlockState.Shared;
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
                                            Monitor.Exit(DataBus.Instance);
                                        }
                                    }
                                    else
                                    {
                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                    }
                                }
                                else
                                {
                                    ThreadStatus = ThreadStatus.Waiting;
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
                else
                {
                    ThreadStatus = ThreadStatus.Waiting;
                }
            }

            return wordData;
        }

        protected override void StoreData(int address, int newData)
        {
            var blockNumberInMemory = GetBlockNumberInMemory(address);
            var wordNumberInBlock = GetWordNumberInBlock(address);
            var blockNumberInCache = blockNumberInMemory % DataCache.CacheSize;
            var hasFinishedStore = false;
            while (!hasFinishedStore)
            {
                var blockPositionInReservations = BlockPositionInReservations(blockNumberInCache);
                // If it is not reserved it can try to lock
                if (!Reservations[blockPositionInReservations].IsInDateCache)
                {
                    Reservations[blockPositionInReservations].IsInDateCache = true;
                    Reservations[blockPositionInReservations].IsWaiting = false;
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
                            }
                            else // It tries to get the bus
                            {
                                // It checks if it is not reserved
                                if (!Reservations[blockPositionInReservations].IsUsingBus)
                                {
                                    Reservations[blockPositionInReservations].IsUsingBus = true;
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
                                                /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                {
                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                }*/
                                            }

                                            var blockNumberInOtherCache =
                                                blockNumberInMemory % DataCache.OtherCache.CacheSize;
                                            // Try lock
                                            var hasTakenOtherBlock = false;
                                            while (!hasTakenOtherBlock)
                                            {
                                                if (Monitor.TryEnter(
                                                    DataCache.OtherCache.Blocks[blockNumberInOtherCache]))
                                                {
                                                    try
                                                    {
                                                        hasTakenOtherBlock = true;
                                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();

                                                        //If it is shared it will invalidate the other cache block
                                                        if (currentBlock.BlockState == BlockState.Shared &&
                                                            currentBlock.Label == blockNumberInMemory &&
                                                            DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                .Label ==
                                                            blockNumberInMemory)
                                                        {
                                                            DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                    .BlockState =
                                                                BlockState.Invalid;
                                                            DataCache.Blocks[blockNumberInCache]
                                                                    .Words[wordNumberInBlock] =
                                                                newData;
                                                            DataCache.Blocks[blockNumberInCache].BlockState =
                                                                BlockState.Modified;
                                                            Context.NumberOfCycles++;
                                                            RemainingThreadCycles--;
                                                            //Processor.Instance.ClockBarrier.SignalAndWait();
                                                            //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                            hasFinishedStore = true;
                                                        }
                                                        //If it is invalid or it is another label
                                                        else if (currentBlock.BlockState == BlockState.Invalid ||
                                                                 currentBlock.Label != blockNumberInMemory)
                                                        {
                                                            ThreadStatus = ThreadStatus.CacheFail;
                                                            DataCache.Blocks[blockNumberInCache].Label =
                                                                blockNumberInMemory;
                                                            // If the label matches with the block number and it is modified it will be replaced with the current block
                                                            var otherCacheBlock =
                                                                DataCache.OtherCache.Blocks[
                                                                    blockNumberInOtherCache]; // CUIDADO
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
                                                                /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                                {
                                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }*/
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
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
                                                            }
                                                            else if (otherCacheBlock.Label ==
                                                                     blockNumberInMemory &&
                                                                     otherCacheBlock.BlockState ==
                                                                     BlockState.Shared)
                                                            {
                                                                DataCache.OtherCache.Blocks[blockNumberInOtherCache]
                                                                        .BlockState
                                                                    = BlockState.Invalid;
                                                                DataCache.Blocks[blockNumberInCache].Words =
                                                                    Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                                /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                                {
                                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }*/
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
                                                                    newData;
                                                                DataCache.Blocks[blockNumberInCache].BlockState =
                                                                    BlockState.Modified;
                                                                ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                                Context.NumberOfCycles++;
                                                                RemainingThreadCycles--;
                                                                //Processor.Instance.ClockBarrier.SignalAndWait();
                                                                //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                hasFinishedStore = true;
                                                            }
                                                            else //it has to bring it from memory
                                                            {
                                                                //Release the lock in other cache because it is not needed
                                                                Monitor.Exit(
                                                                    DataCache.OtherCache.Blocks
                                                                        [blockNumberInOtherCache]);
                                                                DataCache.Blocks[blockNumberInCache].Words =
                                                                    Memory.Instance.LoadDataBlock(blockNumberInMemory);
                                                                /*for (var i = 0; i < Constants.CyclesMemory; i++)
                                                                {
                                                                    //Processor.Instance.ClockBarrier.SignalAndWait();
                                                                    //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                }*/
                                                                DataCache.Blocks[blockNumberInCache]
                                                                        .Words[wordNumberInBlock] =
                                                                    newData;
                                                                DataCache.Blocks[blockNumberInCache].BlockState =
                                                                    BlockState.Modified;
                                                                ThreadStatus = ThreadStatus.SolvedCacheFail;
                                                                Context.NumberOfCycles++;
                                                                RemainingThreadCycles--;
                                                                //Processor.Instance.ClockBarrier.SignalAndWait();
                                                                //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                                                hasFinishedStore = true;
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
                                            Monitor.Exit(DataBus.Instance);
                                        }
                                    }
                                    else
                                    {
                                        //Processor.Instance.ClockBarrier.SignalAndWait();
                                        //Processor.Instance.ProcessorBarrier.SignalAndWait();
                                    }
                                }
                                else
                                {
                                    ThreadStatus = ThreadStatus.Waiting;
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
                else
                {
                    ThreadStatus = ThreadStatus.Waiting;
                }
            }
        }
    }
}