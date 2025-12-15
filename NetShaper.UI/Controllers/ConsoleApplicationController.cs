using System;
using System.Threading;
using System.Threading.Tasks;
using NetShaper.Abstractions;
using NetShaper.UI.Views;

namespace NetShaper.UI.Controllers
{
    sealed class ConsoleApplicationController : IApplicationController
    {
        private readonly EngineController _controller;

        public ConsoleApplicationController(IEngine engine, IPacketLogger logger, IConsoleView consoleView)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(consoleView);

            _controller = new EngineController(engine, logger, consoleView);
        }

        public async Task<int> RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                DrawMenu();
                char key = Console.ReadKey(true).KeyChar;

                switch (key)
                {
                    case '1':
                        HandleStart(ct);
                        break;

                    case '2':
                        HandleStop();
                        break;

                    case '3':
                        return await HandleExitAsync(ct);
                }
            }

            return 0;
        }

        private void HandleStart(CancellationToken ct)
        {
            // La UI no toca el Engine directamente.
            // Solo delega al EngineController.
            StartResult result = _controller.Start("ip and (tcp or udp)", ct);
            
            // Si falla, mostrar error y esperar
            if (result != StartResult.Success)
            {
                Console.WriteLine($"\n❌ Error al iniciar el engine: {result}");
                Console.WriteLine("\nPosibles causas:");
                if (result == StartResult.OpenFailed)
                {
                    Console.WriteLine("- NO tienes privilegios de ADMINISTRADOR (WinDivert requiere admin)");
                    Console.WriteLine("- El driver WinDivert no está instalado");
                }
                else if (result == StartResult.InvalidFilter)
                {
                    Console.WriteLine("- El filtro WinDivert es inválido");
                }
                Console.WriteLine("\nPresiona cualquier tecla para continuar...");
                Console.ReadKey(true);
            }
            else
            {
                Console.WriteLine("\n✅ Engine iniciado correctamente");
                Console.WriteLine("El motor está capturando y mostrando estadísticas en tiempo real.");
                Console.WriteLine("Usa la opción '2. Stop' para detener la captura.");
                Console.WriteLine("\nPresiona cualquier tecla para continuar...");
                Console.ReadKey(true);
            }
        }

        private void HandleStop()
        {
            _controller.Stop();
        }

        private async Task<int> HandleExitAsync(CancellationToken ct)
        {
            await _controller.ShutdownAsync();
            return 0;
        }

        private static void DrawMenu()
        {
            Console.Clear();
            Console.WriteLine("NetShaper");
            Console.WriteLine("1. Start");
            Console.WriteLine("2. Stop");
            Console.WriteLine("3. Exit");
        }
    }
}
