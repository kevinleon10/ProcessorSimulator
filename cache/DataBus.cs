namespace ProcessorSimulator.cache
{
    public sealed class DataBus
    {
        private static readonly DataBus instance = new DataBus();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static DataBus()
        {
        }

        private DataBus()
        {
        }

        public static DataBus Instance
        {
            get
            {
                return instance;
            }
        }
    }
}