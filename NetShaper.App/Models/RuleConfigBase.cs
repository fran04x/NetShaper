using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetShaper.App.Models
{
    /// <summary>
    /// Base class for all rule configurations.
    /// Implements INotifyPropertyChanged for WPF data binding.
    /// </summary>
    public abstract class RuleConfigBase : INotifyPropertyChanged
    {
        private bool _enabled;
        private Direction _direction = Direction.Both;
        private string _filter = "";

        /// <summary>
        /// Whether this rule is active in the pipeline.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set => SetField(ref _enabled, value);
        }

        /// <summary>
        /// Traffic direction filter (Inbound, Outbound, or Both).
        /// NOTE: Currently a UI placeholder - does not filter traffic.
        /// </summary>
        public Direction Direction
        {
            get => _direction;
            set => SetField(ref _direction, value);
        }

        /// <summary>
        /// BPF-like filter expression.
        /// NOTE: Currently a UI placeholder - does not filter traffic.
        /// </summary>
        public string Filter
        {
            get => _filter;
            set => SetField(ref _filter, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
