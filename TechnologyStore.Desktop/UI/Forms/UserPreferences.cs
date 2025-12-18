using System.Text.Json;

namespace TechnologyStore.Desktop.UI.Forms;

public class UserPreferences
{
    public string? Username { get; set; }
    public bool RememberMe { get; set; }

    private static readonly string PreferencesFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TechnologyStoreAutomation",
        "user_prefs.json");

    public static void Save(string username, bool rememberMe)
    {
        try
        {
            var prefs = new UserPreferences
            {
                Username = rememberMe ? username : string.Empty,
                RememberMe = rememberMe
            };

            var directory = Path.GetDirectoryName(PreferencesFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(prefs);
            File.WriteAllText(PreferencesFile, json);
        }
        catch (Exception)
        {
            // Ignore errors saving preferences (not critical)
        }
    }

    public static UserPreferences Load()
    {
        try
        {
            if (File.Exists(PreferencesFile))
            {
                var json = File.ReadAllText(PreferencesFile);
                return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
            }
        }
        catch (Exception)
        {
            // Ignore errors loading preferences
        }

        return new UserPreferences();
    }
}
