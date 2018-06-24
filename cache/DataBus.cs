namespace ProcessorSimulator.cache
{
    public sealed class DataBus
    {
        private static volatile DataBus _instance;
        private static readonly object Padlock = new object();

        private DataBus()
        {
        }

        public static DataBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (Padlock)
                    {
                        if (_instance == null)
                            _instance = new DataBus();
                    }
                }

                return _instance;
            }
        }
    }
}