using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// SynDrop rule: drops TCP SYN packets (blocks new connections).
    /// Short-circuits pipeline.
    /// 
    /// STATE FIELDS:
    ///   - Counter : SYN packets dropped (only common field used)
    /// </summary>
    public static class SynDropRule
    {
        private const int IPv4HeaderOffset = 0;
        private const int TcpFlagsOffset = 13;  // Relative to TCP header
        private const byte SynFlag = 0x02;
        private const byte AckFlag = 0x10;
        
        public static RuleFunc Create() => Evaluate;
        
        public static RuleState CreateState() => default;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            // Minimum: IP header (20) + TCP header (20) + flags
            if (packet.Length < 40)
                return ActionMask.None;
            
            // Check if TCP (protocol = 6 at offset 9 for IPv4)
            byte protocol = packet[9];
            if (protocol != 6)
                return ActionMask.None;
            
            // Get IP header length
            int ipHeaderLen = (packet[0] & 0x0F) * 4;
            if (packet.Length < ipHeaderLen + 20)
                return ActionMask.None;
            
            // Get TCP flags
            byte tcpFlags = packet[ipHeaderLen + TcpFlagsOffset];
            
            // Check for SYN without ACK (new connection)
            bool isSyn = (tcpFlags & SynFlag) != 0;
            bool isAck = (tcpFlags & AckFlag) != 0;
            
            if (isSyn && !isAck)
            {
                state.Counter++;
                return ActionMask.Drop;
            }
            
            return ActionMask.None;
        }
    }
}
