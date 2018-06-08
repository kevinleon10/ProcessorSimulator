namespace ProcessorSimulator.block
{
    public class Word<T>
    {
        public Word(T wordData)
        {
            this.WordData = wordData;
        }

        public T WordData { get; set; }
    }
}