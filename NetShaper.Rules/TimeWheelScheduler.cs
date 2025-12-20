using System.Buffers;
using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules
{
    /// <summary>
    /// Time-wheel scheduler for delayed packet sending.
    /// Ring buffer with power-of-2 size, fixed tick granularity.
    /// </summary>
    public sealed class TimeWheelScheduler : IDisposable
    {
        private const int WheelSize = 1024;  // Power of 2
        private const int WheelMask = WheelSize - 1;
        private const long TicksPerSlot = 10000;  // ~1ms at 10MHz Stopwatch
        
        private readonly Slot[] _wheel;
        private readonly IPacketCapture _capture;
        private long _currentTick;
        private int _disposed;
        
        /// <summary>
        /// Delayed packet entry in the wheel.
        /// </summary>
        private struct DelayedPacket
        {
            public byte[] Buffer;      // Rented from ArrayPool
            public int Length;
            public PacketMetadata Metadata;
            public long TargetTick;
        }
        
        /// <summary>
        /// Slot in the time wheel (linked list of delayed packets).
        /// </summary>
        private struct Slot
        {
            public DelayedPacket[] Packets;
            public int Count;
            public int Capacity;
        }
        
        public TimeWheelScheduler(IPacketCapture capture)
        {
            ArgumentNullException.ThrowIfNull(capture);
            _capture = capture;
            _wheel = new Slot[WheelSize];
            
            // Pre-allocate slots
            for (int i = 0; i < WheelSize; i++)
            {
                _wheel[i].Capacity = 16;
                _wheel[i].Packets = new DelayedPacket[16];
                _wheel[i].Count = 0;
            }
            
            _currentTick = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        
        /// <summary>
        /// Enqueues a packet for delayed sending.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(ReadOnlySpan<byte> packet, ref PacketMetadata metadata, long delayTicks)
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;
            
            long targetTick = System.Diagnostics.Stopwatch.GetTimestamp() + delayTicks;
            int slotIndex = (int)((targetTick / TicksPerSlot) & WheelMask);
            
            ref Slot slot = ref _wheel[slotIndex];
            
            // Grow if needed
            if (slot.Count >= slot.Capacity)
            {
                int newCapacity = slot.Capacity * 2;
                var newPackets = new DelayedPacket[newCapacity];
                Array.Copy(slot.Packets, newPackets, slot.Count);
                slot.Packets = newPackets;
                slot.Capacity = newCapacity;
            }
            
            // Rent buffer and copy packet
            ref DelayedPacket entry = ref slot.Packets[slot.Count];
            entry.Buffer = ArrayPool<byte>.Shared.Rent(packet.Length);
            packet.CopyTo(entry.Buffer.AsSpan());
            entry.Length = packet.Length;
            entry.Metadata = metadata;
            entry.TargetTick = targetTick;
            
            slot.Count++;
        }
        
        /// <summary>
        /// Processes all due packets. Call this periodically.
        /// </summary>
        public void Tick()
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;
            
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            int currentSlot = (int)((now / TicksPerSlot) & WheelMask);
            
            ref Slot slot = ref _wheel[currentSlot];
            
            for (int i = slot.Count - 1; i >= 0; i--)
            {
                ref DelayedPacket entry = ref slot.Packets[i];
                
                if (entry.TargetTick <= now)
                {
                    // Send packet
                    var buffer = entry.Buffer.AsSpan(0, entry.Length);
                    _capture.Send(buffer, ref entry.Metadata);
                    
                    // Return buffer to pool
                    ArrayPool<byte>.Shared.Return(entry.Buffer);
                    
                    // Remove by swap with last
                    slot.Packets[i] = slot.Packets[slot.Count - 1];
                    slot.Count--;
                }
            }
            
            _currentTick = now;
        }
        
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                // Return all rented buffers
                for (int s = 0; s < WheelSize; s++)
                {
                    ref Slot slot = ref _wheel[s];
                    for (int i = 0; i < slot.Count; i++)
                    {
                        if (slot.Packets[i].Buffer != null)
                            ArrayPool<byte>.Shared.Return(slot.Packets[i].Buffer);
                    }
                    slot.Count = 0;
                }
            }
        }
    }
}
