namespace NetShaper.App.Models
{
    /// <summary>MtuClamp config: enforces maximum packet size.</summary>
    public sealed class MtuClampConfig : RuleConfigBase
    {
        private int _maxSize = 1500;

        public int MaxSize
        {
            get => _maxSize;
            set => SetField(ref _maxSize, value);
        }
    }
}
