using System.Runtime.CompilerServices;
using NetShaper.Abstractions;

namespace NetShaper.Rules
{
    /// <summary>
    /// Rule pipeline evaluator. Atomic ruleset swap, linear evaluation.
    /// </summary>
    public sealed class RulePipeline
    {
        private Ruleset _ruleset = Ruleset.Empty;
        
        /// <summary>Current active ruleset.</summary>
        public Ruleset CurrentRuleset => Volatile.Read(ref _ruleset);
        
        /// <summary>
        /// Evaluates all rules against a packet.
        /// Short-circuits on Drop/Blackhole.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActionResult Evaluate(ReadOnlySpan<byte> packet, ref PacketMetadata meta)
        {
            var ruleset = Volatile.Read(ref _ruleset);
            
            // Early-exit: no rules active
            if (ruleset.Capabilities == RuleCapability.None)
                return ActionResult.Create();
            
            var result = ActionResult.Create();
            
            var rules = ruleset.Rules;
            var states = ruleset.States;
            
            for (int i = 0; i < rules.Length; i++)
            {
                ActionMask mask = rules[i](packet, ref meta, ref states[i], ref result);
                result.Mask |= mask;
                
                // Short-circuit only Drop/Blackhole
                if ((mask & (ActionMask.Drop | ActionMask.Blackhole)) != 0)
                    break;
            }
            
            return result;
        }
        
        /// <summary>
        /// Atomically swaps the ruleset.
        /// States must be pre-cloned in the new Ruleset.
        /// </summary>
        public void Swap(Ruleset newRuleset)
        {
            ArgumentNullException.ThrowIfNull(newRuleset);
            Interlocked.Exchange(ref _ruleset, newRuleset);
        }
    }
}
