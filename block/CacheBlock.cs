using System.Threading;
using ProcessorSimulator.common;

namespace ProcessorSimulator.block
{
    public class CacheBlock<T> : Block<T>
    {
        public CacheBlock(T[] words) : base(words)
        {
            Label = Constants.InvalidLabel;
            BlockState = BlockState.Invalid;
        }

        public int Label { get; set; }

        public BlockState BlockState { get; set; }
    }
}