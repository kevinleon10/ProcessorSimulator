using System.Threading;

namespace ProcessorSimulator.block
{
    public class CacheBlock<T> : Block<T>
    {
        public CacheBlock()
        {
            Label = 0;
            Mutex = new Mutex();
            BlockState = BlockState.Invalid;
        }

        public CacheBlock(int label, Mutex mutex, BlockState blockState)
        {
            Label = label;
            Mutex = mutex;
            BlockState = blockState;
        }

        public CacheBlock(Word<T>[] words, int label, Mutex mutex, BlockState blockState) : base(words)
        {
            Label = label;
            Mutex = mutex;
            BlockState = blockState;
        }

        public int Label { get; set; }

        public Mutex Mutex { get; set; }

        public BlockState BlockState { get; set; }
    }
}