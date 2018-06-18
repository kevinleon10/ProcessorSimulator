﻿using ProcessorSimulator.block;
using ProcessorSimulator.common;

namespace ProcessorSimulator.memory
{
    public sealed class Memory
    {
        private static volatile Memory _instance = null;
        private static readonly object Padlock = new object();
 
        private Memory() {}
 
        public static Memory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock(Padlock)
                    {
                        if (_instance == null)
                            _instance = new Memory();
                    }
                }
 
                return _instance;
            }
        }

        public Block<Instruction>[] InstructionBlocks { get; set; }

        public Block<int>[] DataBlocks { get; set; }

        public Block<int> LoadDataBlock(int address)
        {
            var position = address / Constants.BytesInBlock;
            return DataBlocks[position];
        }
        
        public Block<Instruction> LoadInstructionBlock(int address)
        {
            var position = (address-Constants.BytesInMemoryDataBlocks) / Constants.BytesInBlock;
            return InstructionBlocks[position];
        }
        
        public void StoreDataBlock(int address, int[] words)
        {
            var position = address / Constants.BytesInBlock;
            DataBlocks[position].Words = words;
        }
    }
}