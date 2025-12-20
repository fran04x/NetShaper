namespace NetShaper.App.Models
{
    /// <summary>
    /// Container for all 16 rule configurations.
    /// </summary>
    public sealed class RulesConfig
    {
        public DropConfig Drop { get; set; } = new();
        public BlackholeConfig Blackhole { get; set; } = new();
        public SynDropConfig SynDrop { get; set; } = new();
        public LagConfig Lag { get; set; } = new();
        public JitterConfig Jitter { get; set; } = new();
        public OutOfOrderConfig OutOfOrder { get; set; } = new();
        public BurstConfig Burst { get; set; } = new();
        public AckDelayConfig AckDelay { get; set; } = new();
        public ThrottleConfig Throttle { get; set; } = new();
        public BandwidthConfig Bandwidth { get; set; } = new();
        public LossPatternConfig LossPattern { get; set; } = new();
        public DuplicateConfig Duplicate { get; set; } = new();
        public TamperConfig Tamper { get; set; } = new();
        public MtuClampConfig MtuClamp { get; set; } = new();
        public WindowClampConfig WindowClamp { get; set; } = new();
        public TcpRstConfig TcpRst { get; set; } = new();

        /// <summary>
        /// Validates ONLY enabled rules.
        /// Returns true if all enabled rules have valid parameters.
        /// </summary>
        public bool IsValid()
        {
            if (Lag.Enabled && Lag.DelayMs <= 0) return false;
            if (Jitter.Enabled && Jitter.MinMs > Jitter.MaxMs) return false;
            if (Throttle.Enabled && Throttle.Pps <= 0) return false;
            if (Bandwidth.Enabled && Bandwidth.Bps <= 0) return false;
            if (LossPattern.Enabled && (LossPattern.Length <= 0 || LossPattern.Length > 64)) return false;
            if (Duplicate.Enabled && Duplicate.Count < 1) return false;
            if (OutOfOrder.Enabled && OutOfOrder.MaxDelayMs <= 0) return false;
            if (Burst.Enabled && Burst.IntervalMs <= 0) return false;
            if (AckDelay.Enabled && AckDelay.DelayMs <= 0) return false;
            if (MtuClamp.Enabled && MtuClamp.MaxSize <= 0) return false;
            if (WindowClamp.Enabled && WindowClamp.MaxWindow <= 0) return false;
            return true;
        }
    }
}
