using System.Threading;
using ProcessorSimulator.common;

namespace ProcessorSimulator.block
{
    public class CacheBlock<T> : Block<T>
    {
        public CacheBlock()
        {
            Label = Constants.InvalidLabel;
            BlockState = BlockState.Invalid;
            BlockMutex = new Mutex();
        }

        public int Label { get; set; }

        public BlockState BlockState { get; set; }

        public Mutex BlockMutex { get; set; }
    }
}