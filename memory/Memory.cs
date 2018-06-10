using ProcessorSimulator.block;
using ProcessorSimulator.common;

namespace ProcessorSimulator.memory
{
    public class Memory
    {
        public Memory(Block<Instruction>[] instructionBlocks, Block<int>[] dataBlocks)
        {
            InstructionBlocks = instructionBlocks;
            DataBlocks = dataBlocks;
        }

        public Block<Instruction>[] InstructionBlocks { get; set; }

        public Block<int>[] DataBlocks { get; set; }

        public Block<int> GetDataBlock(int address)
        {
            var position = address / Constants.BytesInBlock;
            return DataBlocks[position];
        }
        
        public Block<Instruction> GetInstructionBlock(int address)
        {
            var position = (address+Constants.BlocksInMemory) / Constants.BytesInBlock;
            return InstructionBlocks[position];
        }
        
        public void WriteDataBlock(int address)
        {
            //TODO hacer operacion
        }
        
        public void WriteInstructionBlock(int address)
        {
            //TODO hacer operacion
        }
    }
}