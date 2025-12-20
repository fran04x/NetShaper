using System.Buffers;
using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules
{
    /// <summary>
    /// One-shot inject queue for TCP RST and similar injections.
    /// Does NOT re-evaluate rules on injected packets.
    /// </summary>
    public sealed class InjectQueue : IDisposable
    {
        private const int MaxQueueSize = 64;
        
        private readonly IPacketCapture _capture;
        private readonly InjectEntry[] _queue;
        private int _head;
        private int _tail;
        private int _count;
        private int _disposed;
        
        private struct InjectEntry
        {
            public byte[] Buffer;
            public int Length;
            public PacketMetadata Metadata;
        }
        
        public InjectQueue(IPacketCapture capture)
        {
            ArgumentNullException.ThrowIfNull(capture);
            _capture = capture;
            _queue = new InjectEntry[MaxQueueSize];
        }
        
        /// <summary>
        /// Enqueues a packet for one-shot injection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Enqueue(ReadOnlySpan<byte> packet, ref PacketMetadata metadata)
        {
            if (Volatile.Read(ref _disposed) == 1)
                return false;
            
            if (_count >= MaxQueueSize)
                return false;  // Queue full, drop
            
            ref InjectEntry entry = ref _queue[_tail];
            entry.Buffer = ArrayPool<byte>.Shared.Rent(packet.Length);
            packet.CopyTo(entry.Buffer.AsSpan());
            entry.Length = packet.Length;
            entry.Metadata = metadata;
            
            _tail = (_tail + 1) % MaxQueueSize;
            _count++;
            
            return true;
        }
        
        /// <summary>
        /// Sends all queued packets. Call after ProcessBatch.
        /// </summary>
        public void Flush()
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;
            
            while (_count > 0)
            {
                ref InjectEntry entry = ref _queue[_head];
                
                var buffer = entry.Buffer.AsSpan(0, entry.Length);
                _capture.Send(buffer, ref entry.Metadata);
                
                ArrayPool<byte>.Shared.Return(entry.Buffer);
                entry.Buffer = null!;
                
                _head = (_head + 1) % MaxQueueSize;
                _count--;
            }
        }
        
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                // Return all rented buffers
                while (_count > 0)
                {
                    ref InjectEntry entry = ref _queue[_head];
                    if (entry.Buffer != null)
                        ArrayPool<byte>.Shared.Return(entry.Buffer);
                    _head = (_head + 1) % MaxQueueSize;
                    _count--;
                }
            }
        }
    }
}
