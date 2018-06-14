using System;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.core;
using ProcessorSimulator.memory;
using ProcessorSimulator.processor;

namespace ProcessorSimulator
{
    internal class Program
    {
        private static void Test(Context context)
        {
            Memory memory = null;
            Cache<Instruction> instructionCache = new Cache<Instruction>(8, memory);
            Cache<int> dataCache = new Cache<int>(8, memory);
            var core = new Core(instructionCache, dataCache, 8);
            core.StartExecution(context);
        }

        public static void Main(string[] args)
        {
            /*
            var context = new Context(200, 1);
            var thread = new Thread(() => Test(context));
            thread.Start();
            */
            var processor = new Processor(10);
            Console.WriteLine("Hello world!");
        }
        
    }
}