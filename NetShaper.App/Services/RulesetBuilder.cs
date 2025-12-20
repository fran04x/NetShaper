using NetShaper.App.Models;
using NetShaper.Rules;
using NetShaper.Rules.Rules;

namespace NetShaper.App.Services
{
    /// <summary>
    /// Builds Ruleset from RulesConfig.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses List&lt;&gt; which is acceptable in UI/non-hot-path context.
    /// </para>
    /// <para>
    /// <strong>Direction/Filter Note:</strong>
    /// Currently Direction and Filter properties on RuleConfigBase are NOT used for filtering.
    /// All enabled rules apply globally to all traffic regardless of direction/filter settings.
    /// Direction.Both → treated same as any other direction (no filtering).
    /// This is intentional placeholder for future per-direction rule application.
    /// </para>
    /// </remarks>
    public sealed class RulesetBuilder
    {
        /// <summary>
        /// Builds an immutable Ruleset from the provided configuration.
        /// </summary>
        /// <remarks>
        /// Rules are added in category order to optimize pipeline evaluation:
        /// <list type="number">
        ///   <item><description>Drop rules - short-circuit on match</description></item>
        ///   <item><description>Delay rules - accumulate delays</description></item>
        ///   <item><description>Modify rules - accumulate modification flags</description></item>
        ///   <item><description>Inject rules - queue packet injection</description></item>
        /// </list>
        /// </remarks>
        public Ruleset Build(RulesConfig config)
        {
            var rules = new List<RuleFunc>();
            var states = new List<RuleState>();
            var caps = RuleCapability.None;

            // ══════════════════════════════════════════════════════════════════
            // DROP RULES (short-circuit pipeline)
            // ══════════════════════════════════════════════════════════════════
            if (config.Drop.Enabled)
                AddRule(DropRule.Create(), DropRule.CreateState(), ref caps, RuleCapability.HasDropRules);
            
            if (config.Blackhole.Enabled)
                AddRule(BlackholeRule.Create(), BlackholeRule.CreateState(), ref caps, RuleCapability.HasDropRules);
            
            if (config.SynDrop.Enabled)
                AddRule(SynDropRule.Create(), SynDropRule.CreateState(), ref caps, RuleCapability.HasDropRules);
            
            if (config.LossPattern.Enabled)
                AddRule(
                    LossPatternRule.Create(),
                    LossPatternRule.CreateState(config.LossPattern.Mask, config.LossPattern.Length),
                    ref caps,
                    RuleCapability.HasDropRules);
            
            if (config.Throttle.Enabled)
                AddRule(
                    ThrottleRule.Create(),
                    ThrottleRule.CreateState(config.Throttle.Pps),
                    ref caps,
                    RuleCapability.HasDropRules);
            
            if (config.Bandwidth.Enabled)
                AddRule(
                    BandwidthRule.Create(),
                    BandwidthRule.CreateState(config.Bandwidth.Bps),
                    ref caps,
                    RuleCapability.HasDropRules);

            // ══════════════════════════════════════════════════════════════════
            // DELAY RULES (accumulate delays)
            // ══════════════════════════════════════════════════════════════════
            if (config.Lag.Enabled)
                AddRule(
                    LagRule.Create(),
                    LagRule.CreateState(config.Lag.DelayMs),
                    ref caps,
                    RuleCapability.HasDelayRules);
            
            if (config.Jitter.Enabled)
                AddRule(
                    JitterRule.Create(),
                    JitterRule.CreateState(config.Jitter.MinMs, config.Jitter.MaxMs),
                    ref caps,
                    RuleCapability.HasDelayRules);
            
            if (config.OutOfOrder.Enabled)
                AddRule(
                    OutOfOrderRule.Create(),
                    OutOfOrderRule.CreateState(config.OutOfOrder.MaxDelayMs),
                    ref caps,
                    RuleCapability.HasDelayRules);
            
            if (config.Burst.Enabled)
                AddRule(
                    BurstRule.Create(),
                    BurstRule.CreateState(config.Burst.IntervalMs),
                    ref caps,
                    RuleCapability.HasDelayRules);
            
            if (config.AckDelay.Enabled)
                AddRule(
                    AckDelayRule.Create(),
                    AckDelayRule.CreateState(config.AckDelay.DelayMs),
                    ref caps,
                    RuleCapability.HasDelayRules);

            // ══════════════════════════════════════════════════════════════════
            // MODIFY RULES (accumulate modification flags)
            // ══════════════════════════════════════════════════════════════════
            if (config.Duplicate.Enabled)
                AddRule(
                    DuplicateRule.Create(),
                    DuplicateRule.CreateState(config.Duplicate.Count),
                    ref caps,
                    RuleCapability.HasDuplicateRules);
            
            if (config.Tamper.Enabled)
            {
                var flags = ModifyFlags.None;
                if (config.Tamper.Truncate) flags |= ModifyFlags.Truncate;
                if (config.Tamper.Corrupt) flags |= ModifyFlags.Corrupt;
                if (config.Tamper.Rewrite) flags |= ModifyFlags.Rewrite;
                AddRule(
                    TamperRule.Create(),
                    TamperRule.CreateState(flags),
                    ref caps,
                    RuleCapability.HasModifyRules);
            }
            
            if (config.MtuClamp.Enabled)
                AddRule(
                    MtuClampRule.Create(),
                    MtuClampRule.CreateState(config.MtuClamp.MaxSize),
                    ref caps,
                    RuleCapability.HasModifyRules);
            
            if (config.WindowClamp.Enabled)
                AddRule(
                    WindowClampRule.Create(),
                    WindowClampRule.CreateState(config.WindowClamp.MaxWindow),
                    ref caps,
                    RuleCapability.HasModifyRules);

            // ══════════════════════════════════════════════════════════════════
            // INJECT RULES (queue packet injection)
            // ══════════════════════════════════════════════════════════════════
            if (config.TcpRst.Enabled)
                AddRule(
                    TcpRstRule.Create(),
                    TcpRstRule.CreateState(config.TcpRst.OneShot),
                    ref caps,
                    RuleCapability.HasInjectRules);

            return Ruleset.Create(rules.ToArray(), states.ToArray(), caps);

            void AddRule(RuleFunc rule, RuleState state, ref RuleCapability c, RuleCapability cap)
            {
                rules.Add(rule);
                states.Add(state);
                c |= cap;
            }
        }
    }
}
