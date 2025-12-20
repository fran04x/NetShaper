using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// WindowClamp rule: limits TCP window size.
    /// Sets WindowClamp modify flag for oversized windows.
    /// 
    /// STATE FIELDS (overlay reuse):
    ///   - TokenBucket : max window size (reuses token bucket field)
    ///   - Counter     : windows clamped
    /// </summary>
    public static class WindowClampRule
    {
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for WindowClamp rule.
        /// </summary>
        /// <param name="maxWindowSize">Maximum window size in bytes.</param>
        public static RuleState CreateState(ushort maxWindowSize)
        {
            var state = default(RuleState);
            state.TokenBucket = maxWindowSize;  // Reusing for window size
            return state;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            // Minimum: IP header (20) + TCP header (20)
            if (packet.Length < 40)
                return ActionMask.None;
            
            // Check if TCP
            byte protocol = packet[9];
            if (protocol != 6)
                return ActionMask.None;
            
            // Get IP header length
            int ipHeaderLen = (packet[0] & 0x0F) * 4;
            if (packet.Length < ipHeaderLen + 16)
                return ActionMask.None;
            
            // TCP window is at offset 14-15 from TCP header
            int windowOffset = ipHeaderLen + 14;
            ushort currentWindow = (ushort)((packet[windowOffset] << 8) | packet[windowOffset + 1]);
            
            if (currentWindow <= (ushort)state.TokenBucket)
                return ActionMask.None;  // No modification needed
            
            // Window exceeds max, set clamp flag
            result.ModifyFlags |= ModifyFlags.WindowClamp;
            state.Counter++;
            return ActionMask.Modify;
        }
    }
}
