using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FFTColorMod.Configuration
{
    public class JobEntry : INotifyPropertyChanged
    {
        private string _name = "";
        private ColorScheme _selectedTheme;
        private string _spriteFileName = "";
        private string? _previewImagePath;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public ColorScheme SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                _selectedTheme = value;
                OnPropertyChanged();
            }
        }

        public string SpriteFileName
        {
            get => _spriteFileName;
            set
            {
                _spriteFileName = value;
                OnPropertyChanged();
            }
        }

        public string? PreviewImagePath
        {
            get => _previewImagePath;
            set
            {
                _previewImagePath = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}