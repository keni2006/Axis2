using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Axis2.WPF.Models
{
    public class SubCategory : INotifyPropertyChanged
    {
        private string _name = "";
        private ObservableCollection<SObject> _items = new();
        private bool _isExpanded;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public ObservableCollection<SObject> Items { get => _items; set { _items = value; OnPropertyChanged(nameof(Items)); OnPropertyChanged(nameof(Count)); } }
        public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); } }

        /// <summary>Item count (shown next to the sub-section in the tree).</summary>
        public int Count => _items.Count;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
