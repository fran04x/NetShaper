using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Tamper rule: corrupts/truncates/rewrites packet payload.
    /// Sets appropriate ModifyFlags in result.
    /// 
    /// STATE FIELDS (FRAGILE OVERLAY):
    ///   - PatternIndex : stores ModifyFlags cast to int
    ///   - Counter      : packets modified
    /// 
    /// WARNING: Reuses PatternIndex from LossPattern overlay.
    /// If adding more tamper modes, consider dedicated field.
    /// </summary>
    public static class TamperRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for Tamper rule.
        /// </summary>
        /// <param name="flags">Which tampering operations to apply.</param>
        public static RuleState CreateState(ModifyFlags flags)
        {
            var state = default(RuleState);
            // Store flags in pattern index (reusing field)
            state.PatternIndex = (int)flags;
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            ModifyFlags flags = (ModifyFlags)state.PatternIndex;
            
            if (flags == ModifyFlags.None)
                return ActionMask.None;
            
            result.ModifyFlags |= flags;
            state.Counter++;
            return ActionMask.Modify;
        }
    }
}
