using Nc.Storage;

namespace Nc.Configuration;

/// <summary>
/// Lecture/ecriture de `.nc/config` (JSON) dans un dossier de travail donne. Ce dossier n'est
/// pas necessairement encore un depot git au moment de `nc config` (`nc clone` s'en charge).
/// </summary>
internal sealed class NcConfigStore(string workingDirectory)
{
    private string ConfigPath => Path.Combine(workingDirectory, ".nc", "config");

    public NcConfig Load() => JsonFileStore.Load<NcConfig>(ConfigPath);

    public void Save(NcConfig config) => JsonFileStore.Save(ConfigPath, config);
}
