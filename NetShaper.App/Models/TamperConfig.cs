namespace NetShaper.App.Models
{
    /// <summary>Tamper config: corrupts/truncates/rewrites packet payload.</summary>
    public sealed class TamperConfig : RuleConfigBase
    {
        private int _chance = 100;
        private bool _corrupt;
        private bool _truncate;
        private bool _rewrite;
        private bool _checksumCorrupt = true;

        /// <summary>Chance to tamper packet (0-100%).</summary>
        public int Chance
        {
            get => _chance;
            set => SetField(ref _chance, value);
        }

        /// <summary>Corrupt random bytes in payload.</summary>
        public bool Corrupt
        {
            get => _corrupt;
            set => SetField(ref _corrupt, value);
        }

        /// <summary>Truncate packet payload.</summary>
        public bool Truncate
        {
            get => _truncate;
            set => SetField(ref _truncate, value);
        }

        /// <summary>Rewrite payload with random data.</summary>
        public bool Rewrite
        {
            get => _rewrite;
            set => SetField(ref _rewrite, value);
        }

        /// <summary>Corrupt checksum (always causes packet to be rejected).</summary>
        public bool ChecksumCorrupt
        {
            get => _checksumCorrupt;
            set => SetField(ref _checksumCorrupt, value);
        }
    }
}
