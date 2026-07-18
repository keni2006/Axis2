using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Axis2.WPF.Services
{
    public interface IDialogService
    {
        bool ShowConfirmation(string title, string message);
        Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class;
        Task<string?> ShowInputDialogAsync(string title, string message, string initialInput = "");
        Task<string?> ShowSelectListDialogAsync(string title, string message, ObservableCollection<string> items);
    }
}