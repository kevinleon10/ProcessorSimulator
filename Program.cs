using System;
using ProcessorSimulator.processor;

namespace ProcessorSimulator
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello world!");
            Processor.Instance.RunSimulation(false);
        }
    }
}