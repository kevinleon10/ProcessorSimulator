using System.Collections.Generic;
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
        private static readonly Processor instance = new Processor();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static Processor()
        {
        }

        private Processor()
        {
            Clock = 0;
            ClockBarrier = new Barrier(3);
            ProcessorBarrier = new Barrier(3);
            ContextQueue = new Queue<Context>();
            Quantum = 0;
            InitializeStructures();
        }

        //Singleton instance
        public static Processor Instance
        {
            get
            {
                return instance;
            }
        }

        public Thread CoreZeroThreadA { get; private set; }
        
        public Thread CoreZeroThreadB { get; private set; }
        
        public Thread CoreOneThread { get; private set; }

        public DobleCore CoreZero { get; set; }

        public Core CoreOne { get; set; }

        public int Clock { get; set; }

        public Barrier ClockBarrier { get; set; }

        public Queue<Context> ContextQueue { get; set; }

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
                while((line = file.ReadLine()) != null)  
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
            CoreOne = new Core(instructionCacheOne, dataCacheOne);
            CoreZero = new DobleCore(instructionCacheZero, dataCacheZero);    
        }

        public void Check()
        {
            // Check if there are threads that have ran out of the cycles
            if (CoreZero.RemainingThreadCycles == 0)
            {
                LoadContextMainThread(CoreZero);
                // Set the other context as the one with priority
                CoreZero.ContextTwo.HasPriority = true;
            }

            if (CoreOne.RemainingThreadCycles == 0)
            {
                LoadContextMainThread(CoreOne);
            }

            if (CoreZero.RemainingThreadCyclesTwo == 0)
            {
                LoadContextSecThread(CoreZero);
                // Set the other context as the one with priority
                CoreZero.Context.HasPriority = true;
            }

            // Check if either thread in Core Zero has just resolved a Cache Fail
            var statuses = CheckIfSolvedCacheFail(CoreZero.ThreadStatus, CoreZero.ThreadStatusTwo, CoreZero.Context,
                CoreZeroThreadA, CoreZeroThreadB);
            CoreZero.ThreadStatus = statuses[0];
            CoreZero.ThreadStatusTwo = statuses[1];
            
            statuses = CheckIfSolvedCacheFail(CoreZero.ThreadStatusTwo, CoreZero.ThreadStatus, CoreZero.ContextTwo,
                CoreZeroThreadB, CoreZeroThreadA);
            CoreZero.ThreadStatusTwo = statuses[0];
            CoreZero.ThreadStatus = statuses[1];
            
            
            // Check for reservations, and resume waiting threads if the right conditions hold.
            if (CoreZero.ThreadStatus == ThreadStatus.Waiting)
            {
                var thStatuses = checkForWaitingThreads(CoreZero.Context, CoreZero.ThreadStatusTwo, CoreZeroThreadA, CoreZeroThreadB);
                if (thStatuses != null)
                {
                    CoreZero.ThreadStatus = thStatuses[0];
                    CoreZero.ThreadStatusTwo = thStatuses[1];
                }
            }

            if (CoreZero.ThreadStatusTwo == ThreadStatus.Waiting)
            {
                var thStatuses = checkForWaitingThreads(CoreZero.ContextTwo, CoreZero.ThreadStatus, CoreZeroThreadB, CoreZeroThreadA);
                if (thStatuses != null)
                {
                    CoreZero.ThreadStatus = thStatuses[0];
                    CoreZero.ThreadStatusTwo = thStatuses[1];
                }
            }
            
            // TODO Check for Cache failed threads
            
            // TODO Lastly check for ending threads
            


        }

        private ThreadStatus[] checkForWaitingThreads(Context context, ThreadStatus oStatus, Thread threadA, Thread threadB)
        {
            ThreadStatus baseStatus;
            ThreadStatus oThreadStatus;
            var reservations = CoreZero.Reservations;
            Reservation waitingCause = null;
            foreach (var reservation in reservations)
            {
                // Here we find out the resource the thread was waiting for
                if (reservation.ThreadId != context.ThreadId) continue;
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
                threadB.Suspend();
                baseStatus = ThreadStatus.Running;
                threadA.Resume();
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
            var inDataCache = waitingReservation.IsInDateCache;
            var blockNum = waitingReservation.BlockLabel;
            var found = false;
            var reservations = CoreZero.Reservations;
            foreach (var reservation in reservations)
            {
                if (usingBus && reservation.IsUsingBus)
                {
                    // We then check if they are using the same cache
                    if (inDataCache != reservation.IsInDateCache) continue;
                    found = true;
                    break;
                }

                // Check if block labels match
                if (blockNum != reservation.BlockLabel || inDataCache != reservation.IsInDateCache) continue;
                found = true;
                break;
            }
            return found;
        }

        private void LoadContextMainThread(Core core)
        {
            // If context queue is empty then keep running the same thread
            if (ContextQueue.Count <= 0) return;
            var newContext = ContextQueue.Dequeue();
            var oldContext = core.Context;
            oldContext.HasPriority = false;
            ContextQueue.Enqueue(oldContext);
            core.Context = newContext;
        }

        private void LoadContextSecThread(DobleCore core)
        {
            // If context queue is empty then keep running the same thread
            if (ContextQueue.Count <= 0) return;
            var newContext = ContextQueue.Dequeue();
            var oldContext = core.ContextTwo;
            oldContext.HasPriority = false;
            ContextQueue.Enqueue(oldContext);
            core.ContextTwo = newContext;
        }

        private ThreadStatus[] CheckIfSolvedCacheFail(ThreadStatus baseStatus, ThreadStatus otherStatus, Context baseContext,
                    Thread baseThread, Thread otherThread)
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
                        otherThread.Suspend();
                        // Sets primary thread in the running state
                        baseStatus = ThreadStatus.Running;
                    }
                    else
                    {
                        // Primary thread has no priority, then it must be stopped
                        baseStatus = ThreadStatus.Stopped;
                        baseThread.Suspend();
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
        
        public void RunSimulation()
        {
            
        }

    }
    
}