using System;
using System.Threading.Tasks;

namespace NetShaper.StressTest
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "NetShaper Performance Tests";

            // Command line usage: dotnet run [threads|stability]
            // Example: dotnet run 4
            // Example: dotnet run stability
            if (args.Length > 0)
            {
                if (args[0].ToLowerInvariant() == "stability")
                {
                    RealTests.RunStabilityReal();
                    return 0;
                }
                else if (int.TryParse(args[0], out int threads) && threads >= 1 && threads <= 16)
                {
                    await PerformanceTests.RunPerformanceTestAsync(threads);
                    return 0;
                }
                else
                {
                    Console.WriteLine($"Invalid argument: {args[0]}");
                    Console.WriteLine("Usage: dotnet run [threads|stability]");
                    Console.WriteLine("  dotnet run 4           - Run with 4 threads");
                    Console.WriteLine("  dotnet run stability   - Run stability test (10000 cycles)");
                    return 1;
                }
            }

            // Interactive mode
            while (true)
            {
                Console.Clear();
                Console.WriteLine("════════════════════════════════════════");
                Console.WriteLine("   NetShaper Performance Test Suite");
                Console.WriteLine("════════════════════════════════════════");
                Console.WriteLine();
                Console.WriteLine("Selecciona un test:");
                Console.WriteLine();
                Console.WriteLine("1-16  = Performance Test (N threads)");
                Console.WriteLine("        1 = Single thread (83k PPS)");
                Console.WriteLine("        4 = Quad thread (~81k PPS)");
                Console.WriteLine();
                Console.WriteLine("S     = Stability Test (10,000 Start/Stop cycles)");
                Console.WriteLine();
                Console.WriteLine("0     = Salir");
                Console.Write("\nOpción: ");

                var input = Console.ReadLine()?.Trim();
                
                if (input == "0")
                    return 0;

                if (input?.ToUpperInvariant() == "S")
                {
                    Console.WriteLine();
                    RealTests.RunStabilityReal();
                    Console.WriteLine();
                    Console.WriteLine("Presiona cualquier tecla para continuar...");
                    Console.ReadKey(true);
                }
                else if (int.TryParse(input, out int threadCount) && threadCount >= 1 && threadCount <= 16)
                {
                    Console.WriteLine();
                    await PerformanceTests.RunPerformanceTestAsync(threadCount);
                    
                    Console.WriteLine();
                    Console.WriteLine("Presiona cualquier tecla para continuar...");
                    Console.ReadKey(true);
                }
                else
                {
                    Console.WriteLine("Número inválido. Debe ser entre 1 y 16.");
                    Console.ReadKey(true);
                }
            }
        }
    }
}
