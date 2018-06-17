using ProcessorSimulator.block;
using ProcessorSimulator.common;

namespace ProcessorSimulator.memory
{
    public sealed class Memory
    {
        private static readonly Memory instance = new Memory();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static Memory()
        {
        }

        private Memory()
        {
        }

        //Singleton instance
        public static Memory Instance
        {
            get
            {
                return instance;
            }
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
            var position = (address-(Constants.DataBlocksInMemory*Constants.BytesInBlock)) / Constants.BytesInBlock;
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