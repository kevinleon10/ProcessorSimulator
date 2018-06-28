using System;
using ProcessorSimulator.processor;

namespace ProcessorSimulator
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var quantum = 100;
            var validNumber = false;
            Console.WriteLine("Please insert the thread´s quantum to start the simulation.");
            while (!validNumber)
            {
                try
                {
                    var quantumStr = Console.ReadLine();
                    if (quantumStr == null) continue;
                    quantum = int.Parse(quantumStr);
                    validNumber = true;
                }
                catch (FormatException e)
                {
                    Console.WriteLine("Invalid input for quantum. Try again");
                }  
            }            
            Console.WriteLine("Insert one of the following:");
            Console.WriteLine("1 - Delay Mode");
            Console.WriteLine("2 - No Delay Mode");
            var answer = Console.ReadLine();
            var mode = answer != null && answer.Equals("1");
            Processor.Instance.Quantum = quantum;
            Processor.Instance.RunSimulation(mode);
        }
    }
}