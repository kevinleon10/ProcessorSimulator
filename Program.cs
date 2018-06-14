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
        public static void Main(string[] args)
        {
            var processor = new Processor(10);
            Console.WriteLine("Hello world!");
        }
        
    }
}