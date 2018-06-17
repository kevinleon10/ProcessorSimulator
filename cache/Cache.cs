using System;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.common;
using ProcessorSimulator.memory;

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
    }
}