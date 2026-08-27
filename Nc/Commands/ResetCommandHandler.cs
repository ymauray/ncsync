using Nc.Git;

namespace Nc.Commands;

/// <summary>
/// `nc reset <spec>` : reinitialise des fichiers a partir du dernier point de synchronisation
/// connu (`refs/nc/synced`), purement en local — pas d'appel reseau, puisque ce ref contient
/// deja un instantane exact du contenu distant au dernier `clone`/`push`/`pull` reussi.
///
/// Un fichier jamais synchronise (cree localement, absent de `refs/nc/synced`) n'a pas de
/// contenu distant vers lequel revenir : il est supprime localement plutot que de laisser
/// `nc reset` echouer silencieusement ou demi-appliquer ses effets (decision explicite de
/// l'utilisateur, voir ROADMAP.md).
/// </summary>
internal static class ResetCommandHandler
{
    private const string SyncedRef = "refs/nc/synced";

    public static int Execute(string workingDirectory, string[] specs)
    {
        var git = new GitClient(workingDirectory);

        if (!git.ReadRef(SyncedRef).Success)
        {
            Console.Error.WriteLine("Aucune synchronisation connue dans ce dossier (avez-vous fait « nc clone » ici ?).");
            return 1;
        }

        var toRestore = new List<string>();
        var toDelete = new List<string>();

        foreach (var spec in specs)
        {
            if (git.PathExistsInRef(SyncedRef, spec))
            {
                toRestore.Add(spec);
            }
            else
            {
                toDelete.Add(spec);
            }
        }

        if (toRestore.Count > 0)
        {
            var result = git.CheckoutFromRef(SyncedRef, [.. toRestore]);
            if (!result.Success)
            {
                Console.Error.WriteLine(result.StandardError);
                return 1;
            }
        }

        foreach (var spec in toDelete)
        {
            git.Unstage(spec);
            var fullPath = Path.Combine(workingDirectory, spec);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        return 0;
    }
}
