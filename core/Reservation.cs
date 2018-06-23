using ProcessorSimulator.common;

namespace ProcessorSimulator.core
{
    public class Reservation
    {
        public Reservation()
        {
            IsWaiting = false;
            IsUsingBus = false;
            IsInDateCache = false;
            BlockLabel = Constants.InvalidLabel;
            ThreadId = Constants.InvalidLabel;
        }

        public Reservation(bool isWaiting, bool isUsingBus, bool isInDateCache, int blockLabel, int threadId)
        {
            IsWaiting = isWaiting;
            IsUsingBus = isUsingBus;
            IsInDateCache = isInDateCache;
            BlockLabel = blockLabel;
            ThreadId = threadId;
        }

        public int ThreadId { get; private set; }

        public bool IsWaiting { get; set; }

        public bool IsUsingBus { get; set; }

        public bool IsInDateCache { get; set; }

        public int BlockLabel { get; set; }
    }
}