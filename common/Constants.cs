﻿namespace ProcessorSimulator.common
{
    public class Constants
    {
        public const int InvalidLabel = -1;
        public const int WordsInBlock = 4;
        public const int BytesInBlock = 16;
        public const int BlocksInMemory = 64;
        public const int DataBlocksInMemory = 24;
        public const int InstructionBlocksInMemory = 40;
        public const int DefaultDataValue = 1;
        public const int CoreZeroCacheSize = 8;
        public const int CoreOneCacheSize  = 4;
    }
}