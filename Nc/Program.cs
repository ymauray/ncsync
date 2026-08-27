using System.CommandLine;

namespace Nc;

internal static class Program
{
    private static int Main(string[] args)
    {
        var usernameArgument = new Argument<string>("username")
        {
            Description = "Nom d'utilisateur du compte Nextcloud"
        };
        var configUsernameCommand = new Command("username", "Enregistre le nom d'utilisateur Nextcloud")
        {
            usernameArgument
        };
        configUsernameCommand.SetAction(_ => NotImplemented("config username"));

        var passwordArgument = new Argument<string>("password")
        {
            Description = "Mot de passe (ou app password) du compte Nextcloud"
        };
        var configPasswordCommand = new Command("password", "Enregistre le mot de passe Nextcloud")
        {
            passwordArgument
        };
        configPasswordCommand.SetAction(_ => NotImplemented("config password"));

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
        cloneCommand.SetAction(_ => NotImplemented("clone"));

        var specArgument = new Argument<string[]>("spec")
        {
            Description = "Fichier(s) ou motif(s) à marquer comme prêts à être synchronisés"
        };
        var addCommand = new Command("add", "Marque des fichiers comme prêts à être synchronisés")
        {
            specArgument
        };
        addCommand.SetAction(_ => NotImplemented("add"));

        var pushCommand = new Command("push", "Envoie vers le serveur d'origine les modifications marquées par 'nc add'");
        pushCommand.SetAction(_ => NotImplemented("push"));

        var pullCommand = new Command("pull", "Récupère depuis le serveur d'origine les modifications distantes");
        pullCommand.SetAction(_ => NotImplemented("pull"));

        var diffCommand = new Command("diff", "Affiche le détail des changements locaux non encore poussés");
        diffCommand.SetAction(_ => NotImplemented("diff"));

        var statusCommand = new Command("status", "Affiche l'état local (fichiers modifiés/ajoutés/supprimés non poussés)");
        statusCommand.SetAction(_ => NotImplemented("status"));

        var rootCommand = new RootCommand("nc — client de synchronisation Nextcloud, workflow inspiré de Git")
        {
            configCommand,
            cloneCommand,
            addCommand,
            pushCommand,
            pullCommand,
            diffCommand,
            statusCommand
        };

        return rootCommand.Parse(args).Invoke();
    }

    private static int NotImplemented(string command)
    {
        Console.Error.WriteLine($"nc {command} : pas encore implémenté (voir ROADMAP.md).");
        return 1;
    }
}
