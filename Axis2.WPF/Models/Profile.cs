using Axis2.WPF.Mvvm;
using System.Collections.ObjectModel;

namespace Axis2.WPF.Models
{
    public class Profile : BindableBase
    {
        private string _name;
        private bool _isDefault;
        private bool _isWebProfile;
        private string _baseDirectory;
        private string _url;
        private string _username;
        private string _password;
        private bool _loadResource;
        private ObservableCollection<ScriptItem> _selectedScripts;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsDefault
        {
            get => _isDefault;
            set => SetProperty(ref _isDefault, value);
        }

        public bool IsWebProfile
        {
            get => _isWebProfile;
            set => SetProperty(ref _isWebProfile, value);
        }

        public string BaseDirectory
        {
            get => _baseDirectory;
            set => SetProperty(ref _baseDirectory, value);
        }

        public string URL
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        // GM login for a Web Profile (Sphere account). Only PLEVEL above Player is served.
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public bool LoadResource
        {
            get => _loadResource;
            set => SetProperty(ref _loadResource, value);

        }

        public ObservableCollection<ScriptItem> SelectedScripts
        {
            get => _selectedScripts;
            set => SetProperty(ref _selectedScripts, value);
        }

        public Profile()
        {
            _name = string.Empty;
            _baseDirectory = string.Empty;
            _url = string.Empty;
            _username = string.Empty;
            _password = string.Empty;
            _selectedScripts = new ObservableCollection<ScriptItem>();
        }
    }
}