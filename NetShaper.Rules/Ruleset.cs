namespace NetShaper.Rules
{
    /// <summary>
    /// Immutable ruleset. States are cloned ONLY on activate/deactivate, never in hot-path.
    /// </summary>
    public sealed class Ruleset
    {
        /// <summary>Empty ruleset (no rules, None capabilities).</summary>
        public static readonly Ruleset Empty = new(Array.Empty<RuleFunc>(), Array.Empty<RuleState>(), RuleCapability.None);
        
        /// <summary>Rule functions in evaluation order.</summary>
        public readonly RuleFunc[] Rules;
        
        /// <summary>Per-rule states, aligned by index.</summary>
        public readonly RuleState[] States;
        
        /// <summary>Pre-computed capabilities for early-exit.</summary>
        public readonly RuleCapability Capabilities;
        
        public Ruleset(RuleFunc[] rules, RuleState[] states, RuleCapability capabilities)
        {
            if (rules.Length != states.Length)
                throw new ArgumentException("Rules and States must have same length");
            
            Rules = rules;
            States = states;
            Capabilities = capabilities;
        }
        
        /// <summary>
        /// Creates a new Ruleset with cloned States array.
        /// Call this when activating/deactivating rules, not in hot-path.
        /// </summary>
        /// <remarks>
        /// Rules array is NOT cloned - rules are assumed immutable (delegate references).
        /// States array IS cloned to ensure snapshot isolation.
        /// </remarks>
        public static Ruleset Create(RuleFunc[] rules, RuleState[] states, RuleCapability capabilities)
        {
            var clonedStates = new RuleState[states.Length];
            Array.Copy(states, clonedStates, states.Length);
            return new Ruleset(rules, clonedStates, capabilities);
        }
    }
}
