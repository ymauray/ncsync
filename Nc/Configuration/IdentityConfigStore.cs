using Nc.Storage;

namespace Nc.Configuration;

/// <summary>
/// Source de verite pour l'identite nc (nom d'utilisateur), utilisee par `nc config username`.
/// Ecriture : tente d'abord le dossier global `~/.config/ncsync` ; si ce dossier ne peut pas
/// etre cree (permissions, etc.), affiche un message et ecrit dans `.nc/config` du dossier
/// courant a la place. Lecture : priorite au fichier global, repli silencieux sur le local
/// s'il est absent.
/// </summary>
internal sealed class IdentityConfigStore(string localFallbackDirectory, string? globalConfigDirectory = null)
{
    private readonly string _globalConfigDirectory = globalConfigDirectory ?? GlobalConfigLocation.Directory;

    private string GlobalConfigPath => Path.Combine(_globalConfigDirectory, "config");

    private string LocalConfigPath => Path.Combine(localFallbackDirectory, ".nc", "config");

    public NcConfig Load() => File.Exists(GlobalConfigPath)
        ? JsonFileStore.Load<NcConfig>(GlobalConfigPath)
        : JsonFileStore.Load<NcConfig>(LocalConfigPath);

    public void Save(NcConfig config)
    {
        try
        {
            Directory.CreateDirectory(_globalConfigDirectory);
            JsonFileStore.Save(GlobalConfigPath, config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Impossible d'écrire dans « {_globalConfigDirectory} » ({ex.Message}) : utilisation du dossier courant à la place.");
            JsonFileStore.Save(LocalConfigPath, config);
        }
    }
}
