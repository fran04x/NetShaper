using System.Diagnostics;
using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Lag rule: adds fixed delay to packets.
    /// Does NOT short-circuit.
    /// 
    /// STATE FIELDS:
    ///   - FixedDelayTicks : fixed delay in ticks
    ///   - Counter         : packets delayed
    /// </summary>
    public static class LagRule
    {
        /// <summary>
        /// Creates a Lag rule function.
        /// </summary>
        /// <returns>Rule delegate.</returns>
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Initializes state for Lag rule with specified delay.
        /// </summary>
        /// <param name="delayMs">Delay in milliseconds.</param>
        public static RuleState CreateState(int delayMs)
        {
            var state = default(RuleState);
            // Convert ms to ticks: ticks = ms * Frequency / 1000
            state.FixedDelayTicks = (long)(delayMs * Stopwatch.Frequency / 1000.0);
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            state.Counter++;
            
            // Accumulate delay (max)
            if (state.FixedDelayTicks > result.DelayTicks)
                result.DelayTicks = state.FixedDelayTicks;
            
            return ActionMask.Delay;
        }
    }
}
