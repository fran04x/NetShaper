// NetShaper.Benchmarks/LatencyJitterBenchmark.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using NetShaper.Abstractions;
using NetShaper.Composition;

namespace NetShaper.Benchmarks
{
    /// <summary>
    /// Benchmark de latencia individual y jitter.
    /// Optimizado para hardware lento - expectativas realistas.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 1, iterationCount: 3)]
    public class LatencyJitterBenchmark
    {
        private const int Port = 55558;
        private const int SampleCount = 500; // Reducido para hardware lento
        
        private IEngine _engine = null!;
        private CancellationTokenSource _cts = null!;
        private Task _captureTask = null!;
        private Socket _socket = null!;
        private byte[] _packet = null!;

        [GlobalSetup]
        public void Setup()
        {
            _packet = new byte[512];
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Connect(IPAddress.Loopback, Port);

            var services = new ServiceCollection();
            services.AddNetShaperServices();
            var provider = services.BuildServiceProvider();

            _engine = provider.GetRequiredService<IEngine>();
            _cts = new CancellationTokenSource();

            var result = _engine.Start($"outbound and udp.DstPort == {Port}", _cts.Token);
            if (result != StartResult.Success)
                throw new InvalidOperationException($"Start failed: {result}");

            _captureTask = Task.Run(() => _engine.RunCaptureLoop(), _cts.Token);

            // Warmup ligero
            for (int i = 0; i < 100; i++)
            {
                _socket.Send(_packet);
                Thread.Sleep(5); // Delay para no saturar
            }

            Thread.Sleep(500);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _engine.Stop();
            _cts.Cancel();
            try { _captureTask.Wait(2000); } catch { }
            _engine.Dispose();
            _cts.Dispose();
            _socket.Dispose();
        }

        /// <summary>
        /// Mide latencia de procesamiento individual (send to processed).
        /// Envía 1 paquete, espera que se procese, mide tiempo total.
        /// </summary>
        [Benchmark(Description = "Individual Latency (500 samples)")]
        public LatencyStats MeasureIndividualLatency()
        {
            var latencies = new List<long>(SampleCount);

            for (int i = 0; i < SampleCount; i++)
            {
                long baseline = _engine.PacketCount;
                long start = Stopwatch.GetTimestamp();

                _socket.Send(_packet);

                // Esperar hasta que el paquete sea procesado
                while (_engine.PacketCount == baseline)
                {
                    Thread.SpinWait(50);
                }

                long end = Stopwatch.GetTimestamp();
                latencies.Add(end - start);

                // Pequeño delay entre mediciones para evitar saturación
                Thread.Sleep(2);
            }

            var result = CalculateLatencyStats(latencies);
            
            // Guardar en Desktop con ruta absoluta
            try
            {
                string resultsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "netshaper_latency_results.txt");
                File.AppendAllText(resultsPath, 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] LATENCY: {result}\n");
            }
            catch { /* Ignore file errors in benchmark */ }
            
            return result;
        }

        /// <summary>
        /// Mide jitter enviando paquetes a ritmo constante (100 PPS).
        /// </summary>
        [Benchmark(Description = "Jitter at 100 PPS")]
        public JitterStats MeasureJitterAt100Pps()
        {
            var latencies = new List<long>(SampleCount);
            long intervalTicks = Stopwatch.Frequency / 100; // 10ms interval = 100 PPS

            long nextSend = Stopwatch.GetTimestamp();

            for (int i = 0; i < SampleCount; i++)
            {
                long baseline = _engine.PacketCount;
                long start = Stopwatch.GetTimestamp();

                // Esperar hasta el momento exacto de envío
                while (Stopwatch.GetTimestamp() < nextSend)
                    Thread.SpinWait(10);

                _socket.Send(_packet);
                nextSend += intervalTicks;

                // Esperar procesamiento
                while (_engine.PacketCount == baseline)
                {
                    Thread.SpinWait(50);
                }

                long end = Stopwatch.GetTimestamp();
                latencies.Add(end - start);
            }

            var result = CalculateJitterStats(latencies);
            
            // Guardar en Desktop con ruta absoluta
            try
            {
                string resultsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "netshaper_latency_results.txt");
                File.AppendAllText(resultsPath, 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] JITTER: {result}\n");
            }
            catch { /* Ignore file errors in benchmark */ }
            
            return result;
        }

        private static LatencyStats CalculateLatencyStats(List<long> latencies)
        {
            latencies.Sort();
            double ToMicroseconds(long ticks) => (ticks * 1_000_000.0) / Stopwatch.Frequency;

            return new LatencyStats
            {
                P50_us = ToMicroseconds(latencies[latencies.Count / 2]),
                P95_us = ToMicroseconds(latencies[(int)(latencies.Count * 0.95)]),
                P99_us = ToMicroseconds(latencies[(int)(latencies.Count * 0.99)]),
                Min_us = ToMicroseconds(latencies[0]),
                Max_us = ToMicroseconds(latencies[^1]),
                Mean_us = ToMicroseconds((long)latencies.Average())
            };
        }

        private static JitterStats CalculateJitterStats(List<long> latencies)
        {
            double ToMicroseconds(long ticks) => (ticks * 1_000_000.0) / Stopwatch.Frequency;

            var mean = latencies.Average();
            var variance = latencies.Select(l => Math.Pow(l - mean, 2)).Average();
            var stdDev = Math.Sqrt(variance);

            var deltas = new List<double>();
            for (int i = 1; i < latencies.Count; i++)
            {
                deltas.Add(Math.Abs(latencies[i] - latencies[i - 1]));
            }

            deltas.Sort();

            return new JitterStats
            {
                StdDev_us = ToMicroseconds((long)stdDev),
                MaxJitter_us = ToMicroseconds((long)deltas.Max()),
                AvgJitter_us = ToMicroseconds((long)deltas.Average()),
                P95Jitter_us = ToMicroseconds((long)deltas[(int)(deltas.Count * 0.95)])
            };
        }
    }

    public struct LatencyStats
    {
        public double P50_us;
        public double P95_us;
        public double P99_us;
        public double Min_us;
        public double Max_us;
        public double Mean_us;

        public override string ToString() =>
            $"P50={P50_us:F0}μs P95={P95_us:F0}μs P99={P99_us:F0}μs (Min={Min_us:F0} Max={Max_us:F0})";
    }

    public struct JitterStats
    {
        public double StdDev_us;
        public double MaxJitter_us;
        public double AvgJitter_us;
        public double P95Jitter_us;

        public override string ToString() =>
            $"Avg={AvgJitter_us:F0}μs P95={P95Jitter_us:F0}μs Max={MaxJitter_us:F0}μs (σ={StdDev_us:F0})";
    }
}