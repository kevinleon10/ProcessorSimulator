using System;
using System.Threading;
using ProcessorSimulator.core;

namespace ProcessorSimulator
{
    internal class Program
    {
        public static void Test(Context context)
        {
            Mutex dataMutex = new Mutex();
            Mutex instructionMutex = new Mutex();
            Core core = new Core(8, dataMutex, instructionMutex);
            core.StartExecution(context);
        }

        public static void Main(string[] args)
        {
            Context context = new Context(200, 1);
            Thread thread = new Thread(() => Test(context));
            thread.Start();
            Console.WriteLine("Hello world!");
        }
    }
}