using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules.Rules
{
    /// <summary>
    /// Drop rule: drops all matching packets.
    /// Short-circuits pipeline.
    /// 
    /// STATE FIELDS:
    ///   - Counter : packets dropped (only common field used)
    /// </summary>
    public static class DropRule
    {
        /// <summary>
        /// Creates a Drop rule function.
        /// </summary>
        /// <returns>Rule delegate.</returns>
        public static RuleFunc Create() => Evaluate;
        
        /// <summary>
        /// Initializes state for Drop rule.
        /// </summary>
        public static RuleState CreateState() => default;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionMask Evaluate(
            ReadOnlySpan<byte> packet,
            ref PacketMetadata meta,
            ref RuleState state,
            ref ActionResult result)
        {
            // Unconditional drop
            state.Counter++;
            return ActionMask.Drop;
        }
    }
}
