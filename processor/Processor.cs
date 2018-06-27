﻿using System.Collections.Generic;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.common;
using ProcessorSimulator.core;
using ProcessorSimulator.memory;

namespace ProcessorSimulator.processor
{
    public sealed class Processor
    {
        private static volatile Processor _instance;
        private static readonly object Padlock = new object();

        private Processor()
        {
            Clock = 0;
            ClockBarrier = new Barrier(3);
            ProcessorBarrier = new Barrier(3);
            ContextQueue = new Queue<Context>();
            Quantum = 100;
            ContextList = new List<Context>();
            CoreZeroThreadA = new Thread(StartMainThreadCoreZero);
            CoreZeroThreadB = new Thread(StartSecThreadCoreZero);
            CoreOneThread = new Thread(StartCoreOne);
            InitializeStructures();
        }

        public static Processor Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (Padlock)
                    {
                        if (_instance == null)
                            _instance = new Processor();
                    }
                }

                return _instance;
            }
        }

        private void StartMainThreadCoreZero()
        {
            var context = GetNewContext();
            if (context == null)
            {
                FinalizeHighLevelThread(CoreZeroThreadA);
            }
            else
            {
                context.HasPriority = true;
                CoreZero.StartExecution(context, Constants.FirstContextIndex);
            }              
        }

        private void StartSecThreadCoreZero()
        {
            var context = GetNewContext();
            if (context == null)
            {
                FinalizeHighLevelThread(CoreZeroThreadB);
            }
            else
            {
                CoreZero.StartExecution(context, Constants.SecondContextIndex);

            }
        }

        private void StartCoreOne()
        {
            var context = GetNewContext();
            if (context == null)
            {
                FinalizeHighLevelThread(CoreOneThread);
            }
            else
            {
                context.HasPriority = true;
                CoreOne.StartExecution(context,  Constants.FirstContextIndex); 

            }
        }

        public Thread CoreZeroThreadA { get; set; }

        public Thread CoreZeroThreadB { get; set; }

        public Thread CoreOneThread { get; set; }

        public DobleCore CoreZero { get; set; }

        public Core CoreOne { get; set; }

        public int Clock { get; set; }

        public Barrier ClockBarrier { get; set; }

        public Queue<Context> ContextQueue { get; set; }

        public List<Context> ContextList { get; set; }

        public int Quantum { get; set; }

        public Barrier ProcessorBarrier { get; set; }

        private void InitializeStructures()
        {
            /** Initialize the data block of main Memory **/
            var dataBlocks = new Block<int>[Constants.DataBlocksInMemory];
            for (var i = 0; i < Constants.DataBlocksInMemory; i++)
            {
                var words = new int[Constants.WordsInBlock];
                for (var j = 0; j < Constants.WordsInBlock; j++)
                {
                    words[j] = Constants.DefaultDataValue;
                }

                dataBlocks[i] = new Block<int>(words);
            }

            /* Now we initialize the instruction block of main memory and we fill up the context queue*/
            var pc = 0;
            var blockNum = 0;
            var wordNum = 0;
            var instructionBlocks = new Block<Instruction>[Constants.InstructionBlocksInMemory];
            Instruction[] instructionArray = null;

            for (var i = 0; i < Constants.NumberOfThreadsToLoad; i++)
            {
                ContextQueue.Enqueue(new Context(pc, i));
                var filePath = Constants.FilePath + i + Constants.FileExtension;
                string line;

                // Read the file and display it line by line.  
                var file = new System.IO.StreamReader(filePath);
                const char delimiter = ' '; // Whitespace.
                while ((line = file.ReadLine()) != null)
                {
                    // Get instruction from line
                    var numberStrings = line.Split(delimiter);
                    var opCode = int.Parse(numberStrings[0]);
                    var source = int.Parse(numberStrings[1]);
                    var destiny = int.Parse(numberStrings[2]);
                    var inmediate = int.Parse(numberStrings[3]);
                    var instruction = new Instruction(opCode, source, destiny, inmediate);

                    if (instructionArray == null)
                    {
                        instructionArray = new Instruction[Constants.WordsInBlock];
                    }

                    instructionArray[wordNum++] = instruction;
                    pc += 4;

                    if (wordNum != Constants.WordsInBlock) continue;
                    instructionBlocks[blockNum++] = new Block<Instruction>(instructionArray);
                    wordNum = 0;
                    instructionArray = null;
                }
                file.Close();
            }

            // If the instructions in the .txt files did not fit evenly in the blocks.
            if (instructionArray != null)
            {
                // Now we have to fill the instruction array and save it to the last block
                for (var i = wordNum; i < Constants.WordsInBlock; i++)
                {
                    instructionArray[i] = new Instruction();
                }

                instructionBlocks[blockNum++] = new Block<Instruction>(instructionArray);
            }

            // Now we proceed to fill the remaining empty blocks (if any) with the standard default instruction
            for (var i = blockNum; i < Constants.InstructionBlocksInMemory; i++)
            {
                instructionArray = new Instruction[Constants.WordsInBlock];
                for (var j = 0; j < Constants.WordsInBlock; j++)
                {
                    instructionArray[j] = new Instruction();
                }

                instructionBlocks[i] = new Block<Instruction>(instructionArray);
            }

            /*
             * At this point the instruction block is ready.
             * Now, initialize the Memory structure.
             */
            Memory.Instance.InstructionBlocks = instructionBlocks;
            Memory.Instance.DataBlocks = dataBlocks;

            // Creates the four caches. Two per core
            var dataCacheZero = new Cache<int>(Constants.CoreZeroCacheSize);
            var instructionCacheZero = new Cache<Instruction>(Constants.CoreZeroCacheSize);
            var dataCacheOne = new Cache<int>(Constants.CoreOneCacheSize);
            var instructionCacheOne = new Cache<Instruction>(Constants.CoreOneCacheSize);

            // Set each cache with the other cache connected to it.
            dataCacheZero.OtherCache = dataCacheOne;
            dataCacheOne.OtherCache = dataCacheZero;
            instructionCacheZero.OtherCache = instructionCacheOne;
            instructionCacheOne.OtherCache = instructionCacheZero;

            // Creates the two cores of the processor
            CoreOne = new Core(instructionCacheOne, dataCacheOne)
            {
                ThreadStatuses = {[Constants.FirstContextIndex] = ThreadStatus.Running}                
            };

            CoreZero = new DobleCore(instructionCacheZero, dataCacheZero)
            {
                ThreadStatuses =
                {
                    [Constants.FirstContextIndex] = ThreadStatus.Running,
                    [Constants.SecondContextIndex] = ThreadStatus.Dead
                }
            };
        }

        private bool Check()
        {
            // Check if there are threads that have ended execution
            if (CoreZero.ThreadStatuses[Constants.FirstContextIndex] == ThreadStatus.Ended)
            {
                LoadNewContext(CoreZero, CoreZeroThreadA, Constants.FirstContextIndex);
                // Set the other context as the one with priority
                CoreZero.Contexts[Constants.SecondContextIndex].HasPriority = true;
            }

            if (CoreOne.ThreadStatuses[Constants.FirstContextIndex] == ThreadStatus.Ended)
            {
                LoadNewContext(CoreOne, CoreOneThread, Constants.FirstContextIndex);
            }

            if (CoreZero.ThreadStatuses[Constants.SecondContextIndex] == ThreadStatus.Ended)
            {
                LoadNewContext(CoreZero, CoreZeroThreadB, Constants.SecondContextIndex);
                // Set the other context as the one with priority
                CoreZero.Contexts[Constants.FirstContextIndex].HasPriority = true;
            }


            // Check if there are threads that have ran out of the cycles
            if (CoreZero.RemainingThreadCycles[Constants.FirstContextIndex] == 0)
            {
                SwapContext(CoreZero, Constants.FirstContextIndex);
                // Set the other context as the one with priority
                CoreZero.Contexts[Constants.SecondContextIndex].HasPriority = true;
            }

            if (CoreOne.RemainingThreadCycles[Constants.FirstContextIndex] == 0)
            {
                SwapContext(CoreOne, Constants.FirstContextIndex);
            }

            if (CoreZero.RemainingThreadCycles[Constants.SecondContextIndex] == 0)
            {
                SwapContext(CoreZero, Constants.SecondContextIndex);
                // Set the other context as the one with priority
                CoreZero.Contexts[Constants.FirstContextIndex].HasPriority = true;
            }


            // Check if either thread in Core Zero has just resolved a Cache Fail
            var statuses = CheckIfSolvedCacheFail(CoreZero.ThreadStatuses[Constants.FirstContextIndex],
                CoreZero.ThreadStatuses[Constants.SecondContextIndex], CoreZero.Contexts[Constants.FirstContextIndex],
                CoreZeroThreadA, CoreZeroThreadB);
            CoreZero.ThreadStatuses[Constants.FirstContextIndex] = statuses[0];
            CoreZero.ThreadStatuses[Constants.SecondContextIndex] = statuses[1];

            statuses = CheckIfSolvedCacheFail(CoreZero.ThreadStatuses[Constants.SecondContextIndex], CoreZero.ThreadStatuses[Constants.FirstContextIndex],
                CoreZero.Contexts[Constants.FirstContextIndex],
                CoreZeroThreadB, CoreZeroThreadA);
            CoreZero.ThreadStatuses[Constants.SecondContextIndex] = statuses[0];
            CoreZero.ThreadStatuses[Constants.FirstContextIndex] = statuses[1];


            // Check for reservations, and resume waiting threads if the right conditions hold.
            if (CoreZero.ThreadStatuses[Constants.FirstContextIndex] == ThreadStatus.Waiting)
            {
                var thStatuses = CheckForWaitingThreads(CoreZero.Contexts[Constants.FirstContextIndex],
                    CoreZero.ThreadStatuses[Constants.SecondContextIndex], CoreZeroThreadA,
                    CoreZeroThreadB);
                if (thStatuses != null)
                {
                    CoreZero.ThreadStatuses[Constants.FirstContextIndex] = thStatuses[0];
                    CoreZero.ThreadStatuses[Constants.SecondContextIndex] = thStatuses[1];
                }
            }

            if (CoreZero.ThreadStatuses[Constants.SecondContextIndex] == ThreadStatus.Waiting)
            {
                var thStatuses = CheckForWaitingThreads(CoreZero.Contexts[Constants.SecondContextIndex],
                    CoreZero.ThreadStatuses[Constants.FirstContextIndex], CoreZeroThreadB,
                    CoreZeroThreadA);
                if (thStatuses != null)
                {
                    CoreZero.ThreadStatuses[Constants.FirstContextIndex] = thStatuses[0];
                    CoreZero.ThreadStatuses[Constants.SecondContextIndex] = thStatuses[1];
                }
            }

            // If there is a Cache fail in the main thread of core zero we might have to start another thread
            if (CoreZero.ThreadStatuses[Constants.FirstContextIndex] == ThreadStatus.CacheFail)
            {
                // Check if there is no other thread running, and there are available threads in the context queue
                if (CoreZero.ThreadStatuses[Constants.SecondContextIndex] == ThreadStatus.Dead && ContextQueue.Count > 0)
                {
                    CoreZero.ThreadStatuses[Constants.SecondContextIndex] = ThreadStatus.Running;
                    return true;
                }
            }
            return false;
        }

        private void LoadNewContext(Core core, Thread thread, int contextIndex)
        {
            var oldContext = core.Contexts[contextIndex];
            ContextList.Add(oldContext); // Adds the ending context for statistic purposes
            var newContext = GetNewContext();
            if (newContext != null)
            {
                core.Contexts[contextIndex] = newContext;
                core.RemainingThreadCycles[contextIndex] = Quantum; // Sets the quantum as the remaining cycles for the new thread
            }
            else
            {
                // No more threads to run, so high level threads finishes execution 
                core.ThreadStatuses[contextIndex] = ThreadStatus.Dead;
                FinalizeHighLevelThread(thread);
            }
        }

        private Context GetNewContext()
        {
            if (ContextQueue.Count == 0)
            {
                return null;
            }
            return ContextQueue.Dequeue();
        }

        private ThreadStatus[] CheckForWaitingThreads(Context context, ThreadStatus oStatus, Thread threadA,
            Thread threadB)
        {
            ThreadStatus baseStatus;
            ThreadStatus oThreadStatus;
            var reservations = CoreZero.Reservations;
            Reservation waitingCause = null;
            foreach (var reservation in reservations)
            {
                // Here we find out the resource the thread was waiting for
                if (reservation.ThreadId != context.ThreadId || reservation.IsWaiting == false) continue;
                waitingCause = reservation;
                break;
            }

            // If the reservation that caused to the thread to wait is still there then we can not resume yet.
            if (FindBlockingReservation(waitingCause)) return null;

            // Waiting reservation is now absent, we then resume the thread.
            // If the other thread is running but we have priority
            if (context.HasPriority)
            {
                oThreadStatus = ThreadStatus.Stopped;
                StopHighLevelThread(threadB);
                baseStatus = ThreadStatus.Running;
                ResumeHighLevelThread(threadA);
            }
            else
            {
                baseStatus = ThreadStatus.Stopped;
                oThreadStatus = oStatus;
            }

            ThreadStatus[] statuses = {baseStatus, oThreadStatus};
            return statuses;
        }

        private bool FindBlockingReservation(Reservation waitingReservation)
        {
            var usingBus = waitingReservation.IsUsingBus;
            var inDataCache = waitingReservation.IsInDataCache;
            var blockNum = waitingReservation.BlockNumberInCache;
            var found = false;
            var reservations = CoreZero.Reservations;
            foreach (var reservation in reservations)
            {
                if (usingBus && reservation.IsUsingBus)
                {
                    // We then check if they are using the same cache
                    if (inDataCache != reservation.IsInDataCache) continue;
                    found = true;
                    break;
                }

                // Check if block labels match
                if (blockNum != reservation.BlockNumberInCache || inDataCache != reservation.IsInDataCache) continue;
                found = true;
                break;
            }
            return found;
        }

        private void SwapContext(Core core, int contextIndex)
        {
            // If context queue is empty then keep running the same thread
            if (ContextQueue.Count <= 0)
            {
                var newContext = ContextQueue.Dequeue();
                var oldContext = core.Contexts[contextIndex];
                oldContext.HasPriority = false;
                ContextQueue.Enqueue(oldContext);
                core.Contexts[contextIndex] = newContext;
            }

            core.RemainingThreadCycles[contextIndex] = Quantum; // Restores remaining cycles to the quantum value.
        }

        private ThreadStatus[] CheckIfSolvedCacheFail(ThreadStatus baseStatus, ThreadStatus otherStatus,
            Context baseContext, Thread baseThread, Thread otherThread)
        {
            if (baseStatus == ThreadStatus.SolvedCacheFail)
            {
                // Check if there is another thread running
                if (otherStatus == ThreadStatus.Running)
                {
                    // Check if the thread that solved Cache Fail has priority
                    if (baseContext.HasPriority)
                    {
                        // Secondary thread must be stopped
                        otherStatus = ThreadStatus.Stopped;
                        // Suspends secundary thread
                        StopHighLevelThread(otherThread);
                        // Sets primary thread in the running state
                        baseStatus = ThreadStatus.Running;
                    }
                    else
                    {
                        // Primary thread has no priority, then it must be stopped
                        baseStatus = ThreadStatus.Stopped;
                        ResumeHighLevelThread(baseThread);
                    }
                }
                else
                {
                    // If there is no secundary thread running, then the primary resumes
                    // Sets primary thread in the running state
                    baseStatus = ThreadStatus.Running;
                }
            }

            ThreadStatus[] threadStatuses = {baseStatus, otherStatus};
            return threadStatuses;
        }

        private void StopHighLevelThread(Thread thread)
        {
            thread.Suspend();
            ClockBarrier.RemoveParticipant();
            ProcessorBarrier.RemoveParticipant();
        }

        private void ResumeHighLevelThread(Thread thread)
        {
            thread.Resume();
            ClockBarrier.AddParticipant();
            ProcessorBarrier.AddParticipant();  
        }

        private void FinalizeHighLevelThread(Thread thread)
        {
            thread.Abort();
            ClockBarrier.RemoveParticipant();
            ProcessorBarrier.RemoveParticipant();
        }

        public void RunSimulation(bool slowMotion)
        {
            CoreZeroThreadA.Start();
            CoreOneThread.Start();
            while (ClockBarrier.ParticipantCount > 1)
            {
                ClockBarrier.SignalAndWait();
                if (slowMotion && Clock % Constants.SlowMotionCycles == 0)
                {
                    System.Console.WriteLine("**********************************************************");
                    System.Console.WriteLine("Clock : " + Clock);
                    System.Console.WriteLine("Core Zero Main thread number: " +
                                             CoreZero.Contexts[Constants.FirstContextIndex].ThreadId);
                    if (CoreZero.Contexts[Constants.SecondContextIndex] != null)
                    {
                        System.Console.WriteLine("Core Zero Sec thread number: " +
                                                 CoreZero.Contexts[Constants.SecondContextIndex].ThreadId);
                    }
                    else
                    {
                        System.Console.WriteLine("Core Zero Secundary thread not running yet");
                    }
                    System.Console.WriteLine("Core One thread number: " +
                                             CoreOne.Contexts[Constants.FirstContextIndex].ThreadId);
                    System.Console.WriteLine("***********************************************************");
                }

                Clock++;
                var startSecThread = Check();
                if (startSecThread)
                {
                    ClockBarrier.AddParticipant();
                }
                //Thread.Sleep(Constants.DelayTime);
                ProcessorBarrier.SignalAndWait();
                if (startSecThread)
                {
                    ProcessorBarrier.AddParticipant();
                    CoreZeroThreadB.Start();
                }
            }

            // At this point the simulation has ended
            // First display the data contents of main memory
            System.Console.WriteLine("Displaying Main Memory:");
            System.Console.Write(Memory.Instance.ToString());
            System.Console.WriteLine("***********************************************************");

            // Now display Data Caches
            System.Console.WriteLine("Displaying Data Cache from Core Zero:");
            System.Console.Write(CoreZero.DataCache.ToString());
            System.Console.WriteLine("***********************************************************");

            System.Console.WriteLine("Displaying Data Cache from Core One:");
            System.Console.Write(CoreOne.DataCache.ToString());
            System.Console.WriteLine("***********************************************************");

            // Finally display the statistics of each thread
            System.Console.WriteLine("Displaying statistics for each thread:");
            foreach (var context in ContextList)
            {
                System.Console.WriteLine(context.ToString());
                System.Console.WriteLine("***********************************************************");
            }
        }
    }
}