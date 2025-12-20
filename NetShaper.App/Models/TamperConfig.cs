namespace NetShaper.App.Models
{
    /// <summary>Tamper config: corrupts/truncates/rewrites packet payload.</summary>
    public sealed class TamperConfig : RuleConfigBase
    {
        private bool _truncate;
        private bool _corrupt;
        private bool _rewrite;

        public bool Truncate
        {
            get => _truncate;
            set => SetField(ref _truncate, value);
        }

        public bool Corrupt
        {
            get => _corrupt;
            set => SetField(ref _corrupt, value);
        }

        public bool Rewrite
        {
            get => _rewrite;
            set => SetField(ref _rewrite, value);
        }
    }
}
