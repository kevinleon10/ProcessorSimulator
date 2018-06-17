using System;
using System.Threading;
using ProcessorSimulator.block;
using ProcessorSimulator.cache;
using ProcessorSimulator.core;
using ProcessorSimulator.memory;
using ProcessorSimulator.processor;

namespace ProcessorSimulator
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            Processor.Instance.Quantum = 10;
            Console.WriteLine("Hello world!");
            Console.WriteLine(Processor.Instance.Quantum);
        }
        
    }
}