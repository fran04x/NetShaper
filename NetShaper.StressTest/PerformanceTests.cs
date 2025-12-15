// NetShaper.StressTest/PerformanceTests.cs
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetShaper.Abstractions;

namespace NetShaper.StressTest
{
    /// <summary>
    /// Unified performance tests for NetShaper Engine with configurable threads.
    /// </summary>
    internal static class PerformanceTests
    {
        private const int PerformancePort = 55555;
        private const int PacketCount = 200_000;
        private const int WarmupMs = 500;
        private const int TestDuration = 2000;

        public static async Task RunPerformanceTestAsync(int threadCount)
        {
            Console.WriteLine($">>> NetShaper Performance Test ({threadCount} thread{(threadCount > 1 ? "s" : "")}) <<<");
            Console.Write("Calentando... ");

            using var engine = TestServiceFactory.CreateEngine(threadCount);

            var result = engine.Start($"outbound and udp.DstPort == {PerformancePort}");
            if (result != StartResult.Success)
            {
                Console.WriteLine($"❌ Error: {result}");
                return;
            }

            Console.WriteLine("OK");
            await Task.Delay(WarmupMs);

            // Telemetry baseline
            long packetsBefore = engine.PacketCount;
            long gen0Before = GC.CollectionCount(0);

            // Traffic generation
            Console.WriteLine($"Enviando {PacketCount:N0} paquetes mixtos...");
            
            var sw = Stopwatch.StartNew();
            await GenerateTrafficAsync(PacketCount, PerformancePort);
            sw.Stop();

            // Wait for processing
            await Task.Delay(TestDuration);
            engine.Stop();

            // Calculate results
            long packetsProcessed = engine.PacketCount - packetsBefore;
            long gen0After = GC.CollectionCount(0);
            int gen0Collections = (int)(gen0After - gen0Before);

            double pps = packetsProcessed / (sw.ElapsedMilliseconds / 1000.0);
            
            // Expected PPS calculation
            int expectedSingleThread = 83_000;
            double expectedPPS = threadCount == 1 ? expectedSingleThread : expectedSingleThread * 0.98; // slight degradation with more threads
            double efficiency = (pps / expectedPPS) * 100.0;

            // Display results
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine($"RESULTS ({threadCount} thread{(threadCount > 1 ? "s" : "")}):");
            Console.WriteLine($"PPS: {pps:N0}");
            Console.WriteLine($"Expected PPS: {expectedPPS:N0}");
            Console.WriteLine($"Efficiency: {efficiency:F1}%");
            Console.WriteLine($"GC Gen0: {gen0Collections}");
            Console.WriteLine($"Packets Processed: {packetsProcessed:N0}");
            Console.WriteLine("────────────────────────────────────────");

            // Verdicts
            if (gen0Collections == 0)
                Console.WriteLine("✅ ZERO ALLOC CONFIRMED");
            else
                Console.WriteLine($"⚠️  {gen0Collections} Gen0 collections detected");

            if (efficiency >= 90)
                Console.WriteLine("✅ EXCELLENT PERFORMANCE (≥90%)");
            else if (efficiency >= 70)
                Console.WriteLine("✅ GOOD PERFORMANCE (≥70%)");
            else if (efficiency >= 50)
                Console.WriteLine("⚠️ MODERATE PERFORMANCE (50-70%)");
            else
                Console.WriteLine("❌ POOR PERFORMANCE (<50%)");

            Console.WriteLine("════════════════════════════════════════");
        }

        private static async Task GenerateTrafficAsync(int count, int port)
        {
            await Task.Run(() =>
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var endpoint = new IPEndPoint(IPAddress.Loopback, port);
                
                byte[] small = new byte[64];
                byte[] medium = new byte[512];
                byte[] large = new byte[1400];

                for (int i = 0; i < count; i++)
                {
                    byte[] payload = (i % 3) switch
                    {
                        0 => small,
                        1 => medium,
                        _ => large
                    };
                    
                    socket.SendTo(payload, endpoint);
                    
                    if ((i & 255) == 0)
                        Thread.Yield();
                }
            });
        }
    }
}
