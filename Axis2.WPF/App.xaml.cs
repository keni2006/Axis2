using System.Windows;
using Axis2.WPF.Services;
using Axis2.WPF.ViewModels;
using Axis2.WPF.Models;
using Axis2.WPF.Mvvm;
using System.Linq;
using System;

namespace Axis2.WPF
{
    public partial class App : System.Windows.Application
    {
        public MainViewModel MainViewModel { get; private set; }
        public static IUoArtService UoArtService { get; private set; }

        public static bool IsDarkTheme { get; private set; }

        // Swaps our palette dictionary at runtime (found by its source name, so its position
        // among the merged dictionaries doesn't matter). All theme styles use DynamicResource,
        // so the custom-styled UI repaints live.
        public static void SetTheme(bool dark)
        {
            var dicts = Current.Resources.MergedDictionaries;
            var uri = new Uri(dark ? "/Themes/Palette.Dark.xaml" : "/Themes/Palette.Light.xaml", UriKind.Relative);
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.OriginalString ?? string.Empty;
                if (src.IndexOf("Palette.", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    dicts[i] = new ResourceDictionary { Source = uri };
                    break;
                }
            }
            IsDarkTheme = dark;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Logger.Init();
            Logger.Log(LogSource.Core, "Application starting...");

            // Start both theme systems (WPF-UI window chrome + our palette) in Light so the
            // title bar and content always match, regardless of the OS theme.
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
            SetTheme(false);

            // Services
            Logger.Log(LogSource.Core, "Initializing services...");
            var settingsService = new SettingsService();
            var eventAggregator = new EventAggregator();
            var travelDataService = new TravelDataService();
            var locationService = new LocationService();
            var profileService = new ProfileService();
            var dialogService = new DialogService();
            var mobTypesService = new MobTypesService();
            var scriptParser = new ScriptParser();
            var scriptParserService = new ScriptParserService();
            var skillStatService = new SkillStatService();
            var spellService = new SpellService(); // Instantiate SpellService
            var musicService = new MusicService(); // Instantiate MusicService
            var soundService = new SoundService(); // Instantiate SoundService

            Logger.Log(LogSource.Settings, "Loading settings...");
            var settings = settingsService.LoadSettings();
            Logger.Log(LogSource.Settings, "Settings loaded.");

            var uoClientCommunicator = new UoClientCommunicator(settings.GeneralSettings.CommandPrefix, settings.GeneralSettings.UOTitle);
            var uoClient = new UoClient(uoClientCommunicator);

            // Correct order of declarations for dependencies
            var uopFilePaths = settings.OverridePathsSettings.FilePaths
                .Where(item => item.FileName.EndsWith(".uop", System.StringComparison.OrdinalIgnoreCase))
                .ToDictionary(item => item.FileName, item => item.FilePath, System.StringComparer.OrdinalIgnoreCase);

            Logger.Log(LogSource.UOP, "UOP File Paths:");
            foreach (var entry in uopFilePaths)
            {
                Logger.Log(LogSource.UOP, $"  {entry.Key} = {entry.Value}");
            }
            if (!uopFilePaths.Any())
            {
                Logger.Log(LogSource.UOP, "WARNING: No UOP files found in settings.");
            }

            var fileManager = new FileManager(uopFilePaths);
            var animationManager = new AnimationManager(fileManager);
            try
            {
                Logger.Log(LogSource.Animation, "Loading UOP animations...");
                animationManager.LoadUOP();
                Logger.Log(LogSource.Animation, "UOP animations loaded.");
            }
            catch (Exception ex)
            {
                Logger.Log(LogSource.Animation, $"ERROR: Failed to load UOP animations: {ex.Message}");
                // Optionally, re-throw or handle the exception more gracefully
            }
            var bodyDefService = new BodyDefService();
            // Load body.def and bodyconv.def
            string defaultMulPath = settings.FilePathsSettings.DefaultMulPath;
            string bodyDefPath = System.IO.Path.Combine(defaultMulPath, "body.def");
            string bodyConvPath = System.IO.Path.Combine(defaultMulPath, "bodyconv.def");
            Logger.Log(LogSource.Core, "Loading body definitions...");
            bodyDefService.Load(bodyDefPath, bodyConvPath);
            Logger.Log(LogSource.Core, "Body definitions loaded.");

            var mulFileManager = new MulFileManager(fileManager, animationManager, bodyDefService);
            var uoArtService = new UoArtService(settings, mulFileManager); // Instancier UoArtService avec mulFileManager
            UoArtService = uoArtService;
            var mulMapService = new MulMapService(settingsService); // Added mulMapService declaration
            var lightDataService = new LightDataService(settings, uoArtService); // New LightDataService
            Logger.Log(LogSource.Core, "Services initialized.");

            // ViewModels
            Logger.Log(LogSource.Core, "Initializing view models...");
            var travelTabViewModel = new TravelTabViewModel(travelDataService, mulMapService, settingsService, eventAggregator, locationService, uoClient, scriptParser, scriptParserService);
            var generalTabViewModel = new GeneralTabViewModel(uoClientCommunicator, dialogService);
            var settingsTabViewModel = new SettingsTabViewModel();
            var itemTabViewModel = new ItemTabViewModel(mulFileManager, scriptParser, eventAggregator, uoClient, settingsTabViewModel.SettingsItemTabViewModel, lightDataService, uoArtService, settingsService);

            Logger.Log(LogSource.Profile, "Loading profiles...");
            var profiles = profileService.LoadProfiles();
            if (profiles.Count == 0)
            {
                profiles.Add(new Profile { Name = "<Axis Profile>", BaseDirectory = "C:\\Axis2\\Profiles\\Axis", IsDefault = true });
                profiles.Add(new Profile { Name = "<None>", BaseDirectory = "None" });
                profileService.SaveProfiles(profiles);
                Logger.Log(LogSource.Profile, "Created default profiles.");
            }
            Logger.Log(LogSource.Profile, $"{profiles.Count} profiles loaded.");

            // Determine if a default profile should be loaded
            var defaultProfile = profiles.FirstOrDefault(p => p.IsDefault);
            var loadDefaultProfileOnStartup = settings.GeneralSettings.LoadDefaultProfile;
            bool defaultProfileWasLoaded = false;

            if (loadDefaultProfileOnStartup && defaultProfile != null)
            {
                defaultProfileWasLoaded = true;
            }

            var profilesTabViewModel = new ProfilesTabViewModel(profileService, eventAggregator, profiles, null); // Will fix null later
            var accountTabViewModel = new AccountTabViewModel();
            var commandsTabViewModel = new CommandsTabViewModel();
            var itemTweakTabViewModel = new ItemTweakTabViewModel(uoClientCommunicator, scriptParser, dialogService, uoArtService, settings, eventAggregator, mulFileManager, bodyDefService, lightDataService);
            var launcherTabViewModel = new LauncherTabViewModel(settingsService);
            var logTabViewModel = new LogTabViewModel();
            var miscTabViewModel = new MiscTabViewModel(spellService, musicService, soundService, settingsService, eventAggregator, uoClient);
            var playerTweakTabViewModel = new PlayerTweakTabViewModel(skillStatService, eventAggregator, uoClient);
            var reminderTabViewModel = new ReminderTabViewModel();
            var spawnTabViewModel = new SpawnTabViewModel(mulFileManager, scriptParser, eventAggregator, uoClient, mobTypesService, animationManager, bodyDefService);
            Logger.Log(LogSource.Core, "View models initialized.");

            MainViewModel = new MainViewModel(
                fileManager, animationManager, bodyDefService,
                travelTabViewModel, eventAggregator,
                settingsService, profileService, mulFileManager,
                mobTypesService, scriptParser, dialogService,
                uoClientCommunicator, uoClient, uoArtService, lightDataService,
                generalTabViewModel, itemTabViewModel,
                settingsTabViewModel, profilesTabViewModel,
                accountTabViewModel, commandsTabViewModel,
                itemTweakTabViewModel, launcherTabViewModel,
                logTabViewModel, miscTabViewModel,
                playerTweakTabViewModel, reminderTabViewModel,
                spawnTabViewModel,
                defaultProfileWasLoaded);

            profilesTabViewModel.SetMainViewModel(MainViewModel);

            // Publish the ProfileLoadedEvent if a default profile should be loaded
            if (defaultProfileWasLoaded)
            {
                Logger.Log(LogSource.Profile, $"Loading default profile '{defaultProfile.Name}' on startup.");
                eventAggregator.Publish(new ProfileLoadedEvent(defaultProfile));
            }

            var mainWindow = new MainWindow
            {
                DataContext = MainViewModel
            };
            Logger.Log(LogSource.UI, "Showing main window.");
            mainWindow.Show();
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            if (MainViewModel != null && MainViewModel.ItemTweakTabViewModel != null)
            {
                if (!MainViewModel.ItemTweakTabViewModel.CheckAndSavePalette())
                {
                    // Cannot cancel exit from ExitEventArgs. Log for now.
                    Logger.Log(LogSource.Core, "Application exit cancelled by user (palette save).");
                }
            }
        }
    }
}

