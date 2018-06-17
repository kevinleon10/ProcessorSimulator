using System.Security.Policy;

namespace ProcessorSimulator.common
{
    public static class Constants
    {
        public const int InvalidLabel = -1;
        public const int WordsInBlock = 4;
        public const int BytesInWord = 4;
        public const int BytesInBlock = 16;
        public const int BlocksInMemory = 64;
        public const int DataBlocksInMemory = 24;
        public const int InstructionBlocksInMemory = 40;
        public const int DefaultDataValue = 1;
        public const int CoreZeroCacheSize = 8;
        public const int CoreOneCacheSize  = 4;
        public const int NumberOfThreadsToLoad = 6;
        public const int NotRunningAnyThread = -1;
        public const int CyclesMemory = 40;
        public const string FilePath = "hilillos/";
        public const string FileExtension = ".txt";
    }
}