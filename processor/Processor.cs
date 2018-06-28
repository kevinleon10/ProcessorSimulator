﻿﻿using System;
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
        private static volatile Processor _instance;
        private static readonly object Padlock = new object();

        private Processor()
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

        private void StartCoreZero()
        {
            var context = GetNewContext();
            if (context == null)
            {
                FinalizeHighLevelThread(CoreZeroThread);
            }
            CoreZero.StartExecution(context);  
        }

        private void StartCoreOne()
        {
            var context = GetNewContext();
            if (context == null)
            {
                FinalizeHighLevelThread(CoreOneThread);
            }
            CoreOne.StartExecution(context); 
        }

        private Thread CoreZeroThread { get; set; }

        private Thread CoreOneThread { get; set; }

        private Core CoreZero { get; set; }

        private Core CoreOne { get; set; }

        public int Clock { get; set; }

        public Barrier ClockBarrier { get; set; }

        private Queue<Context> ContextQueue { get; set; }

        private List<Context> ContextList { get; set; }

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

        private void Check()
        {
            // Check if there are threads that have ended execution
            if (CoreZero.ThreadHasEnded)
            {
                LoadNewContext(CoreZero, CoreZeroThread);
            }

            if (CoreOne.ThreadHasEnded)
            {
                LoadNewContext(CoreOne, CoreOneThread);
            }

            // Check if there are threads that have ran out of the cycles
            if (CoreZero.RemainingThreadCycles == 0)
            {
                SwapContext(CoreZero);
            }

            if (CoreOne.RemainingThreadCycles == 0)
            {
                SwapContext(CoreOne);
                
            }
        }

        private void LoadNewContext(Core core, Thread thread)
        {
            var oldContext = core.Context;
            ContextList.Add(oldContext); // Adds the ending context for statistic purposes
            var newContext = GetNewContext();
            if (newContext != null)
            {
                core.Context = newContext;
                core.RemainingThreadCycles = Constants.Quantum; // Sets the quantum as the remaining cycles for the new thread
                core.thereAreContexts = true;
            }
            else
            {
                // No more threads to run, so high level threads finishes execution 
                FinalizeHighLevelThread(thread);
            }
        }

        private Context GetNewContext()
        {
            return ContextQueue.Count == 0 ? null : ContextQueue.Dequeue();
        }
           
        private void SwapContext(Core core)
        {
            // If context queue is empty then keep running the same thread
            if (ContextQueue.Count > 0)
            {
                var newContext = ContextQueue.Dequeue();
                var oldContext = core.Context;
                ContextQueue.Enqueue(oldContext);
                core.Context = newContext;
                core.thereAreContexts = true;
            }
            core.RemainingThreadCycles = Constants.Quantum; // Restores remaining cycles to the quantum value.
        }

        private void FinalizeHighLevelThread(Thread thread)
        {
            // thread.Abort();
            ClockBarrier.RemoveParticipant();
            ProcessorBarrier.SignalAndWait();
            ProcessorBarrier.RemoveParticipant();
            ClockBarrier.SignalAndWait();
        }

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
                }

                Clock++;
                System.Console.Write(Clock+"\n");
                Check();                
                //Thread.Sleep(Constants.DelayTime);
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