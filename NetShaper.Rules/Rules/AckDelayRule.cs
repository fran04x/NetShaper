using System.Diagnostics;
using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// AckDelay rule: delays ACK packets specifically.
    /// Useful for simulating slow ACK receiver.
    /// 
    /// NOTE: Applies to ALL packets with ACK flag set,
    /// including ACK+DATA (piggybacked ACKs).
    /// For pure ACKs only: would need payload length check.
    /// 
    /// STATE FIELDS:
    ///   - FixedDelayTicks : ACK delay in ticks
    ///   - Counter         : ACKs delayed
    /// </summary>
    public static class AckDelayRule
    {
        private const byte AckFlag = 0x10;
        
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Creates state for AckDelay rule.
        /// </summary>
        /// <param name="delayMs">ACK delay in ms.</param>
        public static RuleState CreateState(int delayMs)
        {
            var state = default(RuleState);
            state.FixedDelayTicks = delayMs * Stopwatch.Frequency / 1000;
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
            if (packet.Length < ipHeaderLen + 14)
                return ActionMask.None;
            
            // Get TCP flags
            byte tcpFlags = packet[ipHeaderLen + 13];
            
            // Check for ACK flag
            if ((tcpFlags & AckFlag) == 0)
                return ActionMask.None;
            
            // Delay ACK
            if (state.FixedDelayTicks > result.DelayTicks)
                result.DelayTicks = state.FixedDelayTicks;
            
            state.Counter++;
            return ActionMask.Delay;
        }
    }
}
