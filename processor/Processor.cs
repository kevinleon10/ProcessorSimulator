﻿using System.Collections.Generic;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.common;
using ProcessorSimulator.core;
using ProcessorSimulator.memory;

namespace ProcessorSimulator.processor
{
    /// <summary>
    /// This class directs the main thread of the application and also initializes every structure required. Additionally
    /// it creates and synchronizes other participating threads.
    /// </summary>
    public sealed class Processor
    {
        private static volatile Processor _instance; // Singleton
        private static readonly object Padlock = new object(); // Class´s lock

        /// <summary>
        /// Class constructor.
        /// </summary>        
        public Processor()
        {
            Clock = 0;
            ClockBarrier = new Barrier(3);
            ProcessorBarrier = new Barrier(3);
            ContextQueue = new Queue<Context>();
            ContextList = new List<Context>();
            CoreZeroThread = new Thread(StartCoreZero);
            CoreOneThread = new Thread(StartCoreOne);
            InitializeStructures();
        }

        public void RestartProcessor()
        {
            _instance = null;
            _instance = new Processor();
        }
        
        /// <summary>
        /// Intializes if necessary the singleton instance of the processor.
        /// </summary>
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
            set { throw new System.NotImplementedException(); }
        }

        /// <summary>
        /// Initializes the thread that is to be conducted by the Core Zero.
        /// </summary>
        private void StartCoreZero()
        {
            var context = GetNewContext();
            if (context == null)
            {
                FinalizeHighLevelThread();
            }
            CoreZero.StartExecution(context);  
        }

        /// <summary>
        /// Initializes the thread that is to be conducted by the Core One.
        /// </summary>
        private void StartCoreOne()
        {
            var context = GetNewContext();
            if (context == null)
            {
                FinalizeHighLevelThread();
            }
            CoreOne.StartExecution(context); 
        }

        private Thread CoreZeroThread { get; set; }

        private Thread CoreOneThread { get; set; }

        private Core CoreZero { get; set; }

        private Core CoreOne { get; set; }

        private int Clock { get; set; }

        public Barrier ClockBarrier { get; set; }

        private Queue<Context> ContextQueue { get; set; }

        private List<Context> ContextList { get; set; }

        public Barrier ProcessorBarrier { get; set; }

        public int Quantum { get; set; }

        /// <summary>
        /// Method in charge of creating and initializing all data structures and objects needed in the simulation
        /// </summary>
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
            var dataCacheZero = new Cache<int>(Constants.CoreOneCacheSize);
            var instructionCacheZero = new Cache<Instruction>(Constants.CoreOneCacheSize);
            var dataCacheOne = new Cache<int>(Constants.CoreOneCacheSize);
            var instructionCacheOne = new Cache<Instruction>(Constants.CoreOneCacheSize);

            // Set each cache with the other cache connected to it.
            dataCacheZero.OtherCache = dataCacheOne;
            dataCacheOne.OtherCache = dataCacheZero;
            instructionCacheZero.OtherCache = instructionCacheOne;
            instructionCacheOne.OtherCache = instructionCacheZero;

            // Creates the two cores of the processor
            CoreOne = new Core(instructionCacheOne, dataCacheOne);
            CoreZero = new Core(instructionCacheZero, dataCacheZero);
        }

        /// <summary>
        /// Method in charge of checking at the end of each cycle if a thread has either ended or it´s quantum has
        /// expired.
        /// </summary>
        private void Check()
        {
            // Check if there are threads that have ended execution
            if (CoreZero.ThreadHasEnded && CoreZero.RemainingThreadCycles != Constants.NotRunningAnyThread)
                LoadNewContext(CoreZero);
            else
                CoreZero.Context.NumberOfCycles++;
                        
            if (CoreOne.ThreadHasEnded && CoreOne.RemainingThreadCycles != Constants.NotRunningAnyThread)
                LoadNewContext(CoreOne);
            else
                CoreOne.Context.NumberOfCycles++;
            
            // Check if there are threads that have ran out of the cycles
            if (CoreZero.RemainingThreadCycles == 0)
                SwapContext(CoreZero);
            

            if (CoreOne.RemainingThreadCycles == 0)
                SwapContext(CoreOne);                           
        }

        /// <summary>
        /// Picks the core´s current context, places it in a list for statistical purposes and then loads a new context
        /// in the Core and restores the remaining thread cycles.
        /// </summary>
        /// <param name="core"> Thre core whose context finished execution</param>
        private void LoadNewContext(Core core)
        {
            var oldContext = core.Context;
            ContextList.Add(oldContext); // Adds the ending context for statistic purposes
            var newContext = GetNewContext();
            if (newContext != null)
            {
                core.Context = newContext;
                core.RemainingThreadCycles = Quantum; // Sets the quantum as the remaining cycles for the new thread
                core.ThereAreContexts = true;
            }
            else
            {
                // No more threads to run, so high level threads finishes execution
                core.RemainingThreadCycles = Constants.NotRunningAnyThread;
                FinalizeHighLevelThread();
            }
        }

        /// <summary>
        /// Picks off a context from the context queue.
        /// </summary>
        /// <returns> Thre next context of the queue or null if the queue is empty.</returns>
        private Context GetNewContext()
        {
            return ContextQueue.Count == 0 ? null : ContextQueue.Dequeue();
        }
          
        /// <summary>
        /// Replaces the context in the core and saves the old one in the queue.
        /// </summary>
        /// <param name="core"> The core whose context had a context switch</param>
        private void SwapContext(Core core)
        {
            // If context queue is empty then keep running the same thread
            if (ContextQueue.Count > 0)
            {
                var newContext = ContextQueue.Dequeue();
                var oldContext = core.Context;
                ContextQueue.Enqueue(oldContext);
                core.Context = newContext;
                core.ThereAreContexts = true;
            }
            core.RemainingThreadCycles = Quantum; // Restores remaining cycles to the quantum value.
        }

        /// <summary>
        /// Method call when either of the cores did not have any other remaining threads to run. It has to modify the
        /// synchronizing mechanisms accordingly.
        /// </summary>
        private void FinalizeHighLevelThread()
        {
            ClockBarrier.RemoveParticipant();
            ProcessorBarrier.SignalAndWait();
            Check();
            ProcessorBarrier.RemoveParticipant();
            ClockBarrier.SignalAndWait();
        }

        /// <summary>
        /// Main method through which the simulation starts. It also prints information of the various structures present
        /// as well as the results of the running threads.
        /// </summary>
        /// <param name="slowMotion"> The mode of the execution. "true" stands for slow mode, it means that each 100 cycles
        /// the simulation will stop for 2 seconds and print the current state of the clock and cores, at "false" it jumps
        /// directly to the results.</param>
        public void RunSimulation(bool slowMotion)
        {
            CoreZeroThread.Start();
            CoreOneThread.Start();
            while (ClockBarrier.ParticipantCount > 1)
            {

                ClockBarrier.SignalAndWait();
                if (slowMotion && Clock % Constants.SlowMotionCycles == 0)
                {
                    System.Console.WriteLine("Current Clock: " + Clock);               
                    System.Console.WriteLine("**********************************************************");
                    System.Console.WriteLine("Clock : " + Clock);
                    System.Console.WriteLine("Core Zero Main thread number: " +
                                             CoreZero.Context.ThreadId);                    
                    System.Console.WriteLine("Core One thread number: " +
                                             CoreOne.Context.ThreadId);
                    System.Console.WriteLine("***********************************************************");
                    Thread.Sleep(Constants.DelayTime);
                }
                Clock++;
                Check();                
                ProcessorBarrier.SignalAndWait();                
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