using System.Text.Json;

namespace Nc.Configuration;

/// <summary>
/// Lecture/ecriture de `.nc/config` (JSON) dans un dossier de travail donne. Ce dossier n'est
/// pas necessairement encore un depot git au moment de `nc config` (`nc clone` s'en charge).
/// </summary>
internal sealed class NcConfigStore(string workingDirectory)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private string ConfigPath => Path.Combine(workingDirectory, ".nc", "config");

    public NcConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return new NcConfig();
        }

        return JsonSerializer.Deserialize<NcConfig>(File.ReadAllText(ConfigPath)) ?? new NcConfig();
    }

    public void Save(NcConfig config)
    {
        Directory.CreateDirectory(Path.Combine(workingDirectory, ".nc"));
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, SerializerOptions));
    }
}
