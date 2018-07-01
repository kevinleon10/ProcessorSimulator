using System;
using ProcessorSimulator.processor;

namespace ProcessorSimulator
{
    public class Controller
    {
        public void RunSimulation()
        {
            Console.WriteLine("Welcome to Processor Simulator");
            var finished = false;
            while (!finished)
            {
                var quantum = 100;
                var validNumber = false;
                Console.WriteLine(
                    "\nPlease insert the thread´s quantum to start the simulation\n(Must be greater or equal than 10).");
                while (!validNumber)
                {
                    try
                    {
                        var quantumStr = Console.ReadLine();
                        if (quantumStr == null) continue;
                        quantum = int.Parse(quantumStr);
                        validNumber = quantum >= 10;
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Invalid input for quantum. Try again");
                    }
                }

                Console.WriteLine("\nInsert one of the following options:");
                Console.WriteLine("1 - Delay Mode");
                Console.WriteLine("2 - No Delay Mode\n");
                var answer = Console.ReadLine();
                var mode = answer != null && answer.Equals("1");
                Processor.Instance.RestartProcessor();
                Processor.Instance.Quantum = quantum;
                Processor.Instance.RunSimulation(mode);
                var validOption = false;
                Console.WriteLine(
                    "\nDo you want run the simulation again?");
                Console.WriteLine("1 - YES");
                Console.WriteLine("2 - NO\n");
                while (!validOption)
                {
                    try
                    {
                        var option = Console.ReadLine();
                        if (option == null) continue;
                        quantum = int.Parse(option);
                        validOption = (quantum==1 || quantum==2);
                        if (quantum==2)
                        {
                            finished = true;
                            Console.WriteLine("Simulation finished");
                            
                        }
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Invalid option. Try again");
                    }
                }
            }
        }
    }
}