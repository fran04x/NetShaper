// NetShaper.Benchmarks/Benchmarks.cs
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Microsoft.Extensions.DependencyInjection;
using NetShaper.Abstractions;
using NetShaper.Composition;
using NetShaper.Engine;

namespace NetShaper.Benchmarks
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Run both throughput and latency/jitter benchmarks
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }

    public class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            // Usamos ShortRun para pruebas rápidas
            // Y aumentamos el timeout a 10 minutos para evitar la excepción "takes too long"
            AddJob(Job.ShortRun
                .WithToolchain(new InProcessNoEmitToolchain(
                    timeout: TimeSpan.FromMinutes(10), 
                    logOutput: true // Para ver si avanza
                ))); 
        }
    }

    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [Config(typeof(InProcessConfig))]
    public class EngineBenchmarks
    {
        // R707: Constants first
        private const int Port = 55556;
        private const int PacketCount = 10000;

        // R707: Static fields after constants (Singleton para evitar OpenFailed)
        private static Engine.Engine _sharedEngine = null!;
        private static CancellationTokenSource _sharedCts = null!;
        private static Task _sharedCaptureTask = null!;
        private static Socket _sharedSocket = null!;
        private static IPacketLogger _sharedLogger = null!;
        private static IPacketCapture _sharedCapture = null!;
        private static readonly object _lock = new();
        private static bool _initialized;

        // R707: Instance fields after static fields
        private byte[] _small = null!;
        private byte[] _medium = null!;
        private byte[] _large = null!;

        [GlobalSetup]
        public void Setup()
        {
            lock (_lock)
            {
                // Solo inicializar UNA VEZ por toda la sesión de benchmarks
                if (_initialized) 
                {
                    // Ya existe, solo inicializar buffers locales
                    _small = new byte[64];
                    _medium = new byte[512];
                    _large = new byte[1400];
                    return;
                }

                _initialized = true;

                // Inicializar buffers locales
                _small = new byte[64];
                _medium = new byte[512];
                _large = new byte[1400];

                // Inicializar socket compartido
                _sharedSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _sharedSocket.Connect(IPAddress.Loopback, Port);

                // Inicializar Engine compartido
                var services = new ServiceCollection();
                services.AddNetShaperServices();
                var provider = services.BuildServiceProvider();

                _sharedLogger = provider.GetRequiredService<IPacketLogger>();
                _sharedCapture = provider.GetRequiredService<IPacketCapture>();

                Func<IPacketCapture> captureFactory = () => _sharedCapture;
                _sharedEngine = new Engine.Engine(_sharedLogger, captureFactory, threadCount: 1);
                _sharedCts = new CancellationTokenSource();

                var result = _sharedEngine.Start($"outbound and udp.DstPort == {Port}", _sharedCts.Token);
                if (result != StartResult.Success)
                    throw new InvalidOperationException($"Start failed: {result}");

                _sharedCaptureTask = Task.Factory.StartNew(
                    static state =>
                    {
                        var engine = (Engine.Engine)state!;
                        engine.RunCaptureLoop();
                    },
                    _sharedEngine,
                    TaskCreationOptions.LongRunning);

                Thread.Sleep(200);
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

                try { _sharedCaptureTask?.Wait(1000); } catch { }

                _sharedEngine?.Dispose();
                _sharedCts?.Dispose();
                _sharedSocket?.Dispose();
                _sharedCapture?.Dispose();

                _sharedEngine = null!;
                _sharedCts = null!;
                _sharedCaptureTask = null!;
                _sharedSocket = null!;
                _sharedLogger = null!;
                _sharedCapture = null!;
                _initialized = false;
            }
        }

        [Benchmark(Description = "10k packets - 64 bytes")]
        public void SmallPackets()
        {
            for (int i = 0; i < PacketCount; i++)
            {
                _sharedSocket.Send(_small);
                if ((i & 255) == 0) Thread.Yield();
            }
        }

        [Benchmark(Description = "10k packets - 512 bytes")]
        public void MediumPackets()
        {
            for (int i = 0; i < PacketCount; i++)
            {
                _sharedSocket.Send(_medium);
                if ((i & 255) == 0) Thread.Yield();
            }
        }

        [Benchmark(Description = "10k packets - 1400 bytes")]
        public void LargePackets()
        {
            for (int i = 0; i < PacketCount; i++)
            {
                _sharedSocket.Send(_large);
                if ((i & 255) == 0) Thread.Yield();
            }
        }
    }
}
