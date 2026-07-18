using System.Windows.Input;
using Axis2.WPF.Mvvm;
using Axis2.WPF.Services;
using System.Threading.Tasks;

namespace Axis2.WPF.ViewModels
{
    public class GeneralTabViewModel : BindableBase
    {
        private readonly UoClientCommunicator _uoClientCommunicator;
        private readonly IDialogService _dialogService;

        public ICommand AdminCommand { get; }
        public ICommand InfoCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand ClientsCommand { get; }
        public ICommand ServerInfoCommand { get; }
        public ICommand VersionCommand { get; }
        public ICommand LinkCommand { get; }
        public ICommand FlipCommand { get; }
        public ICommand ShrinkCommand { get; }
        public ICommand DupeCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand NukeCommand { get; }
        public ICommand BuyCommand { get; }
        public ICommand SellCommand { get; }
        public ICommand InventoryCommand { get; }
        public ICommand PurchasesCommand { get; }
        public ICommand SamplesCommand { get; }
        public ICommand RestockCommand { get; }
        public ICommand SnowCommand { get; }
        public ICommand RainCommand { get; }
        public ICommand DryCommand { get; }
        public ICommand SetLightCommand { get; }
        public ICommand InvulnerableCommand { get; }
        public ICommand AllmoveCommand { get; }
        public ICommand InvisibleCommand { get; }
        public ICommand FixCommand { get; }
        public ICommand TeleCommand { get; }
        public ICommand HearAllCommand { get; }
        public ICommand GmToggleCommand { get; }
        public ICommand DetailsCommand { get; }
        public ICommand NightSightCommand { get; }
        public ICommand DebugCommand { get; }
        public ICommand JailCommand { get; }
        public ICommand ForgiveCommand { get; }
        public ICommand KillCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ResurrectCommand { get; }
        public ICommand PageOnCommand { get; }
        public ICommand PageListCommand { get; }
        public ICommand PagePlayerCommand { get; }
        public ICommand PageDisconnectCommand { get; }
        public ICommand PageKickCommand { get; }
        public ICommand PageOffCommand { get; }
        public ICommand PageQueueCommand { get; }
        public ICommand PageOriginCommand { get; }
        public ICommand PageJailCommand { get; }
        public ICommand PageDeleteCommand { get; }
        public ICommand WorldSaveCommand { get; }
        public ICommand SaveStaticsCommand { get; }
        public ICommand ResyncCommand { get; }
        public ICommand RestockAllCommand { get; }

        private int _lightLevel;
        public int LightLevel
        {
            get => _lightLevel;
            set
            {
                if (_lightLevel != value)
                {
                    _lightLevel = value;
                }
            }
        }

