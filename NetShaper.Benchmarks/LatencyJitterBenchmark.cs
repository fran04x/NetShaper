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
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Microsoft.Extensions.DependencyInjection;
using NetShaper.Abstractions;
using NetShaper.Composition;

namespace NetShaper.Benchmarks
{
    public class LatencyJitterInProcessConfig : ManualConfig
    {
        public LatencyJitterInProcessConfig()
        {
            // Usamos ShortRun para pruebas rápidas
            // Y aumentamos el timeout a 10 minutos para evitar la excepción "takes too long"
            AddJob(Job.ShortRun
                .WithWarmupCount(1)
                .WithIterationCount(3)
                .WithToolchain(new InProcessNoEmitToolchain(
                    timeout: TimeSpan.FromMinutes(10), 
                    logOutput: true // Para ver si avanza
                ))); 
        }
    }

    /// <summary>
    /// Benchmark de latencia individual y jitter.
    /// Optimizado para hardware lento - expectativas realistas.
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(LatencyJitterInProcessConfig))]
    public class LatencyJitterBenchmark
    {
        // R707: Constants first
        private const int Port = 55558;
        private const int SampleCount = 500; // Reducido para hardware lento

        // R707: Static fields after constants (Singleton para evitar OpenFailed)
        private static IEngine _sharedEngine = null!;
        private static CancellationTokenSource _sharedCts = null!;
        private static Task _sharedCaptureTask = null!;
        private static Socket _sharedSocket = null!;
        private static readonly object _lock = new();
        private static bool _initialized;

        // R707: Instance fields after static fields
        private byte[] _packet = null!;

        [GlobalSetup]
        public void Setup()
        {
            lock (_lock)
            {
                // Solo inicializar UNA VEZ por toda la sesión de benchmarks
                if (_initialized)
                {
                    // Ya existe, solo inicializar buffer local
                    _packet = new byte[512];
                    return;
                }

                _initialized = true;

                // Inicializar buffer local
                _packet = new byte[512];

                // Inicializar socket compartido
                _sharedSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _sharedSocket.Connect(IPAddress.Loopback, Port);

                // Inicializar Engine compartido
                var services = new ServiceCollection();
                services.AddNetShaperServices();
                var provider = services.BuildServiceProvider();

                _sharedEngine = provider.GetRequiredService<IEngine>();
                _sharedCts = new CancellationTokenSource();

                var result = _sharedEngine.Start($"outbound and udp.DstPort == {Port}", _sharedCts.Token);
                if (result != StartResult.Success)
                    throw new InvalidOperationException($"Start failed: {result}");

                _sharedCaptureTask = Task.Run(() => _sharedEngine.RunCaptureLoop(), _sharedCts.Token);

                // Warmup ligero
                for (int i = 0; i < 100; i++)
                {
                    _sharedSocket.Send(_packet);
                    Thread.Sleep(5); // Delay para no saturar
                }

                Thread.Sleep(500);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            lock (_lock)
            {
                if (!_initialized) return;

                _sharedEngine?.Stop();
                _sharedCts?.Cancel();

                try { _sharedCaptureTask?.Wait(2000); } catch { }

                _sharedEngine?.Dispose();
                _sharedCts?.Dispose();
                _sharedSocket?.Dispose();

                _sharedEngine = null!;
                _sharedCts = null!;
                _sharedCaptureTask = null!;
                _sharedSocket = null!;
                _initialized = false;
            }
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
                long baseline = _sharedEngine.PacketCount;
                long start = Stopwatch.GetTimestamp();

                _sharedSocket.Send(_packet);

                // Esperar hasta que el paquete sea procesado
                while (_sharedEngine.PacketCount == baseline)
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
                long baseline = _sharedEngine.PacketCount;
                long start = Stopwatch.GetTimestamp();

                // Esperar hasta el momento exacto de envío
                while (Stopwatch.GetTimestamp() < nextSend)
                    Thread.SpinWait(10);

                _sharedSocket.Send(_packet);
                nextSend += intervalTicks;

                // Esperar procesamiento
                while (_sharedEngine.PacketCount == baseline)
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