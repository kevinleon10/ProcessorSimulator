using System.Text;
using ProcessorSimulator.block;
using ProcessorSimulator.common;

namespace ProcessorSimulator.cache
{
    public class Cache<T>
    {
        public Cache(int cacheSize)
        {
            CacheSize = cacheSize;

            // Fill up the block array with null values
            Blocks = new CacheBlock<T>[cacheSize];
            for (var i = 0; i < cacheSize; i++)
            {
                var words = new T[Constants.WordsInBlock];
                for (var j = 0; j < Constants.WordsInBlock; j++)
                {
                    words[j] = default(T);
                }

                Blocks[i] = new CacheBlock<T>(words);
            }
        }

        public Cache<T> OtherCache { get; set; }

        public int CacheSize { get; set; }

        public CacheBlock<T>[] Blocks { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < Blocks.Length; i++)
            {
                builder.AppendLine("Block number: " + i + " : " + Blocks[i]);
            }
            return builder.ToString();
        }
    }
}