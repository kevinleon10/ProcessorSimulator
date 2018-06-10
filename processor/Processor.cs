using System;
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
            var dataBlock = new Block<int>[Constants.DataBlocksInMemory];
            for (var i = 0; i < Constants.DataBlocksInMemory; i++)
            {
                var words = new Word<int>[Constants.WordsInBlock];
                for (var j = 0; j < Constants.WordsInBlock; j++)
                {
                    words[j] = new Word<int>(Constants.DefaultDataValue);
                }
                dataBlock[i] = new Block<int>(words);
            }
            
            /* Now we initialize the instruction block of main memory and we fill up the context queue*/
            var pc = 0;
            var blockNum = 0;
            var wordNum = 0;
            var instructionBlocks = new Block<Instruction>[Constants.InstructionBlocksInMemory];

            for (var i = 0; i < 6; i++)
            {
                ContextQueue.Enqueue(new Context(pc, i));
                var values = fillInstructionBlocks(@"hilillos\" + i + ".txt", instructionBlocks, pc, blockNum, wordNum);
                pc = values[0];
                blockNum = values[1];
                wordNum = values[2];
            }
            
            /*
             * At this point the instruction block is ready
             * Now, initialize the Memory structure
             */
            var memory = new Memory(instructionBlocks, dataBlock);
            
            // Creates the two buses for memory access
            var dataCacheBus = new Mutex();
            var instructionCacheBus = new Mutex();
            
            // Creates the four caches. Two per core
            var dataCacheZero = new Cache<int>(Constants.CoreZeroCacheSize, dataCacheBus, memory);
            var instructionCacheZero = new Cache<Instruction>(Constants.CoreZeroCacheSize, instructionCacheBus, memory);
            var dataCacheOne = new Cache<int>(Constants.CoreOneCacheSize, dataCacheBus, memory);
            var instructionCacheOne = new Cache<Instruction>(Constants.CoreOneCacheSize, instructionCacheBus, memory);
            
            // Set each cache with the other cache connected to it.
            dataCacheZero.OtherCache = dataCacheOne;
            dataCacheOne.OtherCache = dataCacheZero;
            instructionCacheZero.OtherCache = instructionCacheOne;
            instructionCacheOne.OtherCache = instructionCacheZero;
            
            // Creates the two cores of the processor
            CoreOne = new Core(instructionCacheOne, dataCacheOne, Constants.CoreOneCacheSize);
            CoreZero = new DobleCore(instructionCacheZero, dataCacheZero, Constants.CoreZeroCacheSize);    
        }

        /**
         * Fills up the instruction block using the instructions found on the file with the given file name.
         * returns the final PC 
         */
        private static int[] fillInstructionBlocks(string filePath, Block<Instruction>[] instructionBlocks, int startPc, 
                                          int currentBlock, int currentWord)
        {        
            string line;
            Word<Instruction>[] instructionArray = null;

            // Read the file and display it line by line.  
            var file = new System.IO.StreamReader(filePath);  
            while((line = file.ReadLine()) != null)  
            {  
                // Get instruction from line
                const char delimiter = ' '; // Whitespace.
                var numberStrings = line.Split(delimiter);
                var opCode = int.Parse(numberStrings[0]);
                var source = int.Parse(numberStrings[1]);
                var destiny = int.Parse(numberStrings[2]);
                var inmediate = int.Parse(numberStrings[3]);
                var instruction = new Instruction(opCode, source, destiny, inmediate);

                if (instructionArray == null)
                {
                    instructionArray = new Word<Instruction>[Constants.WordsInBlock];
                }

                instructionArray[currentBlock++] = new Word<Instruction>(instruction);
                startPc += 4;

                if (currentWord != Constants.WordsInBlock) continue;
                instructionBlocks[currentBlock] = new Block<Instruction>(instructionArray);
                currentBlock++;
                currentWord = 0;
                instructionArray = null;
            }  

            file.Close();
            int[] values = {startPc, currentBlock, currentWord};
            return values;
        }
        
    }
    
}