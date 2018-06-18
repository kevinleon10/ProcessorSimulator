namespace ProcessorSimulator.core
{
    public enum ThreadStatus
    {
        Running,
        Stopped,
        Waiting,
        CacheFail,
        SolvedCacheFail,
        Ended,
        Dead
    }
}