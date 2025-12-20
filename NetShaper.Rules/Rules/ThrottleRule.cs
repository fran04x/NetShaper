using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Throttle rule: rate-limit by packets per second (PPS).
    /// Uses token bucket algorithm where tokens = packets.
    /// 
    /// STATE FIELDS:
    ///   - TokenBucket   : current packet tokens
    ///   - TokensPerTick : packets per SECOND (name is legacy, not per-tick)
    ///   - MaxTokens     : bucket capacity = packetsPerSecond
    ///   - LastTick      : last refill timestamp
    ///   - Counter       : packets passed
    /// </summary>
    public static class ThrottleRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for Throttle rule.
        /// </summary>
        /// <param name="packetsPerSecond">Max packets per second.</param>
        public static RuleState CreateState(int packetsPerSecond)
        {
            var state = default(RuleState);
            state.MaxTokens = packetsPerSecond;
            state.TokenBucket = packetsPerSecond;  // Start full
            state.TokensPerTick = packetsPerSecond;
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
            
            // Check if we have tokens
            if (state.TokenBucket > 0)
            {
                state.TokenBucket--;
                state.Counter++;
                return ActionMask.None;  // Pass
            }
            
            // No tokens, drop
            return ActionMask.Drop;
        }
    }
}
