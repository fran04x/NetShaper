using System.Runtime.InteropServices;

namespace NetShaper.Abstractions
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PacketLogEntry
    {
        public readonly long Timestamp;
        public readonly LogLevel Level;
        public readonly LogCode Code;
        public readonly long Value;

        public PacketLogEntry(long timestamp, LogLevel level, LogCode code, long value)
        {
            Timestamp = timestamp;
            Level = level;
            Code = code;
            Value = value;
        }
    }

    public enum LogLevel : byte
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum LogCode : ushort
    {
        None = 0,
        EngineStarted = 1,
        EngineStopped = 2,
        PacketProcessed = 3,
        RecvFailed = 4,
        SendFailed = 5,
        InvalidPacket = 6,
        OperationAborted = 7,
        InvalidHandle = 8,
        InvalidParameter = 9
    }
}