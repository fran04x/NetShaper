using System.Diagnostics;
using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// OutOfOrder rule: simulates packet reordering via random delay.
    /// SEMANTIC: conceptual reordering - applies 0 to maxDelay randomly,
    /// causing packets to arrive in different order than sent.
    /// Does NOT maintain a reorder buffer.
    /// 
    /// STATE FIELDS:
    ///   - RngState      : xorshift32 state
    ///   - MinDelayTicks : always 0
    ///   - MaxDelayTicks : max random delay
    ///   - Counter       : packets processed
    /// </summary>
    public static class OutOfOrderRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for OutOfOrder rule.
        /// </summary>
        /// <param name="maxDelayMs">Maximum reorder delay in ms.</param>
        /// <param name="seed">RNG seed.</param>
        public static RuleState CreateState(int maxDelayMs, uint seed = 54321)
        {
            var state = default(RuleState);
            long frequency = Stopwatch.Frequency;
            state.MaxDelayTicks = (int)(maxDelayMs * frequency / 1000);
            state.MinDelayTicks = 0;
            state.RngState = seed == 0 ? 54321 : seed;
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
            
            // Random delay from 0 to max
            long delay = (int)(x % (uint)(state.MaxDelayTicks + 1));
            
            if (delay > result.DelayTicks)
                result.DelayTicks = delay;
            
            state.Counter++;
            return delay > 0 ? ActionMask.Delay : ActionMask.None;
        }
    }
}
