// NetShaper.Engine/EngineTelemetry.cs
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetShaper.Engine
{
    internal sealed class EngineTelemetry
    {
        private long _packetsProcessed;
        private long _recvErrors;
        private long _sendErrors;
        private long _invalidPackets;
        private long _consecutiveErrors;

        // Propiedades con Volatile.Read para lecturas cross-thread seguras
        public long PacketsProcessed => Volatile.Read(ref _packetsProcessed);
        public long RecvErrors => Volatile.Read(ref _recvErrors);
        public long SendErrors => Volatile.Read(ref _sendErrors);
        public long InvalidPackets => Volatile.Read(ref _invalidPackets);
        public long ConsecutiveErrors => Volatile.Read(ref _consecutiveErrors);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordPacket()
        {
            Interlocked.Increment(ref _packetsProcessed);
            Interlocked.Exchange(ref _consecutiveErrors, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordRecvError()
        {
            Interlocked.Increment(ref _recvErrors);
            Interlocked.Increment(ref _consecutiveErrors);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordSendError()
        {
            Interlocked.Increment(ref _sendErrors);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordInvalidPacket()
        {
            Interlocked.Increment(ref _invalidPackets);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _packetsProcessed, 0);
            Interlocked.Exchange(ref _recvErrors, 0);
            Interlocked.Exchange(ref _sendErrors, 0);
            Interlocked.Exchange(ref _invalidPackets, 0);
            Interlocked.Exchange(ref _consecutiveErrors, 0);
        }
    }
}