using ProcessorSimulator.common;

namespace ProcessorSimulator.block
{
    public class Block<T>
    {
        public Block()
        {
            Words = new T[Constants.WordsInBlock];
        }

        public Block(T[] words)
        {
            this.Words = words;
        }

        public T[] Words { get; set; }

        public T GetValue(int index)
        {
            T wordData = Words[index];
            return wordData;
        }
    }
}