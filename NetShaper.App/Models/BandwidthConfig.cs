namespace NetShaper.App.Models
{
    /// <summary>Bandwidth unit for display.</summary>
    public enum BandwidthUnit { KB, MB, GB }

    /// <summary>Bandwidth config: rate-limit by bytes per second.</summary>
    public sealed class BandwidthConfig : RuleConfigBase
    {
        private int _limit = 10;
        private BandwidthUnit _unit = BandwidthUnit.KB;
        private bool _dropOverflow = true;

        /// <summary>Bandwidth limit value (interpreted with Unit).</summary>
        public int Limit
        {
            get => _limit;
            set => SetField(ref _limit, value);
        }

        /// <summary>Unit for the limit (KB, MB, GB).</summary>
        public BandwidthUnit Unit
        {
            get => _unit;
            set => SetField(ref _unit, value);
        }

        /// <summary>If true, drop overflow packets. If false, delay them.</summary>
        public bool DropOverflow
        {
            get => _dropOverflow;
            set => SetField(ref _dropOverflow, value);
        }

        /// <summary>Computed bytes per second based on Limit and Unit.</summary>
        public long Bps => Unit switch
        {
            BandwidthUnit.KB => Limit * 1024L,
            BandwidthUnit.MB => Limit * 1024L * 1024L,
            BandwidthUnit.GB => Limit * 1024L * 1024L * 1024L,
            _ => Limit * 1024L
        };
    }
}
