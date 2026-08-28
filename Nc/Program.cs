using System.CommandLine;
using Nc.Commands;

namespace Nc;

internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var usernameArgument = new Argument<string?>("username")
        {
            Description = "Nom d'utilisateur du compte Nextcloud (omis : affiche le nom enregistré)",
            Arity = ArgumentArity.ZeroOrOne
        };
        var configUsernameCommand = new Command("username", "Enregistre ou affiche le nom d'utilisateur Nextcloud")
        {
            usernameArgument
        };
        configUsernameCommand.SetAction((parseResult, _) =>
            Task.FromResult(ConfigCommandHandlers.SetUsername(Environment.CurrentDirectory, parseResult.GetValue(usernameArgument))));

        var passwordArgument = new Argument<string?>("password")
        {
            Description = "Mot de passe (ou app password) du compte Nextcloud (omis : ne fait rien)",
            Arity = ArgumentArity.ZeroOrOne
        };
        var configPasswordCommand = new Command("password", "Enregistre le mot de passe Nextcloud")
        {
            passwordArgument
        };
        configPasswordCommand.SetAction((parseResult, _) =>
            Task.FromResult(ConfigCommandHandlers.SetPassword(Environment.CurrentDirectory, parseResult.GetValue(passwordArgument))));

        var configCommand = new Command("config", "Configure les identifiants de connexion au serveur Nextcloud");
        configCommand.Subcommands.Add(configUsernameCommand);
        configCommand.Subcommands.Add(configPasswordCommand);

        var remoteArgument = new Argument<string>("remote")
        {
            Description = "Serveur et chemin distant, ex: mon-serveur.ch:/Chemin/vers/dossier"
        };
        var destinationArgument = new Argument<string>("destination")
        {
            Description = "Dossier local de destination",
            DefaultValueFactory = _ => "."
        };
        var cloneCommand = new Command("clone", "Clone un dossier distant Nextcloud dans un dossier local")
        {
            remoteArgument,
            destinationArgument
        };
        cloneCommand.SetAction((parseResult, cancellationToken) => CloneCommandHandler.ExecuteAsync(
            Environment.CurrentDirectory,
            parseResult.GetValue(remoteArgument)!,
            parseResult.GetValue(destinationArgument)!,
            cancellationToken));

        var specArgument = new Argument<string[]>("spec")
        {
            Description = "Fichier(s) ou motif(s) à marquer comme prêts à être synchronisés"
        };
        var addCommand = new Command("add", "Marque des fichiers comme prêts à être synchronisés")
        {
            specArgument
        };
        addCommand.SetAction((parseResult, _) =>
            Task.FromResult(GitPassthroughCommandHandlers.Add(Environment.CurrentDirectory, parseResult.GetValue(specArgument)!)));

        var resetSpecArgument = new Argument<string[]>("spec")
        {
            Description = "Fichier(s) à réinitialiser à partir de la dernière synchronisation connue"
        };
        var resetCommand = new Command("reset", "Réinitialise des fichiers à partir du dernier état synchronisé (équivalent local de `git checkout -- <spec>`)")
        {
            resetSpecArgument
        };
        resetCommand.SetAction((parseResult, _) =>
            Task.FromResult(ResetCommandHandler.Execute(Environment.CurrentDirectory, parseResult.GetValue(resetSpecArgument)!)));

        var pushCommand = new Command("push", "Envoie vers le serveur d'origine les modifications marquées par 'nc add'");
        pushCommand.SetAction((_, cancellationToken) => PushCommandHandler.ExecuteAsync(Environment.CurrentDirectory, cancellationToken));

        var pullCommand = new Command("pull", "Récupère depuis le serveur d'origine les modifications distantes");
        pullCommand.SetAction((_, cancellationToken) => PullCommandHandler.ExecuteAsync(Environment.CurrentDirectory, cancellationToken));

        var diffCommand = new Command("diff", "Affiche le détail des changements locaux non encore poussés");
        diffCommand.SetAction((_, _) =>
            Task.FromResult(GitPassthroughCommandHandlers.Diff(Environment.CurrentDirectory)));

        var statusCommand = new Command("status", "Affiche l'état local (fichiers modifiés/ajoutés/supprimés non poussés)");
        statusCommand.SetAction((_, _) =>
            Task.FromResult(GitPassthroughCommandHandlers.Status(Environment.CurrentDirectory)));

        var rootCommand = new RootCommand("nc — client de synchronisation Nextcloud, workflow inspiré de Git")
        {
            configCommand,
            cloneCommand,
            addCommand,
            resetCommand,
            pushCommand,
            pullCommand,
            diffCommand,
            statusCommand
        };

        return rootCommand.Parse(args).InvokeAsync();
    }
}
