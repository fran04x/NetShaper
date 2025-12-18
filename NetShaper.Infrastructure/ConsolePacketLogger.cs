using System;
using System.Diagnostics;
using NetShaper.Abstractions;

namespace NetShaper.Infrastructure
{
    public sealed class ConsolePacketLogger : IPacketLogger
    {
        private static readonly double _tickFrequency = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        public void Log(in PacketLogEntry entry)
        {
            var ticks = (long)(entry.Timestamp * _tickFrequency);
            var elapsed = new TimeSpan(ticks);

            string levelStr = entry.Level switch
            {
                LogLevel.Info => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                _ => "UNKN"
            };

            string codeStr = entry.Code switch
            {
                LogCode.EngineStarted => "Engine Started",
                LogCode.EngineStopped => "Engine Stopped",
                LogCode.PacketProcessed => "Packet Processed",
                LogCode.RecvFailed => "Recv Failed",
                LogCode.SendFailed => "Send Failed",
                LogCode.InvalidPacket => "Invalid Packet",
                LogCode.OperationAborted => "Operation Aborted",
                LogCode.InvalidHandle => "Invalid Handle",
                LogCode.InvalidParameter => "Invalid Parameter",
                _ => "Unknown"
            };

            Console.WriteLine($"[{elapsed.TotalSeconds:F3}s] [{levelStr}] {codeStr} (Value: {entry.Value})");
        }
    }
}