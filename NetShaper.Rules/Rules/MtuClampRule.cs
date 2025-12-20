using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// MtuClamp rule: enforces maximum packet size.
    /// Sets Truncate modify flag for oversized packets.
    /// 
    /// STATE FIELDS (overlay reuse):
    ///   - MaxTokens : max packet size (reuses token bucket field)
    ///   - Counter   : packets truncated
    /// </summary>
    public static class MtuClampRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for MtuClamp rule.
        /// </summary>
        /// <param name="maxPacketSize">Maximum packet size in bytes.</param>
        public static RuleState CreateState(int maxPacketSize)
        {
            var state = default(RuleState);
            state.MaxTokens = maxPacketSize;  // Reusing MaxTokens field for MTU
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            if (packet.Length <= state.MaxTokens)
                return ActionMask.None;  // No modification needed
            
            // Packet exceeds MTU, set truncate flag
            result.ModifyFlags |= ModifyFlags.Truncate;
            state.Counter++;
            return ActionMask.Modify;
        }
    }
}
