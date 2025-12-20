using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Bandwidth rule: rate-limit by bytes per second (BPS).
    /// Uses token bucket algorithm where tokens = bytes.
    /// 
    /// STATE FIELDS:
    ///   - TokenBucket   : current byte tokens
    ///   - TokensPerTick : bytes per SECOND (name is legacy, not per-tick)
    ///   - MaxTokens     : bucket capacity = bytesPerSecond
    ///   - LastTick      : last refill timestamp
    ///   - Counter       : packets passed
    /// </summary>
    public static class BandwidthRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for Bandwidth rule.
        /// </summary>
        /// <param name="bytesPerSecond">Max bytes per second.</param>
        public static RuleState CreateState(int bytesPerSecond)
        {
            var state = default(RuleState);
            state.MaxTokens = bytesPerSecond;
            state.TokenBucket = bytesPerSecond;  // Start full
            state.TokensPerTick = bytesPerSecond;
            state.LastTick = System.Diagnostics.Stopwatch.GetTimestamp();
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            long elapsed = now - state.LastTick;
            long frequency = System.Diagnostics.Stopwatch.Frequency;
            
            // Refill tokens based on elapsed time
            int tokensToAdd = (int)(elapsed * state.TokensPerTick / frequency);
            if (tokensToAdd > 0)
            {
                state.TokenBucket = Math.Min(state.TokenBucket + tokensToAdd, state.MaxTokens);
                state.LastTick = now;
            }
            
            int packetSize = packet.Length;
            
            // Check if we have enough tokens for this packet
            if (state.TokenBucket >= packetSize)
            {
                state.TokenBucket -= packetSize;
                state.Counter++;
                return ActionMask.None;  // Pass
            }
            
            // Not enough bandwidth, drop
            return ActionMask.Drop;
        }
    }
}
