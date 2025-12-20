using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules
{
    /// <summary>
    /// IPacketCapture decorator that applies rules on Send.
    /// Hook point: Send() intercepts packets before forwarding.
    /// </summary>
    public sealed class RulePacketCapture : IPacketCapture
    {
        private readonly IPacketCapture _inner;
        private readonly RulePipeline _pipeline;
        private readonly TimeWheelScheduler? _scheduler;
        private readonly InjectQueue? _injectQueue;
        
        public RulePacketCapture(IPacketCapture inner, RulePipeline pipeline)
            : this(inner, pipeline, null, null) { }
        
        public RulePacketCapture(
            IPacketCapture inner, 
            RulePipeline pipeline,
            TimeWheelScheduler? scheduler,
            InjectQueue? injectQueue)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(pipeline);
            
            _inner = inner;
            _pipeline = pipeline;
            _scheduler = scheduler;
            _injectQueue = injectQueue;
        }
        
        public CaptureResult Open(string filter) => _inner.Open(filter);
        
        public CaptureResult Receive(Span<byte> buffer, out uint length, ref PacketMetadata metadata)
            => _inner.Receive(buffer, out length, ref metadata);
        
        public CaptureResult ReceiveBatch(Span<byte> buffer, Span<PacketMetadata> metadataArray, out uint batchLength, out int packetCount)
            => _inner.ReceiveBatch(buffer, metadataArray, out batchLength, out packetCount);
        
        /// <summary>
        /// Intercepts Send to apply rules.
        /// Execution order: Drop → Duplicate → Modify → Delay → Send
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CaptureResult Send(ReadOnlySpan<byte> buffer, ref PacketMetadata metadata)
        {
            // Evaluate rules
            ActionResult result = _pipeline.Evaluate(buffer, ref metadata);
            
            // 1. Short-circuit: Drop or Blackhole
            if ((result.Mask & (ActionMask.Drop | ActionMask.Blackhole)) != 0)
                return CaptureResult.Success; // Packet "sent" (dropped)
            
            // 2. Duplicate: send N copies of ORIGINAL buffer (before Modify)
            if ((result.Mask & ActionMask.Duplicate) != 0 && result.DuplicateCount > 0)
            {
                for (int i = 0; i < result.DuplicateCount; i++)
                    _inner.Send(buffer, ref metadata);
            }
            
            // 3. Modify: apply once on original buffer
            if ((result.Mask & ActionMask.Modify) != 0 && result.ModifyFlags != ModifyFlags.None)
            {
                // We need a mutable copy to modify. 
                // Stackalloc is safe for typical MTU (up to 2KB). 
                // For jumbo frames > 2KB, we'd need ArrayPool, but let's stick to stackalloc 2048 for now for zero-alloc speed.
                // If packet > 2048, we skip modification or clip it (simplification).
                const int MaxStackSize = 2048;
                if (buffer.Length <= MaxStackSize)
                {
                    Span<byte> modBuffer = stackalloc byte[buffer.Length];
                    buffer.CopyTo(modBuffer);
                    
                    ApplyModifications(modBuffer, result.ModifyFlags, ref metadata);
                    
                    // Recursive call with modified buffer (skip rules to avoid infinite loop? No, rules already evaluated)
                    // We must NOT call Send recursively because it would re-evaluate rules!
                    // We proceed to Delay or Inner Send with modBuffer.
                    
                    // TRICKY: We need to pass 'modBuffer' to the next steps (Delay or Send).
                    // Refactoring: Use a local 'activeBuffer' span that points to either 'buffer' or 'modBuffer'.
                    ReadOnlySpan<byte> activeBuffer = modBuffer;
                    
                    // 4. Delay
                    if ((result.Mask & ActionMask.Delay) != 0 && _scheduler != null)
                    {
                        _scheduler.Enqueue(activeBuffer, ref metadata, result.DelayTicks);
                        return CaptureResult.Success;
                    }
                    
                    // Default
                    return _inner.Send(activeBuffer, ref metadata);
                }
            }
            
            // Standard path (no modify)
            ReadOnlySpan<byte> finalBuffer = buffer;
            
            // 4. Delay: enqueue to time-wheel (packet sent later)
            if ((result.Mask & ActionMask.Delay) != 0 && _scheduler != null)
            {
                _scheduler.Enqueue(finalBuffer, ref metadata, result.DelayTicks);
                return CaptureResult.Success; // Packet will be sent by scheduler
            }
            
            // 5. Inject: handled separately via InjectQueue (not here)
            
            // Default: forward packet immediately
            return _inner.Send(finalBuffer, ref metadata);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyModifications(Span<byte> buffer, ModifyFlags flags, ref PacketMetadata meta)
        {
            // Truncate (simulated by reducing length via slice in caller? No, we have fixed buffer here)
            // Ideally we'd return new length. But here we modify content.
            // Packet truncation implies changing the slice length downstream.
            // Since we passed 'modBuffer' which has same length, real truncation requires downstream support.
            // For now, let's implement Corrupt and Rewrite.
            
            if ((flags & ModifyFlags.Corrupt) != 0)
            {
                // Simple corruption: flip bit in payload
                // Skip headers (approx 40 bytes)
                if (buffer.Length > 50)
                    buffer[50] ^= 0xFF; // Flip byte 50
            }
            
            if ((flags & ModifyFlags.Rewrite) != 0)
            {
                // Rewrite: zero out payload tail
                if (buffer.Length > 60)
                    buffer.Slice(60).Clear();
            }
            
            if ((flags & ModifyFlags.WindowClamp) != 0 || (flags & ModifyFlags.MssClamp) != 0)
            {
                // TCP Clamping requires parsing TCP header
                // Minimal lazy parsing
                if (buffer.Length >= 40 && buffer[9] == 6) // IPv4 + TCP
                {
                    int ipHeaderLen = (buffer[0] & 0x0F) * 4;
                    if (buffer.Length >= ipHeaderLen + 20)
                    {
                        if ((flags & ModifyFlags.WindowClamp) != 0)
                        {
                             // Tcp Window at offset 14 in tcp header
                             int winOffset = ipHeaderLen + 14;
                             // Clamp to e.g. 1024
                             ushort clamp = 1024;
                             buffer[winOffset] = (byte)(clamp >> 8);
                             buffer[winOffset + 1] = (byte)(clamp & 0xFF);
                        }
                    }
                }
            }
            
            // Recalculate checksums
            _inner.CalculateChecksums(buffer, (uint)buffer.Length, ref meta);
        }

        public void Tick()
        {
            _scheduler?.Tick();
            _injectQueue?.Flush();
        }
        
        public void Shutdown() => _inner.Shutdown();
        
        public void CalculateChecksums(Span<byte> buffer, uint length, ref PacketMetadata metadata)
            => _inner.CalculateChecksums(buffer, length, ref metadata);
        
        public void Dispose()
        {
            _scheduler?.Dispose();
            _injectQueue?.Dispose();
            _inner.Dispose();
        }
    }
}