        public GeneralTabViewModel(UoClientCommunicator uoClientCommunicator, IDialogService dialogService)
        {
            _uoClientCommunicator = uoClientCommunicator;
            _dialogService = dialogService;

            AdminCommand = new RelayCommand(async () => await AdminAsync());
            InfoCommand = new RelayCommand(async () => await InfoAsync());
            EditCommand = new RelayCommand(async () => await EditAsync());
            ClientsCommand = new RelayCommand(async () => await ClientsAsync());
            ServerInfoCommand = new RelayCommand(async () => await ServerInfoAsync());
            VersionCommand = new RelayCommand(async () => await VersionAsync());
            LinkCommand = new RelayCommand(async () => await LinkAsync());
            FlipCommand = new RelayCommand(async () => await FlipAsync());
            ShrinkCommand = new RelayCommand(async () => await ShrinkAsync());
            DupeCommand = new RelayCommand(async () => await DupeAsync());
            RemoveCommand = new RelayCommand(async () => await RemoveAsync());
            NukeCommand = new RelayCommand(async () => await NukeAsync());
            BuyCommand = new RelayCommand(async () => await BuyAsync());
            SellCommand = new RelayCommand(async () => await SellAsync());
            InventoryCommand = new RelayCommand(async () => await InventoryAsync());
            PurchasesCommand = new RelayCommand(async () => await PurchasesAsync());
            SamplesCommand = new RelayCommand(async () => await SamplesAsync());
            RestockCommand = new RelayCommand(async () => await RestockAsync());
            SnowCommand = new RelayCommand(async () => await SnowAsync());
            RainCommand = new RelayCommand(async () => await RainAsync());
            DryCommand = new RelayCommand(async () => await DryAsync());
            SetLightCommand = new RelayCommand(async () => await SetLightAsync());
            InvulnerableCommand = new RelayCommand(async () => await InvulnerableAsync());
            AllmoveCommand = new RelayCommand(async () => await AllmoveAsync());
            InvisibleCommand = new RelayCommand(async () => await InvisibleAsync());
            FixCommand = new RelayCommand(async () => await FixAsync());
            TeleCommand = new RelayCommand(async () => await TeleAsync());
            HearAllCommand = new RelayCommand(async () => await HearAllAsync());
            GmToggleCommand = new RelayCommand(async () => await GmToggleAsync());
            DetailsCommand = new RelayCommand(async () => await DetailsAsync());
            NightSightCommand = new RelayCommand(async () => await NightsightAsync());
            DebugCommand = new RelayCommand(async () => await DebugAsync());
            JailCommand = new RelayCommand(async () => await JailAsync());
            ForgiveCommand = new RelayCommand(async () => await ForgiveAsync());
            KillCommand = new RelayCommand(async () => await KillAsync());
            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync());
            ResurrectCommand = new RelayCommand(async () => await ResurrectAsync());
            PageOnCommand = new RelayCommand(async () => await PageOnAsync());
            PageListCommand = new RelayCommand(async () => await PageListAsync());
            PagePlayerCommand = new RelayCommand(async () => await PagePlayerAsync());
            PageDisconnectCommand = new RelayCommand(async () => await PageDisconnectAsync());
            PageKickCommand = new RelayCommand(async () => await PageKickAsync());
            PageOffCommand = new RelayCommand(async () => await PageOffAsync());
            PageQueueCommand = new RelayCommand(async () => await PageQueueAsync());
            PageOriginCommand = new RelayCommand(async () => await PageOriginAsync());
            PageJailCommand = new RelayCommand(async () => await PageJailAsync());
            PageDeleteCommand = new RelayCommand(async () => await PageDeleteAsync());
            WorldSaveCommand = new RelayCommand(async () => await WorldSaveAsync());
            SaveStaticsCommand = new RelayCommand(async () => await SaveStaticsAsync());
            ResyncCommand = new RelayCommand(async () => await ResyncAsync());
            RestockAllCommand = new RelayCommand(async () => await RestockAllAsync());
        }

