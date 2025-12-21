using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace Tester
{
    sealed class Program
    {
        static void Main(string[] args)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            if (args.Length == 0)
            {
                Console.WriteLine("Uso: tester client <udp/tcp> <ip> <port> <pps>");
                return;
            }

            string mode = args[0].ToLowerInvariant();
            bool isUdp = args[1].ToLowerInvariant() == "udp";

            if (mode == "client")
            {
                IPAddress ip = IPAddress.Parse(args[2]);
                int port = int.Parse(args[3]);
                int pps = int.Parse(args[4]);

                RunClientAsync(isUdp, ip, port, pps);
                return;
            }

            Console.WriteLine("Modo no soportado");
        }

        static void RunClientAsync(bool isUdp, IPAddress ip, int port, int targetPps)
        {
            Console.Title = $"CLIENT ASYNC -> {ip}:{port} @ {targetPps} PPS";

            using var socket = new Socket(
                ip.AddressFamily,
                isUdp ? SocketType.Dgram : SocketType.Stream,
                isUdp ? ProtocolType.Udp : ProtocolType.Tcp);

            if (!isUdp)
                socket.Connect(ip, port);

            byte[] payload = Encoding.ASCII.GetBytes("NETSHAPER_ASYNC_STRESS_PAYLOAD");
            var endpoint = new IPEndPoint(ip, port);

            var args = new SocketAsyncEventArgs
            {
                RemoteEndPoint = endpoint
            };
            args.SetBuffer(payload, 0, payload.Length);

            long sent = 0;
            long pps = 0;
            long drops = 0;

            long freq = Stopwatch.Frequency;
            long ticksPerPacket = targetPps > 0 ? freq / targetPps : 0;

            var sw = Stopwatch.StartNew();
            long nextTick = 0;
            long lastReport = 0;

            args.Completed += (_, e) =>
            {
                if (e.SocketError != SocketError.Success)
                    Interlocked.Increment(ref drops);
            };

            Console.WriteLine("Iniciando envío async (IOCP)...");

            while (true)
            {
                long now = sw.ElapsedTicks;

                if (targetPps > 0 && now < nextTick)
                {
                    Thread.Sleep(1);
                    continue;
                }

                nextTick += ticksPerPacket;

                bool pending;
                try
                {
                    pending = socket.SendToAsync(args);
                }
                catch (SocketException)
                {
                    drops++;
                    continue;
                }

                if (!pending && args.SocketError != SocketError.Success)
                    drops++;

                sent++;
                pps++;

                if (now - lastReport >= freq)
                {
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}] TX: {pps} pps | Drops: {drops} | Total: {sent}");
                    pps = 0;
                    drops = 0;
                    lastReport = now;
                }
            }
        }
    }
}
