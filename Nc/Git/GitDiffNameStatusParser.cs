namespace Nc.Git;

/// <summary>
/// Parsing pur (sans dépendance process) de la sortie de `git diff --cached --name-status -M`
/// en une liste de <see cref="GitDiffEntry"/>. Séparé de <see cref="GitClient"/> pour rester
/// testable avec de simples chaînes, sans invoquer `git`.
/// </summary>
internal static class GitDiffNameStatusParser
{
    public static IReadOnlyList<GitDiffEntry> Parse(string nameStatusOutput)
    {
        var entries = new List<GitDiffEntry>();

        foreach (var rawLine in nameStatusOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split('\t');
            var status = fields[0];

            entries.Add(status[0] switch
            {
                'A' => new GitDiffEntry(GitChangeType.Added, fields[1]),
                'M' => new GitDiffEntry(GitChangeType.Modified, fields[1]),
                'D' => new GitDiffEntry(GitChangeType.Deleted, fields[1]),
                'R' => new GitDiffEntry(GitChangeType.Renamed, fields[2], fields[1]),
                _ => throw new NotSupportedException($"Statut « {status} » non pris en charge par nc push (ligne : « {line} »)."),
            });
        }

        return entries;
    }
}