        private async Task AdminAsync() => await _uoClientCommunicator.SendToUOAsync("admin");
        private async Task InfoAsync() => await _uoClientCommunicator.SendToUOAsync("info");
        private async Task EditAsync() => await _uoClientCommunicator.SendToUOAsync("xedit");
        private async Task ClientsAsync() => await _uoClientCommunicator.SendToUOAsync("show serv.clients");
        private async Task ServerInfoAsync() => await _uoClientCommunicator.SendToUOAsync("information");
        private async Task VersionAsync() => await _uoClientCommunicator.SendToUOAsync("version");
        private async Task LinkAsync() => await _uoClientCommunicator.SendToUOAsync("link");
        private async Task FlipAsync() => await _uoClientCommunicator.SendToUOAsync("xflip");
        private async Task ShrinkAsync() => await _uoClientCommunicator.SendToUOAsync("shrink");
        private async Task DupeAsync() => await _uoClientCommunicator.SendToUOAsync("dupe");
        private async Task RemoveAsync() => await _uoClientCommunicator.SendToUOAsync("remove");
        private async Task NukeAsync() => await _uoClientCommunicator.SendToUOAsync("nuke");
        private async Task BuyAsync() => await _uoClientCommunicator.SendToUOAsync("buy");
        private async Task SellAsync() => await _uoClientCommunicator.SendToUOAsync("sell");
        private async Task InventoryAsync() => await _uoClientCommunicator.SendToUOAsync("bank 1a");
        private async Task PurchasesAsync() => await _uoClientCommunicator.SendToUOAsync("bank 1b");
        private async Task SamplesAsync() => await _uoClientCommunicator.SendToUOAsync("bank 1c");
        private async Task RestockAsync() => await _uoClientCommunicator.SendToUOAsync("xrestock");
        private async Task SnowAsync() => await _uoClientCommunicator.SendToUOAsync("sector.snow");
        private async Task RainAsync() => await _uoClientCommunicator.SendToUOAsync("sector.rain");
        private async Task DryAsync() => await _uoClientCommunicator.SendToUOAsync("sector.dry");
        private async Task SetLightAsync() => await _uoClientCommunicator.SendToUOAsync($"sector.light {LightLevel}");
        private async Task InvulnerableAsync() => await _uoClientCommunicator.SendToUOAsync("invulnerable");
        private async Task AllmoveAsync() => await _uoClientCommunicator.SendToUOAsync("allmove");
        private async Task InvisibleAsync() => await _uoClientCommunicator.SendToUOAsync("invisible");
        private async Task FixAsync() => await _uoClientCommunicator.SendToUOAsync("fix");
        private async Task TeleAsync() => await _uoClientCommunicator.SendToUOAsync("tele");
        private async Task HearAllAsync() => await _uoClientCommunicator.SendToUOAsync("hearall");
        private async Task GmToggleAsync() => await _uoClientCommunicator.SendToUOAsync("gm");
        private async Task DetailsAsync() => await _uoClientCommunicator.SendToUOAsync("detail");
        private async Task NightsightAsync() => await _uoClientCommunicator.SendToUOAsync("nightsight");
        private async Task DebugAsync() => await _uoClientCommunicator.SendToUOAsync("debug");
        private async Task JailAsync() => await _uoClientCommunicator.SendToUOAsync("jail");
        private async Task ForgiveAsync() => await _uoClientCommunicator.SendToUOAsync("forgive");
        private async Task KillAsync() => await _uoClientCommunicator.SendToUOAsync("kill");
        private async Task DisconnectAsync() => await _uoClientCommunicator.SendToUOAsync("xdisconnect");
        private async Task ResurrectAsync() => await _uoClientCommunicator.SendToUOAsync("xresurrect");
        private async Task PageOnAsync() => await _uoClientCommunicator.SendToUOAsync("page on");
        private async Task PageListAsync() => await _uoClientCommunicator.SendToUOAsync("page list");
        private async Task PagePlayerAsync() => await _uoClientCommunicator.SendToUOAsync("page player");
        private async Task PageDisconnectAsync()
        {
            if (_dialogService.ShowConfirmation("Warning", "Are you sure you want to disconnect the paged player?"))
                await _uoClientCommunicator.SendToUOAsync("page disconnect");
        }
        private async Task PageKickAsync()
        {
            if (_dialogService.ShowConfirmation("Warning", "Are you sure you want to ban the paged player?"))
                await _uoClientCommunicator.SendToUOAsync("page ban");
        }
        private async Task PageOffAsync() => await _uoClientCommunicator.SendToUOAsync("page off");
        private async Task PageQueueAsync() => await _uoClientCommunicator.SendToUOAsync("page queue");
        private async Task PageOriginAsync() => await _uoClientCommunicator.SendToUOAsync("page origin");
        private async Task PageJailAsync()
        {
            if (_dialogService.ShowConfirmation("Warning", "Are you sure you want to jail the paged player?"))
                await _uoClientCommunicator.SendToUOAsync("page jail");
        }
        private async Task PageDeleteAsync()
        {
            if (_dialogService.ShowConfirmation("Warning", "Are you sure you want to delete the page?"))
                await _uoClientCommunicator.SendToUOAsync("page delete");
        }
        private async Task WorldSaveAsync() => await _uoClientCommunicator.SendToUOAsync("serv.save");
        private async Task SaveStaticsAsync() => await _uoClientCommunicator.SendToUOAsync("serv.savestatics");
        private async Task ResyncAsync() => await _uoClientCommunicator.SendToUOAsync("serv.resync");
        private async Task RestockAllAsync() => await _uoClientCommunicator.SendToUOAsync("serv.restock");
    }
}