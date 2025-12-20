namespace NetShaper.App.Models
{
    /// <summary>Burst config: releases packets in periodic windows.</summary>
    public sealed class BurstConfig : RuleConfigBase
    {
        private int _intervalMs = 100;

        public int IntervalMs
        {
            get => _intervalMs;
            set => SetField(ref _intervalMs, value);
        }
    }
}
