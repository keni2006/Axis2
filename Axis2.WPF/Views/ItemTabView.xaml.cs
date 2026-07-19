using Axis2.WPF.Shared;
using Axis2.WPF.ViewModels;
using Axis2.WPF.Models; // Added for SObject
using System; // Added for Math
using System.Linq; // Added for Any()
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Added for MouseButtonEventArgs, MouseEventArgs
using System.Windows.Media; // Added for Point, Vector

namespace Axis2.WPF.Views
{
    /// <summary>
    /// Interaction logic for ItemTabView.xaml
    /// </summary>
    public partial class ItemTabView : System.Windows.Controls.UserControl
    {
        public ItemTabView()
        {
            InitializeComponent();
        }

        // Remembered full-mode column widths so compact mode can restore them.
        private System.Windows.GridLength _detailWidth, _rightWidth;
        private bool _compactStored;

        /// <summary>
        /// Compact mode: collapse the detail + preview/custom-list columns to leave just the
        /// categories tree and item list (a focused item picker). Restores on exit.
        /// </summary>
        public void SetCompact(bool on)
        {
            if (!_compactStored)
            {
                _detailWidth = DetailCol.Width;
                _rightWidth = RightCol.Width;
                _compactStored = true;
            }

            var zero = new System.Windows.GridLength(0);
            DetailCol.Width = on ? zero : _detailWidth;
            RightCol.Width = on ? zero : _rightWidth;
            DetailCol.MinWidth = on ? 0 : 248;
            RightCol.MinWidth = on ? 0 : 190;
            Split2Col.Width = on ? zero : System.Windows.GridLength.Auto;
            Split3Col.Width = on ? zero : System.Windows.GridLength.Auto;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ItemTabViewModel viewModel)
            {
                viewModel.SelectedTreeItem = e.NewValue;
            }
        }

        // Double-click a result row => run Create (place the item in the UO client).
        private void DisplayedItems_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TryCreateSelected();
        }

        // Enter on the list => Create. Ctrl+F anywhere on the tab => focus the search box.
        private void Root_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                SearchBox?.Focus();
                SearchBox?.SelectAll();
                e.Handled = true;
            }
        }

        private void DisplayedItems_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                TryCreateSelected();
                e.Handled = true;
            }
        }

        private void TryCreateSelected()
        {
            if (DataContext is ItemTabViewModel viewModel &&
                viewModel.SelectedItem != null &&
                viewModel.CreateCommand != null &&
                viewModel.CreateCommand.CanExecute(null))
            {
                viewModel.CreateCommand.Execute(null);
            }
        }

        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ItemTabViewModel viewModel)
            {
                var findWindow = new FindWindow(viewModel.GetUniqueScriptTypes());
                findWindow.Owner = System.Windows.Application.Current.MainWindow;
                findWindow.SearchRequested += FindWindow_SearchRequested;
                findWindow.ShowDialog();
            }
        }

        private void FindWindow_SearchRequested(Axis2.WPF.Shared.SearchCriteria searchCriteria)
        {
            if (DataContext is ItemTabViewModel viewModel)
            {
                viewModel.FilterItems(searchCriteria);
            }
        }
        private System.Windows.Point _startPoint;

        private void DisplayedItems_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void DisplayedItems_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                System.Windows.Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    System.Windows.Controls.ListView listView = sender as System.Windows.Controls.ListView;
                    if (listView != null)
                    {
                        SObject selectedItem = listView.SelectedItem as SObject;
                        if (selectedItem != null)
                        {
                            System.Windows.DataObject dragData = new System.Windows.DataObject("mySObjectFormat", selectedItem);
                            System.Windows.DragDrop.DoDragDrop(listView, dragData, System.Windows.DragDropEffects.Copy);
                        }
                    }
                }
            }
        }

        private void CustomItemList_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("mySObjectFormat"))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void CustomItemList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("mySObjectFormat"))
            {
                SObject droppedItem = e.Data.GetData("mySObjectFormat") as SObject;
                if (droppedItem != null)
                {
                    if (DataContext is ItemTabViewModel viewModel)
                    {
                        if (viewModel.SelectedCustomItemList != null)
                        {
                            // Check if the item already exists in the list to prevent duplicates
                            if (!viewModel.SelectedCustomItemList.Items.Any(item => item.Id == droppedItem.Id))
                            {
                                viewModel.SelectedCustomItemList.Items.Add(droppedItem);
                            }
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("Please select a custom list first.", "No List Selected", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        }
                    }
                }
            }
            e.Handled = true;
        }
    }
}