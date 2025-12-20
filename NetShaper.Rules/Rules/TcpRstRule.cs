using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// TcpRst rule: injects TCP RST packets.
    /// Sets Inject flag and prepares RST packet ID.
    /// 
    /// EXPERIMENTAL: Currently flow-agnostic (no per-flow state).
    /// InjectPacketId = 0 is placeholder; actual RST construction external.
    /// 
    /// STATE FIELDS (FRAGILE OVERLAY):
    ///   - PatternIndex : mode (0=continuous, 1=one-shot, 2=fired)
    ///   - Counter      : RST injections triggered
    /// </summary>
    public static class TcpRstRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for TcpRst rule.
        /// </summary>
        /// <param name="oneShot">If true, only inject once then disable.</param>
        public static RuleState CreateState(bool oneShot)
        {
            var state = default(RuleState);
            state.PatternIndex = oneShot ? 1 : 0;  // 1 = one-shot mode
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            // Check if already fired (one-shot mode)
            if (state.PatternIndex == 2)  // 2 = already fired
                return ActionMask.None;
            
            // Minimum: IP header (20) + TCP header (20)
            if (packet.Length < 40)
                return ActionMask.None;
            
            // Check if TCP
            byte protocol = packet[9];
            if (protocol != 6)
                return ActionMask.None;
            
            // Note: Actual RST packet construction happens in InjectQueue
            // Here we just signal that injection is needed
            // The inject packet ID would be set by a packet builder (TODO)
            result.InjectPacketId = 0;  // Placeholder, actual RST building is external
            
            state.Counter++;
            
            // If one-shot, mark as fired
            if (state.PatternIndex == 1)
                state.PatternIndex = 2;
            
            return ActionMask.Inject;
        }
    }
}
