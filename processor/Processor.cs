using System.Collections.Generic;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.common;
using ProcessorSimulator.core;
using ProcessorSimulator.memory;

namespace ProcessorSimulator.processor
{
    public class Processor
    {
        public Processor(int quantum)
        {
            Clock = 0;
            ClockBarrier = new Barrier(3);
            ProcessorBarrier = new Barrier(3);
            ContextQueue = new Queue<Context>();
            Quantum = quantum;
            InitializeStructures();
        }

        private Thread coreZeroThreadA;
        private Thread coreZeroThreadB;
        private Thread coreOneThread;
        
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

            for (var i = 0; i < Constants.numberOfThreadsToLoad; i++)
            {
                ContextQueue.Enqueue(new Context(pc, i));
                var filePath = @"hilillos\" + i + ".txt";
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
            var memory = new Memory(instructionBlocks, dataBlocks);
            
            // Creates the four caches. Two per core
            var dataCacheZero = new Cache<int>(Constants.CoreZeroCacheSize, memory);
            var instructionCacheZero = new Cache<Instruction>(Constants.CoreZeroCacheSize, memory);
            var dataCacheOne = new Cache<int>(Constants.CoreOneCacheSize, memory);
            var instructionCacheOne = new Cache<Instruction>(Constants.CoreOneCacheSize, memory);
            
            // Set each cache with the other cache connected to it.
            dataCacheZero.OtherCache = dataCacheOne;
            dataCacheOne.OtherCache = dataCacheZero;
            instructionCacheZero.OtherCache = instructionCacheOne;
            instructionCacheOne.OtherCache = instructionCacheZero;
            
            // Creates the two cores of the processor
            CoreOne = new Core(instructionCacheOne, dataCacheOne, Quantum);
            CoreZero = new DobleCore(instructionCacheZero, dataCacheZero, Quantum);    
        }

        public void Check()
        {
            // Check if there are threads that have ran out of the cycles
            if (CoreZero.RemainingThreadCycles == 0)
                loadContextMainThread(CoreZero);
            if (CoreOne.RemainingThreadCycles == 0)
                loadContextMainThread(CoreOne);
            if (CoreZero.RemainingThreadCyclesTwo == 0)
                loadContextSecThread(CoreZero);

            // Check if either thread in Core Zero has just resolved a Cache Fail
            if (CoreZero.ThreadStatus == ThreadStatus.SolvedCacheFail)
            {
                // Check if there is another thread running
                if (CoreZero.ThreadStatusTwo == ThreadStatus.Running)
                {
                    // Check if the thread that solved Cache Fail has priority
                    if (CoreZero.Context.HasPriority)
                    {
                        // Secondary thread must be stopped
                        CoreZero.ThreadStatusTwo = ThreadStatus.Stopped;
                        // Suspends secundary thread
                        coreZeroThreadB.Suspend();
                        // Sets primary thread in the running state
                        CoreZero.ThreadStatus = ThreadStatus.Running;
                        // Resumes primary thread
                        coreZeroThreadA.Resume();
                    }
                    else
                    {
                        // Primary thread has no priority, then it must be stopped
                        CoreZero.ThreadStatus = ThreadStatus.Stopped;
                        coreZeroThreadA.Suspend();
                    }
                }
                else
                {
                    // If there is no secundary thread running, then the primary resumes
                    // Sets primary thread in the running state
                    CoreZero.ThreadStatus = ThreadStatus.Running;
                    // Resumes primary thread
                    coreZeroThreadA.Resume();
                }
            }
            
            // TODO check if secundary thread solved Cache fail
            
            
            
        }

        private void loadContextMainThread(Core core)
        {
            var newContext = ContextQueue.Dequeue();
            var oldContext = core.Context;
            oldContext.HasPriority = false;
            ContextQueue.Enqueue(oldContext);
            core.Context = newContext;
        }

        private void loadContextSecThread(DobleCore core)
        {
            var newContext = ContextQueue.Dequeue();
            var oldContext = core.ContextTwo;
            oldContext.HasPriority = false;
            ContextQueue.Enqueue(oldContext);
            core.ContextTwo = newContext;
        }

        public void runSimulation()
        {
            
        }

    }
    
}