using Nc.Processes;

namespace Nc.Git;

/// <summary>
/// Wrapper shell-out autour du binaire `git` (decision actee, voir SPECS.md §2).
/// Chaque methode ne fait qu'invoquer une commande git et exposer son resultat brut ;
/// aucune interpretation metier n'est faite ici.
/// </summary>
internal sealed class GitClient(string workingDirectory)
{
    private static readonly string[] CommitIdentityArgs = ["-c", "user.name=nc", "-c", "user.email=nc@localhost"];

    public static ProcessResult GetVersion() => ProcessRunner.Run("git", workingDirectory: null, ["--version"]);

    public ProcessResult Init() => Run("init");

    public ProcessResult AddAll() => Run("add", "-A");

    public ProcessResult Add(params string[] specs) => Run(["add", .. specs]);

    public ProcessResult Status() => Run("status", "--porcelain");

    public ProcessResult DiffCachedNameStatus() => Run("diff", "--cached", "--name-status", "-M");

    public ProcessResult DiffCached() => Run("diff", "--cached");

    // -c user.name/user.email explicites : les commits de nc sont des points de synchronisation
    // techniques, pas des contributions attribuables a une identite git locale eventuellement absente.
    public ProcessResult Commit(string message) => Run([.. CommitIdentityArgs, "commit", "-m", message]);

    public ProcessResult UpdateRef(string refName, string commitSha) => Run("update-ref", refName, commitSha);

    public ProcessResult ReadRef(string refName) => Run("rev-parse", "--verify", refName);

    // `ls-tree` (pathspec) plutot que `cat-file -e <ref>:<path>` (syntaxe de revision) :
    // cette derniere n'accepte pas les chemins a l'antislash (style Windows) meme quand le
    // fichier existe reellement dans le ref, contrairement a `checkout`/`add`/`rm` qui les
    // normalisent — bug constate en usage reel (voir ROADMAP.md, journal des decisions).
    public bool PathExistsInRef(string refName, string path) =>
        Run("ls-tree", "-r", "--name-only", refName, "--", path).StandardOutput.Trim().Length > 0;

    public ProcessResult CheckoutFromRef(string refName, params string[] paths) => Run(["checkout", refName, "--", .. paths]);

    public ProcessResult Unstage(string path) => Run("rm", "--cached", "--ignore-unmatch", "--", path);

    private ProcessResult Run(params string[] arguments) => ProcessRunner.Run("git", workingDirectory, arguments);
}
