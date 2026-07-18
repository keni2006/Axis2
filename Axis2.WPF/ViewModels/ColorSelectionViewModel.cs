using Axis2.WPF.Mvvm;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using Axis2.WPF.Services;
using Axis2.WPF.Models;
using System;
using Axis2.WPF.Extensions;

namespace Axis2.WPF.ViewModels
{
    public class ColorSelectionViewModel : BindableBase, IDialog
    {
        private readonly IUoArtService _uoArtService;
        private ColorCellViewModel? _selectedHue;
        private ObservableCollection<SolidColorBrush> _hueSpectrum;
        private ObservableCollection<string> _previewObjectTypes;
        private string _selectedPreviewObjectType;
        private string _previewObjectId;
        private string _previewHue;
        private ImageSource? _previewImage;
        private SObject? _previewSObject;

        public ObservableCollection<ColorCellViewModel> AllHues { get; }
        public ICommand UpdatePreviewCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public ushort SelectedColorIndex { get; private set; }

        public string Title => "Select a Color";
        public object Content => this;
        public bool? DialogResult { get; private set; }

        public event EventHandler? CloseRequested;

        public ColorSelectionViewModel(IUoArtService uoArtService, SObject? previewSObject)
        {
            _uoArtService = uoArtService;
            AllHues = new ObservableCollection<ColorCellViewModel>();
            _hueSpectrum = new ObservableCollection<SolidColorBrush>();
            _previewObjectTypes = new ObservableCollection<string> { "Items", "NPCs" };
            _selectedPreviewObjectType = _previewObjectTypes.FirstOrDefault() ?? string.Empty;
            _previewSObject = previewSObject;
            _previewHue = string.Empty;

            if (_previewSObject != null)
            {
                PreviewObjectId = _previewSObject.Id;
                if (_previewSObject.Type == SObjectType.Item)
                {
                    SelectedPreviewObjectType = "Items";
                }
                else if (_previewSObject.Type == SObjectType.Npc)
                {
                    SelectedPreviewObjectType = "NPCs";
                }
            }
            else
            {
                PreviewObjectId = string.Empty;
            }

            for (ushort i = 1; i < 3000; i++)
            {
                var cell = new ColorCellViewModel { ColorIndex = i, Color = _uoArtService.GetColorFromHue(i, 16) };
                for (int j = 0; j < 32; j += 4) // Create a small 8-color spectrum
                {
                    cell.MiniSpectrum.Add(new SolidColorBrush(_uoArtService.GetColorFromHue(i, j)));
                }
                AllHues.Add(cell);
            }

            UpdatePreviewCommand = new RelayCommand(OnUpdatePreview);
            OkCommand = new RelayCommand(OnOk);
            CancelCommand = new RelayCommand(OnCancel);
            SelectedHue = AllHues.FirstOrDefault();
            OnUpdatePreview();
        }

        public ColorCellViewModel? SelectedHue
        {
            get => _selectedHue;
            set
            {
                if (SetProperty(ref _selectedHue, value))
                {
                    if (value != null)
                    {
                        SelectedColorIndex = value.ColorIndex;
                        PreviewHue = $"0x{value.ColorIndex:X}";
                        GenerateHueSpectrum(value);
                        OnUpdatePreview();
                    }
                }
            }
        }

        public ObservableCollection<SolidColorBrush> HueSpectrum
        {
            get => _hueSpectrum;
            set => SetProperty(ref _hueSpectrum, value);
        }

        public ObservableCollection<string> PreviewObjectTypes
        {
            get => _previewObjectTypes;
            set => SetProperty(ref _previewObjectTypes, value);
        }

        public string SelectedPreviewObjectType
        {
            get => _selectedPreviewObjectType;
            set => SetProperty(ref _selectedPreviewObjectType, value);
        }

        public string PreviewObjectId
        {
            get => _previewObjectId;
            set { SetProperty(ref _previewObjectId, value); OnUpdatePreview(); }
        }

        public string PreviewHue
        {
            get => _previewHue;
            set { SetProperty(ref _previewHue, value); OnUpdatePreview(); }
        }

        public ImageSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        private void GenerateHueSpectrum(ColorCellViewModel baseHue)
        {
            HueSpectrum.Clear();
            if (baseHue == null) return;

            for (int i = 0; i < 32; i++)
            {
                HueSpectrum.Add(new SolidColorBrush(_uoArtService.GetColorFromHue(baseHue.ColorIndex, i)));
            }
        }

        private void OnUpdatePreview()
        {

            int objectId = 0;
            int hue = 0;

            if (string.IsNullOrEmpty(PreviewObjectId))
            {
                PreviewImage = null;
                return;
            }

            // Parse ObjectId
            if (SelectedPreviewObjectType == "Items")
            {
                objectId = (int)PreviewObjectId.AllToUInt();
            }
            else if (SelectedPreviewObjectType == "NPCs")
            {
                // For NPCs, assume decimal unless 0x prefix
                if (PreviewObjectId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(PreviewObjectId.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out objectId))
                    {
                        PreviewImage = null;
                        return;
                    }
                }
                else
                {
                    if (!int.TryParse(PreviewObjectId, out objectId))
                    {
                        PreviewImage = null;
                        return;
                    }
                }
            }

            // Parse Hue
            if (!string.IsNullOrEmpty(PreviewHue) && PreviewHue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(PreviewHue.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out hue))
                {
                    hue = 0; // Default to no hue if parsing fails
                }
            }
            else if (!int.TryParse(PreviewHue, out hue))
            {
                hue = 0; // Default to no hue if parsing fails
            }

            if (SelectedPreviewObjectType == "Items")
            {
                PreviewImage = _uoArtService.GetItemArt(objectId, hue);
            }
            else if (SelectedPreviewObjectType == "NPCs")
            {
                PreviewImage = _uoArtService.GetNpcArt(objectId, hue);
            }
            else
            {
                PreviewImage = null;
            }

            if (PreviewImage == null)
            {
            }
            else
            {
            }
        }

        private void OnOk()
        {
            DialogResult = true;
            Close();
        }

        private void OnCancel()
        {
            DialogResult = false;
            Close();
        }

        public void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}