using System.Text;
using ProcessorSimulator.block;
using ProcessorSimulator.common;

namespace ProcessorSimulator.memory
{
    /// <summary>
    /// A class that represents the structure of main memory.
    /// </summary>
    public sealed class Memory
    {
        private static volatile Memory _instance; // Singleton
        private static readonly object Padlock = new object(); // Lock
 
        /// <summary>
        /// Empty constructor.
        /// </summary>
        private Memory() {}
 
        /// <summary>
        /// Constructs the singleton instance if none exists, otherwise just return the existing one.
        /// </summary>
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

        /// <summary>
        /// Accessor for the instruction block array.
        /// </summary>
        public Block<Instruction>[] InstructionBlocks { get; set; }

        /// <summary>
        /// Accessor for the data block array.
        /// </summary>
        public Block<int>[] DataBlocks { get; set; }
        
        /// <summary>
        /// Loads the instruction array found at a given position.
        /// </summary>
        /// <param name="position">The position where the instruction array should be fetched from.</param>
        /// <returns></returns>
        public Instruction[] LoadInstructionBlock(int position)
        {
            var instructions = new Instruction[Constants.WordsInBlock];
            for (var i=0; i<InstructionBlocks[position].Words.Length; ++i)
            {
                instructions[i] = InstructionBlocks[position].Words[i];
            }
            return instructions;
        }
        
        /// <summary>
        /// Loads the data array found at a given position.
        /// </summary>
        /// <param name="position">The position where the data array should be fetched from.</param>
        /// <returns></returns>
        public int[] LoadDataBlock(int position)
        {
            var words = new int[Constants.WordsInBlock];
            for (var i=0; i<DataBlocks[position].Words.Length; ++i)
            {
                words[i] = DataBlocks[position].Words[i];
            }
            return words;
        }
        
        /// <summary>
        /// Saves a block of data at a given position.
        /// </summary>
        /// <param name="position">Index where the data is to be saved.</param>
        /// <param name="words">The data array that is to be stored.</param>
        public void StoreDataBlock(int position, int[] words)
        {
            var newWords = new int[Constants.WordsInBlock];
            for (var i=0; i<words.Length; ++i)
            {
                newWords[i] = words[i];
            }
            DataBlocks[position].Words = newWords;
        }

        /// <summary>
        /// Builds a textual representation of the object.
        /// </summary>
        /// <returns>a string of characters that represent the object.</returns>
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