// NetShaper.StressTest/RealTests.cs
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetShaper.Engine;
using NetShaper.Abstractions;

namespace NetShaper.StressTest
{
    static class RealTests
    {
        private const int PerformancePort = 55555;
        private const int WarmupPackets = 200;
        private const int MeasurementPackets = 200_000;
        private const int BurstSize = 5000;
        private const int BurstCount = 10;

        public static void RunStabilityReal()
        {
            Console.WriteLine(">>> INICIANDO TEST DE ESTABILIDAD REAL (uso correcto) <<<");
            int cycles = 10000;
            int totalFallos = 0;
            int startFailures = 0;
            int captureLoopFailures = 0;
            int exceptionFailures = 0;
            var failedCycles = new List<int>();
            
            var sw = Stopwatch.StartNew();
            for (int i = 1; i <= cycles; i++)
            {
                FailureType failure = RunSingleCycle(i);
                
                if (failure != FailureType.None)
                {
                    totalFallos++;
                    failedCycles.Add(i);
                    
                    switch (failure)
                    {
                        case FailureType.StartFailed:
                            startFailures++;
                            break;
                        case FailureType.CaptureLoopFailed:
                            captureLoopFailures++;
                            break;
                        case FailureType.Exception:
                            exceptionFailures++;
                            break;
                    }
                }
                
                if (i % 100 == 0) Console.Write(".");
            }

            sw.Stop();
            Console.WriteLine("\n");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("RESUMEN ESTABILIDAD REAL:");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine($"Tiempo total: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Ciclos ejecutados: {cycles:N0}");
            Console.WriteLine($"Fallos totales: {totalFallos}");
            
            if (totalFallos > 0)
            {
                Console.WriteLine("\nFALLOS POR CATEGORÍA:");
                Console.WriteLine($"  - Start failures:       {startFailures}");
                Console.WriteLine($"  - CaptureLoop failures: {captureLoopFailures}");
                Console.WriteLine($"  - Exceptions:           {exceptionFailures}");
                
                Console.WriteLine($"\nTasa de fallo: {(totalFallos * 100.0 / cycles):F3}%");
                Console.WriteLine($"\nCiclos con fallos: {string.Join(", ", failedCycles.Take(20))}");
                if (failedCycles.Count > 20)
                    Console.WriteLine($"  ... y {failedCycles.Count - 20} más");
            }
            
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine(totalFallos == 0 ? "✅ ESTABLE" : "❌ INESTABLE");
            Console.WriteLine("═══════════════════════════════════════════════════════");
        }
        
        private enum FailureType
        {
            None,
            StartFailed,
            CaptureLoopFailed,
            Exception
        }

        private static FailureType RunSingleCycle(int ciclo)
        {
            try
            {
                using var engine = TestServiceFactory.CreateEngine();
                using var cts = new CancellationTokenSource();

                StartResult startResult = engine.Start("ip", cts.Token);
                if (startResult != StartResult.Success)
                {
                    Console.WriteLine($"\n[FAIL ciclo {ciclo}] Start failed: {startResult}");
                    return FailureType.StartFailed;
                }

                var captureResult = EngineResult.InvalidState;
                var t = new Thread(() =>
                {
                    captureResult = engine.RunCaptureLoop();
                })
                {
                    IsBackground = true
                };

                t.Start();

                Thread.Sleep(Random.Shared.Next(0, 5));
                engine.Stop();

                // Espera activa por estado, no por timeout fijo
                SpinWait spin = default;
                while (engine.IsRunning)
                    spin.SpinOnce();

                t.Join(); // ya debería estar fuera

                // Validate capture loop result
                if (captureResult != EngineResult.Success && captureResult != EngineResult.Stopped)
                {
                    Console.WriteLine($"\n[FAIL ciclo {ciclo}] CaptureLoop error: {captureResult}");
                    return FailureType.CaptureLoopFailed;
                }

                return FailureType.None;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[EXCEPTION ciclo {ciclo}] {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"  Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                return FailureType.Exception;
            }
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

        private static void RunBursts()
        {
            Console.WriteLine("Enviando ráfagas (bursts)...");
            for (int i = 0; i < BurstCount; i++)
            {
                SendTrafficBurst(BurstSize, PerformancePort);
                Thread.Sleep(10);
            }
        }

        private static void PrintPerformanceSummary(long packetCount, long memStart, long memEnd, int g0Start, int g0End, long tStart, long tEnd)
        {
            long memDelta = memEnd - memStart;
            int gcTriggers = g0End - g0Start;
            double seconds = Stopwatch.GetElapsedTime(tStart, tEnd).TotalSeconds - 0.5;
            if (seconds < 0.1) seconds = 0.1;
            long pps = (long)(packetCount / seconds);

            Console.WriteLine("────────────────────────────────");
            Console.WriteLine("RESULTADOS NIVEL 3:");
            Console.WriteLine($"PPS: {pps:N0}");
            Console.WriteLine($"GC Gen0: {gcTriggers}");
            Console.WriteLine($"Memoria Delta: {memDelta / 1024.0:F2} KB");
            Console.WriteLine($"Paquetes procesados: {packetCount:N0}");

            if (gcTriggers == 0) Console.WriteLine("✅ ZERO ALLOC CONFIRMADO (Nivel 3)");
            else Console.WriteLine("❌ Asignaciones detectadas");
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

        private static void SendTrafficBurst(int count, int port)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(IPAddress.Loopback, port);
            byte[] data = new byte[1400];
            for (int i = 0; i < count; i++) socket.Send(data);
        }
    }
}