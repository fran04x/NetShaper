using System;
using System.Threading;
using NetShaper.Abstractions;

namespace NetShaper.Infrastructure
{
    public sealed class RingBufferPacketLogger : IPacketLogger
    {
        private readonly PacketLogEntry[] _buffer;
        private readonly int _mask;
        private int _index;

        public RingBufferPacketLogger(int powerOfTwoSize = 4096)
        {
            if (powerOfTwoSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(powerOfTwoSize), powerOfTwoSize, "Size must be positive");

            if ((powerOfTwoSize & (powerOfTwoSize - 1)) != 0)
                throw new ArgumentException($"Size must be a power of two. Provided: {powerOfTwoSize}", nameof(powerOfTwoSize));

            _buffer = new PacketLogEntry[powerOfTwoSize];
            _mask = powerOfTwoSize - 1;
            _index = -1;
        }

        public void Log(in PacketLogEntry entry)
        {
            int i = Interlocked.Increment(ref _index) & _mask;
            _buffer[i] = entry;
        }

        public PacketLogEntry[] Snapshot()
        {
            return (PacketLogEntry[])_buffer.Clone();
        }
    }
}