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
        public const int SlowMotionCycles = 20;
        public const int JROperationCode = 2;
        public const int JALOperationCode = 3;
        public const int BEQZOperationCode = 4;
        public const int BNEZOperationCode = 5;
        public const int DADDIOperationCode = 8;
        public const int DMULOperationCode = 12;
        public const int DDIVOperationCode = 14;
        public const int DADDOperationCode = 32;
        public const int DSUBOperationCode = 34;
        public const int LWOperationCode = 35;
        public const int SWOperationCode = 43;
        public const int EndOperationCode = 63;
        public const string FilePath = "hilillos/";
        public const string FileExtension = ".txt";
    }
}