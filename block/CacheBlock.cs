using System.Threading;

namespace ProcessorSimulator.block
{
    public class CacheBlock<T> : Block<T>
    {
        public CacheBlock()
        {
            Label = -1;
            BlockState = BlockState.Invalid;
            BlockMutex = new Mutex();
        }

        public int Label { get; set; }

        public BlockState BlockState { get; set; }

        public Mutex BlockMutex { get; set; }
    }
}