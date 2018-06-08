namespace ProcessorSimulator.block
{
    public class Block<T>
    {
        protected Block()
        {
            Words = new Word<T>[4];
        }

        protected Block(Word<T>[] words)
        {
            this.Words = words;
        }

        public Word<T>[] Words { get; set; }

        public T GetValue(int index)
        {
            T wordData = Words[index].WordData;
            return wordData;
        }

    }
}