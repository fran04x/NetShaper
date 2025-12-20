using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Blackhole rule: silent drop (no response/RST).
    /// Short-circuits pipeline.
    /// 
    /// STATE FIELDS:
    ///   - Counter : packets blackholed (only common field used)
    /// </summary>
    public static class BlackholeRule
    {
        public static RuleFunc Create() => Evaluate;
        
        public static RuleState CreateState() => default;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            state.Counter++;
            return ActionMask.Blackhole;
        }
    }
}
