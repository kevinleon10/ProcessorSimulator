namespace ProcessorSimulator.cache
{
    public sealed class InstructionBus
    {
        private static volatile InstructionBus _instance;
        private static readonly object Padlock = new object();

        private InstructionBus()
        {
        }

        public static InstructionBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (Padlock)
                    {
                        if (_instance == null)
                            _instance = new InstructionBus();
                    }
                }

                return _instance;
            }
        }
    }
}