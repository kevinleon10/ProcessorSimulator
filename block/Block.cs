using ProcessorSimulator.common;

namespace ProcessorSimulator.block
{
    public class Block<T>
    {
        public Block()
        {
            Words = new Word<T>[Constants.WordsInBlock];
        }

        public Block(Word<T>[] words)
        {
            this.Words = words;
        }

        public Word<T>[] Words { get; set; }

        public T GetValue(int index)
        {
            var wordData = Words[index].WordData;
            return wordData;
        }
    }
}