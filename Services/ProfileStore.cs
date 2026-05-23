using System.Text.Json;
using DBSyncApp.Models;

namespace DBSyncApp.Services
{
    public static class ProfileStore
    {
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DBSyncApp", "connections.json");

        public static List<ConnectionProfile> Load()
        {
            try
            {
                if (!File.Exists(_path)) return new();
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<List<ConnectionProfile>>(json) ?? new();
            }
            catch { return new(); }
        }

        public static void Save(List<ConnectionProfile> profiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
    }
}
