using System;
using System.Threading.Tasks;

namespace NetShaper.StressTest
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Support command-line arguments for automation
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "1":
                        RealTests.RunStabilityReal();
                        return 0;
                    case "2":
                        await RealTests.RunPerformanceRealAsync();
                        return 0;
                    case "3":
                        await ChaoticTests.RunChaoticAssaultAsync();
                        return 0;
                    default:
                        Console.WriteLine($"Invalid argument: {args[0]}");
                        return 1;
                }
            }

            // Interactive menu mode
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== NETSHAPER STRESS TEST SUITE ===");
                Console.WriteLine("1. Test de Estabilidad Real (uso correcto)");
                Console.WriteLine("2. Test de Rendimiento Real (PPS/Jitter/ZeroAlloc)");
                Console.WriteLine("3. Test Caótico (mal uso extremo)");
                Console.WriteLine("4. Salir");
                Console.Write("\nSelecciona una opción: ");

                var key = Console.ReadKey(true);
                Console.WriteLine();

                switch (key.KeyChar)
                {
                    case '1':
                        RealTests.RunStabilityReal();
                        break;
                    case '2':
                        await RealTests.RunPerformanceRealAsync();
                        break;
                    case '3':
                        await ChaoticTests.RunChaoticAssaultAsync();
                        break;
                    case '4':
                        return 0;
                    default:
                        Console.WriteLine("Opción no válida.");
                        break;
                }

                Console.WriteLine("\nPresiona cualquier tecla para volver al menú...");
                Console.ReadKey();
            }
        }
    }
}
