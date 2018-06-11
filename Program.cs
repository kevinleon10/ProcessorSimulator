using System;
using System.Threading;
using ProcessorSimulator.core;

namespace ProcessorSimulator
{
    internal class Program
    {
        private static void Test(Context context)
        {
            var core = new Core(null, null, 8);
            core.StartExecution(context);
        }

        public static void Main(string[] args)
        {
            var context = new Context(200, 1);
            var thread = new Thread(() => Test(context));
            thread.Start();
            Console.WriteLine("Hello world!");
        }
        
    }
}