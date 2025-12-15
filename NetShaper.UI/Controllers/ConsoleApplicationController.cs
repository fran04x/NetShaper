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
                if (_controller.IsRunning)
                {
                    // RUNNING STATE: Show stats, wait for ESC
                    await RunningStateAsync(ct);
                }
                else
                {
                    // MENU STATE: Show menu, process commands
                    var exitCode = await MenuStateAsync(ct);
                    if (exitCode.HasValue)
                        return exitCode.Value;
                }
            }

            return 0;
        }

        private async Task<int?> MenuStateAsync(CancellationToken ct)
        {
            DrawMenu();
            
            var key = Console.ReadKey(true).KeyChar;

            switch (key)
            {
                case '1':
                    HandleStart(ct);
                    return null; // Continue to running state
                    
                case '3':
                    return await HandleExitAsync(ct);
                    
                default:
                    return null; // Ignore invalid input, redraw menu
            }
        }

        private async Task RunningStateAsync(CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine("   NetShaper - Capturando Paquetes");
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("Presiona ESC para detener y volver al menú");
            Console.WriteLine();
            Console.WriteLine("Estadísticas:");
            Console.WriteLine();

            // Wait for ESC while engine is running
            while (_controller.IsRunning && !ct.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Escape)
                    {
                        HandleStop();
                        await Task.Delay(300); // Wait for clean shutdown
                        break;
                    }
                }
                
                await Task.Delay(50);
            }
        }

        private void HandleStart(CancellationToken ct)
        {
            StartResult result = _controller.Start("ip and (tcp or udp)", ct);
            
            if (result != StartResult.Success)
            {
                Console.Clear();
                Console.WriteLine("════════════════════════════════════════");
                Console.WriteLine("   Error al Iniciar");
                Console.WriteLine("════════════════════════════════════════");
                Console.WriteLine();
                Console.WriteLine($"❌ Error: {result}");
                Console.WriteLine();
                
                if (result == StartResult.OpenFailed)
                {
                    Console.WriteLine("Posibles causas:");
                    Console.WriteLine("- NO tienes privilegios de ADMINISTRADOR");
                    Console.WriteLine("- El driver WinDivert no está instalado");
                }
                else if (result == StartResult.InvalidFilter)
                {
                    Console.WriteLine("El filtro WinDivert es inválido");
                }
                
                Console.WriteLine();
                Console.WriteLine("Presiona cualquier tecla para volver al menú...");
                Console.ReadKey(true);
            }
            // If success, RunAsync will transition to RunningState automatically
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
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine("   NetShaper - Packet Shaping Monitor");
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("⚠️  IMPORTANTE: Debe ejecutarse como ADMINISTRADOR");
            Console.WriteLine();
            Console.WriteLine("1. Start  - Iniciar captura de paquetes");
            Console.WriteLine("3. Exit   - Salir del programa");
            Console.WriteLine();
            Console.WriteLine("Filtro: ip and (tcp or udp)");
            Console.WriteLine();
        }
    }
}
