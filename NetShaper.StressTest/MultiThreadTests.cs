// NetShaper.StressTest/MultiThreadTests.cs
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetShaper.Engine;

namespace NetShaper.StressTest
{
    /// <summary>
    /// Tests for validating multi-threaded performance with EnginePool.
    /// </summary>
    static class MultiThreadTests
    {
        private const int PerformancePort = 55556; // Different port to avoid conflicts
        private const int WarmupPackets = 200;
        private const int MeasurementPackets = 200_000;

        public static async Task RunMultiThreadPerformanceAsync(int threadCount)
        {
            Console.WriteLine($"\u003e\u003e\u003e MULTI-THREADING TEST ({threadCount} threads) \u003c\u003c\u003c");

            using var pool = TestServiceFactory.CreateEnginePool(threadCount);

            var result = pool.Start($"outbound and udp.DstPort == {PerformancePort}");
            if (result != NetShaper.Abstractions.StartResult.Success)
            {
                Console.WriteLine($"[FAIL] Could not start pool: {result}");
                return;
            }

            await WarmupAsync();
            PrepareGcForMeasurement();

            long memStart = GC.GetTotalMemory(true);
            int g0Start = GC.CollectionCount(0);
            long tStart = Stopwatch.GetTimestamp();

            await RunMeasurementTrafficAsync();

            Thread.Sleep(500);  // Let all packets finish processing

            pool.Stop();

            long tEnd = Stopwatch.GetTimestamp();
            long memEnd = GC.GetTotalMemory(false);
            int g0End = GC.CollectionCount(0);
            
            PrintPerformanceSummary(
                threadCount,
                pool.TotalPacketsProcessed, 
                memStart, memEnd, 
                g0Start, g0End, 
                tStart, tEnd);
        }

        private static async Task WarmupAsync()
        {
            Console.Write("Calentando... ");
            await Task.Run(() => SendTrafficMixed(WarmupPackets, PerformancePort));
            Thread.Sleep(200);
            Console.WriteLine("OK");
        }

        private static void PrepareGcForMeasurement()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static async Task RunMeasurementTrafficAsync()
        {
            Console.WriteLine($"Enviando {MeasurementPackets:N0} paquetes mixtos...");
            await Task.Run(() => SendTrafficMixed(MeasurementPackets, PerformancePort));
        }

        private static void PrintPerformanceSummary(
            int threadCount,
            long packetCount, 
            long memStart, long memEnd, 
            int g0Start, int g0End, 
            long tStart, long tEnd)
        {
            long memDelta = memEnd - memStart;
            int gcTriggers = g0End - g0Start;
            double seconds = Stopwatch.GetElapsedTime(tStart, tEnd).TotalSeconds - 0.5;
            if (seconds < 0.1) seconds = 0.1;
            long pps = (long)(packetCount / seconds);

            // Calculate expected performance and scaling efficiency
            long baselinePPS = 34_000;  // Known baseline from single-thread
            long expectedPPS = baselinePPS * threadCount;
            double scalingEfficiency = (double)pps / expectedPPS * 100.0;

            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine($"RESULTADOS MULTI-THREADING ({threadCount} threads):");
            Console.WriteLine($"PPS: {pps:N0}");
            Console.WriteLine($"Expected PPS ({threadCount}×34k): {expectedPPS:N0}");
            Console.WriteLine($"Scaling Efficiency: {scalingEfficiency:F1}%");
            Console.WriteLine($"GC Gen0: {gcTriggers}");
            Console.WriteLine($"Memoria Delta: {memDelta / 1024.0:F2} KB");
            Console.WriteLine($"Paquetes procesados: {packetCount:N0}");
            Console.WriteLine("────────────────────────────────────────");

            if (gcTriggers == 0)
                Console.WriteLine("✅ ZERO ALLOC CONFIRMADO");
            else
                Console.WriteLine("❌ Asignaciones detectadas");

            if (scalingEfficiency >= 90)
                Console.WriteLine("✅ EXCELLENT SCALING (≥90%)");
            else if (scalingEfficiency >= 70)
                Console.WriteLine("⚠️ GOOD SCALING (70-90%)");
            else if (scalingEfficiency >= 50)
                Console.WriteLine("⚠️ MODERATE SCALING (50-70%)");
            else
                Console.WriteLine("❌ POOR SCALING (<50%)");
                
            Console.WriteLine("════════════════════════════════════════");
        }

        private static void SendTrafficMixed(int count, int port)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(IPAddress.Loopback, port);

            byte[] small = new byte[64];
            byte[] medium = new byte[512];
            byte[] large = new byte[1400];
            
            for (int i = 0; i < count; i++)
            {
                int pick = i % 3;
                if (pick == 0) socket.Send(small);
                else if (pick == 1) socket.Send(medium);
                else socket.Send(large);

                if ((i & 1023) == 0) Thread.Yield();
            }
        }
    }
}
