namespace NetShaper.App.Models
{
    /// <summary>Duplicate config: send packet N times.</summary>
    public sealed class DuplicateConfig : RuleConfigBase
    {
        private int _count = 1;

        public int Count
        {
            get => _count;
            set => SetField(ref _count, value);
        }
    }
}
