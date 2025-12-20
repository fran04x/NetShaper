namespace NetShaper.App.Models
{
    /// <summary>Bandwidth config: rate-limit by bytes per second (BPS).</summary>
    public sealed class BandwidthConfig : RuleConfigBase
    {
        private int _bps = 1_000_000;

        public int Bps
        {
            get => _bps;
            set => SetField(ref _bps, value);
        }
    }
}
