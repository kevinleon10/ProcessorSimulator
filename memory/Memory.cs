using ProcessorSimulator.block;

namespace ProcessorSimulator.memory
{
    public class Memory
    {
        private Block<Instruction>[] _instructionBlocks;
        private Block<int>[] _dataBlocks;

        public Memory(Block<Instruction>[] instructionBlocks, Block<int>[] dataBlocks)
        {
            _instructionBlocks = instructionBlocks;
            _dataBlocks = dataBlocks;
        }

        public Block<Word<int>> GetDataBlock(int address)
        {
            //TODO hacer calculo
            return null;
        }
        
        public Block<Word<Instruction>> GetInstructionBlock(int address)
        {
            //TODO hacer calculo
            return null;
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