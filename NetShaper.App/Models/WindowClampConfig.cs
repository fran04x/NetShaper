namespace NetShaper.App.Models
{
    /// <summary>WindowClamp config: limits TCP window size.</summary>
    public sealed class WindowClampConfig : RuleConfigBase
    {
        private ushort _maxWindow = 65535;

        public ushort MaxWindow
        {
            get => _maxWindow;
            set => SetField(ref _maxWindow, value);
        }
    }
}
