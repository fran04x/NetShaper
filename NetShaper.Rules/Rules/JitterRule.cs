using System.Diagnostics;
using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Jitter rule: adds random delay variance (±ms).
    /// Uses xorshift RNG for O(1) random.
    /// 
    /// STATE FIELDS:
    ///   - RngState      : xorshift32 state
    ///   - MinDelayTicks : min delay in ticks
    ///   - MaxDelayTicks : max delay in ticks
    ///   - Counter       : packets jittered
    /// </summary>
    public static class JitterRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for Jitter rule.
        /// </summary>
        /// <param name="minDelayMs">Minimum delay in ms.</param>
        /// <param name="maxDelayMs">Maximum delay in ms.</param>
        /// <param name="seed">RNG seed (use unique per rule).</param>
        public static RuleState CreateState(int minDelayMs, int maxDelayMs, uint seed = 12345)
        {
            var state = default(RuleState);
            long frequency = Stopwatch.Frequency;
            state.MinDelayTicks = (int)(minDelayMs * frequency / 1000);
            state.MaxDelayTicks = (int)(maxDelayMs * frequency / 1000);
            state.RngState = seed == 0 ? 12345 : seed;
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            // xorshift32
            uint x = state.RngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state.RngState = x;
            
            // Random delay in range [min, max]
            int range = state.MaxDelayTicks - state.MinDelayTicks;
            long delay = state.MinDelayTicks + (int)(x % (uint)(range + 1));
            
            if (delay > result.DelayTicks)
                result.DelayTicks = delay;
            
            state.Counter++;
            return ActionMask.Delay;
        }
    }
}
