using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Duplicate rule: send packet N times.
    /// Does NOT short-circuit.
    /// 
    /// STATE FIELDS:
    ///   - DuplicateCount : number of extra copies
    ///   - Counter        : packets duplicated
    /// </summary>
    public static class DuplicateRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for Duplicate rule.
        /// </summary>
        /// <param name="count">Number of duplicates (0 = no extra copies).</param>
        public static RuleState CreateState(int count)
        {
            var state = default(RuleState);
            state.DuplicateCount = count;
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            if (state.DuplicateCount <= 0)
                return ActionMask.None;
            
            // Accumulate max duplicate count
            if (state.DuplicateCount > result.DuplicateCount)
                result.DuplicateCount = state.DuplicateCount;
            
            state.Counter++;
            return ActionMask.Duplicate;
        }
    }
}
