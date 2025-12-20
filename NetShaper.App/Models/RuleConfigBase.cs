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

        // Visual-only flags for bounce-back animation effect
        private bool _inboundVisuallyOff;
        private bool _outboundVisuallyOff;

        /// <summary>
        /// Gets or sets whether inbound traffic is affected.
        /// Maps to Direction enum for UI toggle binding.
        /// Uses visual bounce-back when user tries to disable the last active direction.
        /// </summary>
        public bool IsInbound
        {
            get => !_inboundVisuallyOff && (_direction == Direction.Inbound || _direction == Direction.Both);
            set
            {
                var outbound = _direction == Direction.Outbound || _direction == Direction.Both;
                if (value && outbound)
                    Direction = Direction.Both;
                else if (value)
                    Direction = Direction.Inbound;
                else if (outbound)
                    Direction = Direction.Outbound;
                else
                {
                    // Visual bounce-back: show OFF briefly, then bounce back to ON
                    _inboundVisuallyOff = true;
                    OnPropertyChanged(nameof(IsInbound));
                    
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        async () =>
                        {
                            await System.Threading.Tasks.Task.Delay(100);
                            _inboundVisuallyOff = false;
                            OnPropertyChanged(nameof(IsInbound));
                        });
                    return;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOutbound));
            }
        }

        /// <summary>
        /// Gets or sets whether outbound traffic is affected.
        /// Maps to Direction enum for UI toggle binding.
        /// Uses visual bounce-back when user tries to disable the last active direction.
        /// </summary>
        public bool IsOutbound
        {
            get => !_outboundVisuallyOff && (_direction == Direction.Outbound || _direction == Direction.Both);
            set
            {
                var inbound = _direction == Direction.Inbound || _direction == Direction.Both;
                if (value && inbound)
                    Direction = Direction.Both;
                else if (value)
                    Direction = Direction.Outbound;
                else if (inbound)
                    Direction = Direction.Inbound;
                else
                {
                    // Visual bounce-back: show OFF briefly, then bounce back to ON
                    _outboundVisuallyOff = true;
                    OnPropertyChanged(nameof(IsOutbound));
                    
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        async () =>
                        {
                            await System.Threading.Tasks.Task.Delay(100);
                            _outboundVisuallyOff = false;
                            OnPropertyChanged(nameof(IsOutbound));
                        });
                    return;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInbound));
            }
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
