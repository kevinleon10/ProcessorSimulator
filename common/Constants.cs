namespace ProcessorSimulator.common
{
    public static class Constants
    {
        public const int InvalidLabel = -1;
        public const int WordsInBlock = 4;
        public const int BytesInWord = 4;
        public const int BytesInBlock = 16;
        //It is BytesInBlock * DataBlocksInMemory
        public const int BytesInMemoryDataBlocks = 384;
        public const int DataBlocksInMemory = 24;
        public const int InstructionBlocksInMemory = 40;
        public const int DefaultDataValue = 1;
        public const int CoreZeroCacheSize = 8;
        public const int CoreOneCacheSize = 4;
        public const int NumberOfThreadsToLoad = 6;
        public const int NumberOfRegisters = 32;
        public const int NotRunningAnyThread = -1;
        public const int CyclesMemory = 40;
        public const int Quantum = 100;
        public const int SlowMotionCycles = 20;
        public const int DelayTime = 2000; // Miliseconds
        public const string FilePath = "hilillos/";
        public const string FileExtension = ".txt";
        // ERRORS
        public const string AddressError = "ERROR: Bad memory address reference in the following instruction: ";
    }
}