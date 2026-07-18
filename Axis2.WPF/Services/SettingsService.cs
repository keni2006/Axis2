using System.IO;
using System.Text.Json;
using Axis2.WPF.Models;
using Axis2.WPF.ViewModels.Settings;
using System;

namespace Axis2.WPF.Services
{
    public class SettingsService : ISettingsService // Implements ISettingsService
    {
        // Anchor to the executable's folder so settings load the same way no matter what the current
        // working directory is (e.g. launched from a shortcut). Falls back to a legacy file in the
        // working directory if one already exists there, so older setups keep working.
        private readonly string _settingsFilePath = ResolvePath("settings.json");

        private static string ResolvePath(string fileName)
        {
            var beside = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(beside)) return beside;
            if (File.Exists(fileName)) return fileName; // legacy working-directory location
            return beside;
        }

        public event EventHandler<Models.SettingsChangedEventArgs> SettingsChanged; // Implements event from ISettingsService

        public AllSettings LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                string jsonString = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AllSettings>(jsonString);
                return settings ?? new AllSettings();
            }
            return new AllSettings(); // Return default settings if file doesn't exist
        }

        public void SaveSettings(AllSettings settings)
        {
            string jsonString = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, jsonString);
            // Raise the event after saving settings
            SettingsChanged?.Invoke(this, new Models.SettingsChangedEventArgs(settings));
        }
    }
}