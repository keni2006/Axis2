using Axis2.WPF.Models;
using Axis2.WPF.Mvvm;
using Axis2.WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Axis2.WPF.ViewModels
{
    /// <summary>
    /// Drives the Player Tweak tab. Populates the Brain / Stats / Skills pickers (skills are read
    /// from the profile's scripts — spheretables.scp — locally, or fetched from the data server for
    /// a web profile) and sends the matching console command to the client on Apply.
    /// </summary>
    public class PlayerTweakTabViewModel : ViewModelBase, IHandler<ProfileLoadedEvent>
    {
        private readonly SkillStatService _skillStatService;
        private readonly EventAggregator _eventAggregator;
        private readonly IUoClient _uoClient;

        public ObservableCollection<SkillDef> Skills { get; } = new();
        public ObservableCollection<StatDef> Stats { get; } = new();
        public ObservableCollection<BrainDef> Brains { get; } = new();

        private BrainDef _selectedBrain;
        public BrainDef SelectedBrain
        {
            get => _selectedBrain;
            set => SetProperty(ref _selectedBrain, value);
        }

        private StatDef _selectedStat;
        public StatDef SelectedStat
        {
            get => _selectedStat;
            set => SetProperty(ref _selectedStat, value);
        }

        private string _statValue = "100";
        public string StatValue
        {
            get => _statValue;
            set => SetProperty(ref _statValue, value);
        }

        private SkillDef _selectedSkill;
        public SkillDef SelectedSkill
        {
            get => _selectedSkill;
            set => SetProperty(ref _selectedSkill, value);
        }

        private string _skillValue = "100.0";
        public string SkillValue
        {
            get => _skillValue;
            set => SetProperty(ref _skillValue, value);
        }

        public ICommand ApplyBrainCommand { get; }
        public ICommand ApplyStatCommand { get; }
        public ICommand ApplySkillCommand { get; }

        public PlayerTweakTabViewModel(SkillStatService skillStatService, EventAggregator eventAggregator, IUoClient uoClient)
        {
            _skillStatService = skillStatService;
            _eventAggregator = eventAggregator;
            _uoClient = uoClient;

            // Stats and brains are fixed lists — available immediately, no profile needed.
            foreach (var stat in _skillStatService.GetStats())
                Stats.Add(stat);
            foreach (var brain in _skillStatService.GetBrains())
                Brains.Add(brain);

            SelectedStat = Stats.FirstOrDefault();
            SelectedBrain = Brains.FirstOrDefault();

            ApplyBrainCommand = new RelayCommand(ApplyBrain, () => SelectedBrain != null);
            ApplyStatCommand = new RelayCommand(ApplyStat, () => SelectedStat != null);
            ApplySkillCommand = new RelayCommand(ApplySkill, () => SelectedSkill != null);

            _eventAggregator.Subscribe(this);
        }

        public void Handle(ProfileLoadedEvent message)
        {
            if (message?.LoadedProfile == null)
            {
                Logger.Log("ERROR: PlayerTweakTabViewModel received a null profile.");
                return;
            }
            LoadSkills(message.LoadedProfile);
        }

        private async void LoadSkills(Profile profile)
        {
            Skills.Clear();
            SelectedSkill = null;

            if (profile == null)
                return;

            // Web profile: fetch the parsed skill list from the data server.
            if (profile.IsWebProfile)
            {
                if (string.IsNullOrWhiteSpace(profile.URL))
                {
                    Logger.Log("PlayerTweakTabViewModel: Web profile has no URL. Skills not loaded.");
                    return;
                }
                try
                {
                    var webSkills = await WebDataService.FetchSkillsAsync(profile.URL, profile.Username, profile.Password);
                    foreach (var skill in webSkills.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
                        Skills.Add(skill);
                    SelectedSkill = Skills.FirstOrDefault();
                    Logger.Log($"DEBUG: PlayerTweakTabViewModel - Loaded {Skills.Count} skills from web profile.");
                }
                catch (Exception ex)
                {
                    Logger.Log($"ERROR: PlayerTweakTabViewModel - Web skill load failed: {ex.Message}");
                }
                return;
            }

            // Local profile: [SKILL n] blocks live in spheretables.scp. Use the selected scripts, or
            // fall back to scanning the base directory (skipping large save dumps) so pointing a
            // profile straight at a Sphere folder also works.
            var scriptFiles = new List<string>();
            if (profile.SelectedScripts != null && profile.SelectedScripts.Any())
            {
                scriptFiles.AddRange(profile.SelectedScripts.Select(s => s.Path));
            }
            else if (!string.IsNullOrEmpty(profile.BaseDirectory) && Directory.Exists(profile.BaseDirectory))
            {
                scriptFiles.AddRange(Directory
                    .GetFiles(profile.BaseDirectory, "*.scp", SearchOption.AllDirectories)
                    .Where(f => !IsSaveFile(f)));
            }
            else
            {
                Logger.Log("PlayerTweakTabViewModel: profile has no scripts and no base directory. Skills not loaded.");
                return;
            }

            var byIndex = new Dictionary<int, SkillDef>();
            foreach (var path in scriptFiles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                    continue;
                try
                {
                    string content = await Task.Run(() => File.ReadAllText(path));
                    foreach (var skill in _skillStatService.ParseSkills(content))
                        byIndex[skill.Index] = skill; // de-dupe by skill index
                }
                catch (Exception ex)
                {
                    Logger.Log($"WARNING: PlayerTweakTabViewModel - could not read '{path}': {ex.Message}");
                }
            }

            foreach (var skill in byIndex.Values.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
                Skills.Add(skill);
            SelectedSkill = Skills.FirstOrDefault();
            Logger.Log($"DEBUG: PlayerTweakTabViewModel - Loaded {Skills.Count} skills from local scripts.");
        }

        // Account/world save dumps carry no [SKILL] blocks and are large — skip them on a folder scan.
        private static bool IsSaveFile(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            foreach (var seg in dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.Equals(seg, "accounts", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(seg, "save", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(seg, "saves", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            var name = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            if (name.StartsWith("sphereworld") || name.StartsWith("spheredata") ||
                name.StartsWith("spheremultis") || name.StartsWith("sphereaccu") ||
                name.StartsWith("sphereacct") || name.StartsWith("spheregmpage"))
                return true;

            // "sphereb" + digit = a save backup (sphereb01a = accounts, sphereb01w = world…).
            return name.StartsWith("sphereb") && name.Length > 7 && char.IsDigit(name[7]);
        }

        private void ApplyBrain()
        {
            if (SelectedBrain == null)
                return;
            // Sets the AI brain on the targeted character.
            _uoClient.SendToClient($"BRAIN {SelectedBrain.Value}");
        }

        private void ApplyStat()
        {
            if (SelectedStat == null)
                return;
            _uoClient.SendToClient($"{SelectedStat.Key} {StatValue}".Trim());
        }

        private void ApplySkill()
        {
            if (SelectedSkill == null)
                return;
            _uoClient.SendToClient($"{SelectedSkill.Key} {SkillValue}".Trim());
        }
    }
}
