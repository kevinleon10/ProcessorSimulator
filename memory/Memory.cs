using System.Linq.Expressions;
using System.Text;
using ProcessorSimulator.block;
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
        
        public Instruction[] LoadInstructionBlock(int address)
        {
            var instructions = new Instruction[Constants.WordsInBlock];
            var position = address / Constants.BytesInBlock;
            for (var i=0; i<InstructionBlocks[position].Words.Length; ++i)
            {
                instructions[i] = InstructionBlocks[position].Words[i];
            }
            return instructions;
        }
        
        public int[] LoadDataBlock(int address)
        {
            var words = new int[Constants.WordsInBlock];
            var position = address / Constants.BytesInBlock;
            for (var i=0; i<DataBlocks[position].Words.Length; ++i)
            {
                words[i] = DataBlocks[position].Words[i];
            }
            return words;
        }
        
        public void StoreDataBlock(int address, int[] words)
        {
            var newWords = new int[Constants.WordsInBlock];
            for (var i=0; i<words.Length; ++i)
            {
                newWords[i] = words[i];
            }
            var position = address / Constants.BytesInBlock;
            DataBlocks[position].Words = newWords;
        }

        public override string ToString()
        {
            // Gathers the data blocks of the memory instance.
            var builder = new StringBuilder();
            for (var i = 0; i < DataBlocks.Length; i++)
            {
                builder.AppendLine("Block number: " + i + " : " + DataBlocks[i]);
            }
            return builder.ToString();
        }
    }
}