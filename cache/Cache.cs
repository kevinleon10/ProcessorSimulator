using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.memory;

namespace ProcessorSimulator.cache
{
    public class Cache<T>
    {
        public const int BlockSize = 4;
        
        public Cache( int cacheSize, Mutex busMutex)
        {
            Blocks = new Block<T>[cacheSize];
            BusMutex = busMutex;
        }

        public Block<T>[] Blocks { get; set; }

        public Cache<T> OtherCache { get; set; }

        public Mutex BusMutex { get; set; }

        public int TableSize { get; set; }

        public Memory Memory { get; set; }

        public Block<T> GetBlock(int address)
        {
            //TODO agregar buscar etiqueta del bloque con la direccion
            return null;
        }

        public void WriteBlock(Block<T> block, int position)
        {
            //TODO revisar si solo se escribe una palabra
            Blocks[position] = block;
        }

    }
}