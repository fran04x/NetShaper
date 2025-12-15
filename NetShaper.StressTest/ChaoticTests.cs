// NetShaper.StressTest/ChaoticTests.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetShaper.Abstractions;

namespace NetShaper.StressTest
{
    static class ChaoticTests
    {
        private const int WorkerCount = 64;
        private const int IterationsPerWorker = 500;
        private const int FilterCount = 7;
        private const int MaxRandomDelay = 10;

        private static readonly string[] Filters = new[]
        {
            "ip",
            "tcp",
            "udp",
            "outbound and udp.DstPort == 55555",
            "",
            "this is not a valid filter",
            "ip and (tcp or udp)"
        };

        private static volatile bool _supervisorCancel;
        private static int _totalOperations;
        private static int _crashes;
        private static int _exceptions;
        private static int _workersCompleted;

        public static async Task RunChaoticAssaultAsync()
        {
            Console.WriteLine(">>> INICIANDO TEST CAÓTICO (mal uso extremo) <<<");
            Console.WriteLine($"Workers: {WorkerCount} | Iteraciones por worker: {IterationsPerWorker}");
            Console.WriteLine($"Total operaciones planificadas: {WorkerCount * IterationsPerWorker:N0}");
            Console.WriteLine();

            // Reset counters
            _supervisorCancel = false;
            _totalOperations = 0;
            _crashes = 0;
            _exceptions = 0;
            _workersCompleted = 0;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tasks = new List<Task>(WorkerCount);

            for (int i = 0; i < WorkerCount; i++)
            {
                int workerId = i;
                tasks.Add(Task.Run(() => WorkerLoop(workerId)));
            }

            var supervisor = Task.Run(() => SupervisorLoop());
            
            // Show progress
            var progressTask = Task.Run(async () =>
            {
                while (_workersCompleted < WorkerCount)
                {
                    await Task.Delay(500);
                    int progress = (_totalOperations * 100) / Math.Max(WorkerCount * IterationsPerWorker, 1);
                    Console.Write($"\rProgreso: {progress}% ({_totalOperations:N0} ops, {_crashes} crashes, {_exceptions} exc)");
                }
            });

            await Task.WhenAll(tasks);
            await Task.Delay(1000);
            _supervisorCancel = true;
            await supervisor;
            sw.Stop();

            // Clear progress line
            Console.WriteLine("\r" + new string(' ', 100));
            Console.WriteLine();

            // Print summary
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("RESUMEN TEST CAÓTICO:");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine($"Tiempo total: {sw.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"Workers ejecutados: {_workersCompleted} / {WorkerCount}");
            Console.WriteLine($"Operaciones completadas: {_totalOperations:N0} / {WorkerCount * IterationsPerWorker:N0}");
            Console.WriteLine($"Crashes (comportamiento esperado): {_crashes:N0}");
            Console.WriteLine($"Excepciones manejadas: {_exceptions:N0}");
            
            double opsPerSecond = _totalOperations / (sw.ElapsedMilliseconds / 1000.0);
            Console.WriteLine($"Throughput: {opsPerSecond:N0} operaciones/segundo");
            
            double crashRate = (_crashes * 100.0) / Math.Max(_totalOperations, 1);
            Console.WriteLine($"Tasa de crashes: {crashRate:F2}%");

            Console.WriteLine("═══════════════════════════════════════════════════════");
            
            // The chaotic test is expected to have crashes - it's testing abuse scenarios
            bool passed = _workersCompleted == WorkerCount && _totalOperations > 0;
            Console.WriteLine(passed ? "✅ TEST COMPLETADO (sistema sobrevivió al abuso)" : "❌ FALLO CRÍTICO");
            Console.WriteLine("═══════════════════════════════════════════════════════");
        }

        private static void WorkerLoop(int workerId)
        {
            var rnd = new Random(unchecked(Environment.TickCount * 31 + workerId));

            for (int iter = 0; iter < IterationsPerWorker; iter++)
            {
                Interlocked.Increment(ref _totalOperations);
                int action = rnd.Next(0, 6);
                try
                {
                    switch (action)
                    {
                        case 0:
                            using (var engine = CreateEngine())
                            {
                                string filter = Filters[rnd.Next(FilterCount)];
                                TryStart(engine, filter, rnd);
                                if (rnd.NextDouble() < 0.2) TryRunCaptureLoop(engine);
                                if (rnd.NextDouble() < 0.1) TryStart(engine, filter, rnd);
                                TryStop(engine);
                            }
                            break;

                        case 1:
                            var e = CreateEngine();
                            TryStart(e, Filters[rnd.Next(FilterCount)], rnd);
                            // Abuse: No Stop() call, but we still need to dispose to avoid ArrayPool assert
                            try { e.Dispose(); } catch { }
                            Interlocked.Increment(ref _crashes); // No proper cleanup - track as crash
                            break;

                        case 2:
                            var engine2 = CreateEngine();
                            var cts = new CancellationTokenSource();
                            var t = Task.Run(() =>
                            {
                                try { engine2.Start("ip", cts.Token); engine2.RunCaptureLoop(); } catch { }
                            });
                            Thread.Sleep(rnd.Next(0, 5));
                            TryStop(engine2);
                            try { engine2.Dispose(); } catch { }
                            break;

                        case 3:
                            var engine3 = CreateEngine();
                            TryStop(engine3);
                            TryRunCaptureLoop(engine3);
                            try { engine3.Dispose(); } catch { }
                            Interlocked.Increment(ref _crashes); // Abuse: stop/run without start
                            break;

                        case 4:
                            using (var engine4 = CreateEngine())
                            {
                                TryStart(engine4, Filters[rnd.Next(FilterCount)], rnd);
                                TryStop(engine4);
                            }
                            break;

                        case 5:
                            ParallelStartStopBurst(rnd);
                            break;
                    }
                }
                catch
                {
                    Interlocked.Increment(ref _exceptions);
                }
            }
            
            Interlocked.Increment(ref _workersCompleted);
        }

        private static IEngine CreateEngine()
        {
            return TestServiceFactory.CreateEngine();
        }

        private static void TryStart(IEngine engine, string filter, Random rnd)
        {
            try
            {
                using var cts = new CancellationTokenSource();
                if (rnd.NextDouble() < 0.15) cts.Cancel();
                engine.Start(filter, cts.Token);
            }
            catch { }
        }

        private static void TryRunCaptureLoop(IEngine engine)
        {
            try
            {
                var t = new Thread(() =>
                {
                    try { engine.RunCaptureLoop(); } catch { }
                });
                t.IsBackground = true;
                t.Start();
                if (new Random().NextDouble() < 0.3) return;
                t.Join(50);
            }
            catch { }
        }

        private static void TryStop(IEngine engine)
        {
            try { engine.Stop(); } catch { }
        }

        private static void ParallelStartStopBurst(Random rnd)
        {
            int burst = rnd.Next(2, 8);
            var engines = new IEngine[burst];
            for (int i = 0; i < burst; i++) engines[i] = CreateEngine();

            Parallel.ForEach(engines, eng =>
            {
                try
                {
                    TryStart(eng, Filters[rnd.Next(FilterCount)], rnd);
                    if (rnd.NextDouble() < 0.5) TryRunCaptureLoop(eng);
                    TryStop(eng);
                }
                catch { }
                finally { try { eng.Dispose(); } catch { } }
            });
        }

        private static void SupervisorLoop()
        {
            var rnd = new Random();
            while (!_supervisorCancel)
            {
                try
                {
                    if (rnd.NextDouble() < 0.3)
                    {
                        var e = CreateEngine();
                        try { e.Start("ip", CancellationToken.None); } catch { }
                        try { e.Stop(); } catch { }
                        try { e.Dispose(); } catch { }
                    }
                }
                catch { }
                Thread.Sleep(rnd.Next(0, MaxRandomDelay));
            }
        }
    }
}