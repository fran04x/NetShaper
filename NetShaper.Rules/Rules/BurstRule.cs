using System.Diagnostics;
using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Burst rule: releases packets in periodic windows.
    /// SEMANTIC: "periodic gate" - does NOT accumulate packets,
    /// instead delays them until the next burst window opens.
    /// 
    /// STATE FIELDS:
    ///   - FixedDelayTicks : burst interval in ticks
    ///   - LastTick        : last window open timestamp
    ///   - Counter         : packets passed immediately
    /// </summary>
    public static class BurstRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for Burst rule.
        /// </summary>
        /// <param name="burstIntervalMs">Interval between bursts in ms.</param>
        public static RuleState CreateState(int burstIntervalMs)
        {
            var state = default(RuleState);
            long frequency = Stopwatch.Frequency;
            state.FixedDelayTicks = burstIntervalMs * frequency / 1000;
            state.LastTick = Stopwatch.GetTimestamp();
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            long now = Stopwatch.GetTimestamp();
            long elapsed = now - state.LastTick;
            
            // If burst interval elapsed, reset timer and pass immediately
            if (elapsed >= state.FixedDelayTicks)
            {
                state.LastTick = now;
                state.Counter++;
                return ActionMask.None;
            }
            
            // Otherwise delay until next burst window
            long remainingDelay = state.FixedDelayTicks - elapsed;
            if (remainingDelay > result.DelayTicks)
                result.DelayTicks = remainingDelay;
            
            return ActionMask.Delay;
        }
    }
}
