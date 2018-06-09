using ProcessorSimulator.block;

namespace ProcessorSimulator.memory
{
    public class Memory
    {
        private Block<Instruction>[] _instructionBlocks;
        private Block<int>[] _dataBlocks;
        private const int  DataBlocksSize = 380;

        public Memory(Block<Instruction>[] instructionBlocks, Block<int>[] dataBlocks)
        {
            _instructionBlocks = instructionBlocks;
            _dataBlocks = dataBlocks;
        }

        public Block<int> GetDataBlock(int address)
        {
            var position = address / 16;
            return _dataBlocks[position];
        }
        
        public Block<Instruction> GetInstructionBlock(int address)
        {
            var position = (address+380) / 16;
            return _instructionBlocks[position];
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