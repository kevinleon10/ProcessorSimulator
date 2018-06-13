using System;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.common;
using ProcessorSimulator.memory;

namespace ProcessorSimulator.cache
{
    public class Cache<T> where T : new()
    {
        public Cache(int cacheSize, Memory memory)
        {
            CacheSize = cacheSize;
            Memory = memory;
            
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

        public Memory Memory { get; set; }
        
        public CacheBlock<T>[] Blocks { get; set; }

        /// <summary>
        /// Load a word, which can be an instruction or a data
        /// </summary>
        /// <returns>
        /// The resulting word
        /// </returns>
        public T LoadWord(int blockNumberInMemory, int wordNumberInBlock, bool isDoubleCore)
        {
            var word = new T();
            var blockNumberInCache = blockNumberInMemory % CacheSize;
            Console.WriteLine("Block number in cache: " + blockNumberInCache);
            if (isDoubleCore)
            {
                //TODO Resolve reservation
            }
            else
            {
                var hasGottenBlock = false;
                //while it has not gotten the block it continues asking for
                while (!hasGottenBlock)
                {
                    if (Monitor.TryEnter(Blocks[blockNumberInCache]))
                    {
                        try
                        {
                            hasGottenBlock = true;
                            //if the label matches with the block number
                            if (Blocks[blockNumberInCache].Label == blockNumberInMemory)
                            {
                                //if the block status is invalid
                                if (Blocks[blockNumberInCache].BlockState == BlockState.Invalid)
                                {
                                    Console.WriteLine("The current block status is invalid");
                                    
                                }
                                else
                                {
                                    word = Blocks[blockNumberInCache].Words[wordNumberInBlock];
                                    Console.WriteLine("I could take the block");
                                }
                            }
                            else
                            {
                            }

                            // The critical section.
                        }
                        finally
                        {
                            // Ensure that the lock is released.
                            Monitor.Exit(Blocks[blockNumberInCache]);
                        }
                    }
                    else
                    {
                        // The lock was not acquired.
                        
                    }
                }
            }

            return word;
        }

        public void WriteBlock(Block<T> block, int position)
        {
            //TODO revisar si solo se escribe una palabra
        }

    }
}