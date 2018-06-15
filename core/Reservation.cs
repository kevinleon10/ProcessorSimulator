namespace ProcessorSimulator.core
{
    public class Reservation
    {
        public Reservation()
        {
            IsWaiting = false;
            IsUsingBus = false;
            IsInDateCache = false;
            BlockLabel = 0;
        }

        public Reservation(bool isWaiting, bool isUsingBus, bool isInDateCache, int blockLabel, int tID)
        {
            IsWaiting = isWaiting;
            IsUsingBus = isUsingBus;
            IsInDateCache = isInDateCache;
            BlockLabel = blockLabel;
            ThreadId = tID;
        }

        public int ThreadId { get; private set; }

        public bool IsWaiting { get; set; }

        public bool IsUsingBus { get; set; }

        public bool IsInDateCache { get; set; }

        public int BlockLabel { get; set; }
    }
}