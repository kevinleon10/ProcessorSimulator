using ProcessorSimulator.common;

namespace ProcessorSimulator.core
{
    public class Reservation
    {
        public Reservation()
        {
            IsWaiting = false;
            IsUsingBus = false;
            IsInDataCache = false;
            BlockNumberInCache = Constants.InvalidLabel;
            ThreadId = Constants.InvalidLabel;
        }

        public Reservation(bool isWaiting, bool isUsingBus, bool isInDataCache, int blockNumberInCache, int threadId)
        {
            IsWaiting = isWaiting;
            IsUsingBus = isUsingBus;
            IsInDataCache = isInDataCache;
            BlockNumberInCache = blockNumberInCache;
            ThreadId = threadId;
        }

        public int ThreadId { get; private set; }

        public bool IsWaiting { get; set; }

        public bool IsUsingBus { get; set; }

        public bool IsInDataCache { get; set; }

        public int BlockNumberInCache { get; set; }
    }
}