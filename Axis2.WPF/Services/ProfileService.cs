using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization; // Added
using Axis2.WPF.Models;

namespace Axis2.WPF.Services
{
    public class ProfileService
    {
        // Anchored to the executable's folder (with a legacy working-directory fallback) so a local
        // profile loads reliably regardless of how the app was launched.
        private readonly string _profilesFilePath = ResolvePath("profiles.json");

        private static string ResolvePath(string fileName)
        {
            var beside = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(beside)) return beside;
            if (File.Exists(fileName)) return fileName;
            return beside;
        }

        private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.Preserve // Added
        };

        public ObservableCollection<Profile> LoadProfiles()
        {
            if (File.Exists(_profilesFilePath))
            {
                string jsonString = File.ReadAllText(_profilesFilePath);
                var profiles = JsonSerializer.Deserialize<ObservableCollection<Profile>>(jsonString, _jsonSerializerOptions);
                return profiles ?? new ObservableCollection<Profile>();
            }
            return new ObservableCollection<Profile>();
        }

        public void SaveProfiles(ObservableCollection<Profile> profiles)
        {
            string jsonString = JsonSerializer.Serialize(profiles, _jsonSerializerOptions);
            File.WriteAllText(_profilesFilePath, jsonString);
        }
    }
}