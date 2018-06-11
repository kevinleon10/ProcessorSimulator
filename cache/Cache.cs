using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.common;
using ProcessorSimulator.memory;

namespace ProcessorSimulator.cache
{
    public class Cache<T>
    {
        public Cache(int cacheSize, Mutex busMutex, Memory memory)
        {
            BusMutex = busMutex;
            TableSize = cacheSize;
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

        public Mutex BusMutex { get; set; }

        public int TableSize { get; set; }

        public Memory Memory { get; set; }
        
        public CacheBlock<T>[] Blocks { get; set; }


        public Block<T> GetBlock(int address)
        {
            //TODO agregar buscar etiqueta del bloque con la direccion
            return null;
        }

        public void WriteBlock(Block<T> block, int position)
        {
            //TODO revisar si solo se escribe una palabra
        }

    }
}