// NetShaper.MultiThreadTest/Program.cs
// Simple test to validate EnginePool with 1, 2, and 4 threads

using System;
using System.Diagnostics;
using System.Threading;
using NetShaper.Abstractions;
using NetShaper.Engine;
using NetShaper.Infrastructure;
using NetShaper.Native;

namespace NetShaper.MultiThreadTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("NetShaper Multi-Threading Test");
            Console.WriteLine("================================\n");
            
            int threadCount = args.Length > 0 && int.TryParse(args[0], out int tc) ? tc : 1;
            
            Console.WriteLine($"Testing with {threadCount} thread(s)...\n");
            
            // Create dependencies
            var logger = new ConsolePacketLogger();
            
            // Factory for captures
            Func<IPacketCapture> captureFactory = () => new WinDivertAdapter();
            
            // Create engine pool
            using var pool = new EnginePool(logger, captureFactory, threadCount);
            
            var filter = "outbound and tcp";
            Console.WriteLine($"Filter: {filter}");
            Console.WriteLine("Starting engines...");
            
            var startResult = pool.Start(filter);
            if (startResult != StartResult.Success)
            {
                Console.WriteLine($"Failed to start: {startResult}");
                return;
            }
            
            Console.WriteLine("Engines started. Processing for 3 seconds...\n");
            
            var sw = Stopwatch.StartNew();
            Thread.Sleep(3000);
            
            pool.Stop();
            sw.Stop();
            
            var totalPackets = pool.TotalPacketsProcessed;
            var seconds = sw.Elapsed.TotalSeconds;
            var pps = totalPackets / seconds;
            
            Console.WriteLine("\n── RESULTS ──────────────────────");
            Console.WriteLine($"Threads: {threadCount}");
            Console.WriteLine($"Duration: {seconds:F2}s");
            Console.WriteLine($"Total Packets: {totalPackets:N0}");
            Console.WriteLine($"PPS: {pps:N0}");
            Console.WriteLine($"Per-thread PPS: {pps/threadCount:N0}");
            Console.WriteLine("─────────────────────────────────\n");
            
            // Expected scaling
            double singleThreadBaseline = 34000; // Known baseline
            double expectedPPS = singleThreadBaseline * threadCount;
            double scalingEfficiency = (pps / expectedPPS) * 100;
            
            Console.WriteLine($"Expected PPS ({threadCount}x scaling): {expectedPPS:N0}");
            Console.WriteLine($"Scaling efficiency: {scalingEfficiency:F1}%");
            
            if (scalingEfficiency >= 90)
                Console.WriteLine("✅ Excellent scaling!");
            else if (scalingEfficiency >= 70)
                Console.WriteLine("⚠️ Good scaling, but room for improvement");
            else
                Console.WriteLine("❌ Poor scaling, investigation needed");
        }
    }
}
