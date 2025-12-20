using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// LossPattern rule: drops packets by bitmask pattern.
    /// E.g., pattern 0b10 = drop every other packet.
    /// 
    /// STATE FIELDS:
    ///   - PatternMask   : drop bitmask (1 = drop)
    ///   - PatternIndex  : current position (0 to PatternLength-1)
    ///   - PatternLength : cycle length (1-64)
    ///   - Counter       : packets processed
    /// </summary>
    public static class LossPatternRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for LossPattern rule.
        /// </summary>
        /// <param name="pattern">Bitmask where 1 = drop. E.g., 0b1010 drops positions 1,3.</param>
        /// <param name="length">Pattern length in bits (1-64).</param>
        public static RuleState CreateState(ulong pattern, int length)
        {
            var state = default(RuleState);
            state.PatternMask = pattern;
            state.PatternLength = Math.Clamp(length, 1, 64);
            state.PatternIndex = 0;
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            int index = state.PatternIndex;
            bool shouldDrop = ((state.PatternMask >> index) & 1) == 1;
            
            // Advance pattern
            state.PatternIndex = (index + 1) % state.PatternLength;
            state.Counter++;
            
            return shouldDrop ? ActionMask.Drop : ActionMask.None;
        }
    }
}
