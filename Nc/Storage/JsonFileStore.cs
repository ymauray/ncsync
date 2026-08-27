using System.Text.Json;

namespace Nc.Storage;

/// <summary>
/// (De)serialisation JSON generique d'un fichier d'etat local (`.nc/config`, `.nc/state.json`).
/// Factorise apres l'apparition d'un deuxieme consommateur (NcConfigStore, SyncStateStore)
/// suivant exactement le meme schema : valeur par defaut si le fichier n'existe pas encore,
/// creation du dossier parent a l'ecriture.
/// </summary>
internal static class JsonFileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static T Load<T>(string path) where T : new()
    {
        if (!File.Exists(path))
        {
            return new T();
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T();
    }

    public static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(value, SerializerOptions));
    }
}
