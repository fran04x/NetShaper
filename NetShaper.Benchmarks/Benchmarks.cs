// NetShaper.Benchmarks/Benchmarks.cs
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
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

    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class EngineBenchmarks
    {
        private const int Port = 55556;
        private const int PacketCount = 10000;

        private Engine.Engine _engine = null!;
        private CancellationTokenSource _cts = null!;
        private Task _captureTask = null!;
        private Socket _socket = null!;
        private IPacketLogger _logger = null!;
        private IPacketCapture _capture = null!;

        private byte[] _small = null!;
        private byte[] _medium = null!;
        private byte[] _large = null!;

        [GlobalSetup]
        public void Setup()
        {
            _small = new byte[64];
            _medium = new byte[512];
            _large = new byte[1400];

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Connect(IPAddress.Loopback, Port);

            var services = new ServiceCollection();
            services.AddNetShaperServices();
            var provider = services.BuildServiceProvider();

            _logger = provider.GetRequiredService<IPacketLogger>();
            _capture = provider.GetRequiredService<IPacketCapture>();

            Func<IPacketCapture> captureFactory = () => _capture;
            _engine = new Engine.Engine(_logger, captureFactory, threadCount: 1);
            _cts = new CancellationTokenSource();

            var result = _engine.Start($"outbound and udp.DstPort == {Port}", _cts.Token);
            if (result != StartResult.Success)
                throw new InvalidOperationException(result.ToString());

            _captureTask = Task.Factory.StartNew(
                static state =>
                {
                    var engine = (Engine.Engine)state!;
                    engine.RunCaptureLoop();
                },
                _engine,
                TaskCreationOptions.LongRunning);

            Thread.Sleep(200);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _engine.Stop();
            _cts.Cancel();

            try { _captureTask.Wait(1000); } catch { }

            _engine.Dispose();
            _cts.Dispose();
            _socket.Dispose();
            _capture.Dispose();
        }

        [Benchmark(Description = "10k packets - 64 bytes")]
        public void SmallPackets()
        {
            for (int i = 0; i < PacketCount; i++)
            {
                _socket.Send(_small);
                if ((i & 255) == 0) Thread.Yield();
            }
        }

        [Benchmark(Description = "10k packets - 512 bytes")]
        public void MediumPackets()
        {
            for (int i = 0; i < PacketCount; i++)
            {
                _socket.Send(_medium);
                if ((i & 255) == 0) Thread.Yield();
            }
        }

        [Benchmark(Description = "10k packets - 1400 bytes")]
        public void LargePackets()
        {
            for (int i = 0; i < PacketCount; i++)
            {
                _socket.Send(_large);
                if ((i & 255) == 0) Thread.Yield();
            }
        }
    }
}
