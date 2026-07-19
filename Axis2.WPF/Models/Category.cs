using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Axis2.WPF.Models
{
    public class Category : INotifyPropertyChanged
    {
        private string _name = "";
        private ObservableCollection<SubCategory> _subSections = new();
        private bool _isExpanded;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public ObservableCollection<SubCategory> SubSections { get => _subSections; set { _subSections = value; OnPropertyChanged(nameof(SubSections)); OnPropertyChanged(nameof(Count)); } }
        public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); } }

        /// <summary>Total items across all sub-sections (shown next to the category in the tree).</summary>
        public int Count => _subSections.Sum(s => s.Items.Count);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
