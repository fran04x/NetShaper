// NetShaper.App/Program.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using NetShaper.UI;

namespace NetShaper.UI
{
    sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                var controller = Bootstrap.BuildController();
                return await controller.RunAsync(cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL] {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }
    }
}
