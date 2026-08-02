using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Localcel.Models
{
    public class AppItemViewModel : INotifyPropertyChanged
    {
        private bool _isRunning;

        public AppConfig Config { get; set; }

        public AppItemViewModel(AppConfig config)
        {
            Config = config;
        }

        public string Name => Config.Name;
        public string DisplayTitle => Config.DisplayTitle;

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }

        public Brush StatusBrush => IsRunning
            ? new SolidColorBrush(Color.FromArgb(255, 108, 203, 95))  // Green #6CCB5F
            : new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));  // Red #C42B1C

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
