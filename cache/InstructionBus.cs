namespace ProcessorSimulator.cache
{
    public sealed class InstructionBus
    {
        private static readonly InstructionBus instance = new InstructionBus();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static InstructionBus()
        {
        }

        private InstructionBus()
        {
        }

        public static InstructionBus Instance
        {
            get
            {
                return instance;
            }
        }
    }
}