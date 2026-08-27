# Roadmap d'implémentation — nc (Nextcloud Sync Client)

Ce document découpe le projet en étapes concrètes avec leur statut, pour permettre à n'importe quel agent (humain ou IA) de reprendre le travail à froid sans avoir à relire tout l'historique de conversation.

## 0. Comment reprendre ce projet à froid

1. Lire `CAHIER_DES_CHARGES.md` (besoin fonctionnel) puis `SPECS.md` (design technique). Ces deux documents sont la source de vérité fonctionnelle/technique — ne pas les recontredire sans mettre à jour ce roadmap en conséquence.
2. Lire la section 2 ci-dessous (« Décisions actées ») pour ne pas re-débattre de choix déjà tranchés.
3. Regarder le tableau de la section 3 pour savoir quelle est la prochaine étape non terminée (`⬜` ou `🟨`).
4. Avant de coder une étape, vérifier ses prérequis dans le tableau — ne pas commencer une étape dont les prérequis sont encore `⬜`.
5. Après chaque étape terminée : mettre à jour son statut ici (`⬜` → `✅`), et ajouter une ligne au journal des décisions (section 5) si un choix d'implémentation non prévu dans `SPECS.md` a dû être fait.

Légende des statuts : `⬜` non commencé · `🟨` en cours · `✅` terminé · `🟥` bloqué (voir note).

## 1. État global actuel

Dépôt Git initialisé et publié sur GitHub (`ymauray/ncsync`), scaffolding de gouvernance en place. **Phase 0 terminée** : solution .NET 10 bootstrappée, `nc --help` liste les 7 commandes (stubs non implémentés). **Phase 1 terminée** : `GitClient` implémenté et testé. **Phase 2 terminée** : `ICredentialStore` (DPAPI / Keychain / Secret Service + repli fichier chiffré) implémenté et testé. **Phase 3 terminée** : `nc config username`/`nc config password` fonctionnels de bout en bout (vérifié manuellement en plus des tests) — 31 tests au total, 27 exécutés + 4 skip propres (Keychain/Secret Service, non disponibles sur la machine de dev Windows).

Prochaine étape : **Phase 4 — `NextcloudWebDavClient`**.

## 2. Décisions actées (ne pas rouvrir sans raison documentée)

| Sujet | Décision | Réf. |
|---|---|---|
| Interface Git | Shell-out vers le binaire `git` (`Process`), pas de LibGit2Sharp | SPECS.md §2 |
| Transport serveur | WebDAV natif (`HttpClient` + verbes custom), pas de lib WebDAV tierce | SPECS.md §3 |
| Cibles OS | Windows, macOS, Linux dès la v1 | CAHIER_DES_CHARGES.md §6 |
| Fusion | Aucune — conflit = refus explicite, jamais de merge automatique | CAHIER_DES_CHARGES.md §5, SPECS.md §4 |
| Périmètre commandes | `config`, `clone`, `add`, `push`, `pull`, `diff`, `status` — pas de `branch`/`tag`/`merge`/`rebase`/multi-remote | CAHIER_DES_CHARGES.md §4 |
| Credentials | Abstraction `ICredentialStore` : DPAPI (Windows) / Keychain via `security` (macOS) / `secret-tool` + repli fichier chiffré (Linux) | SPECS.md §5 |
| CLI framework | `System.CommandLine` | SPECS.md §6 |
| Nommage projets | Solution `ncsync.sln`, projet CLI `Nc/Nc.csproj` (namespace `Nc`, `AssemblyName=nc`), tests `Nc.Tests/Nc.Tests.csproj`, réf. via `InternalsVisibleTo` | Journal §5, entrée 2026-08-27 |
| Version System.CommandLine | Épinglé en `2.0.11` (stable), pas la ligne preview `3.0.0-preview.*` qui s'installe par défaut avec `dotnet add package` | Journal §5, entrée 2026-08-27 |

## 3. Découpage en étapes

| # | Étape | Statut | Prérequis | Livrable attendu |
|---|---|---|---|---|
| 0 | Bootstrap solution .NET 10 : structure de dossiers, `.sln`, projet CLI (`net10.0`), dépendance `System.CommandLine`, squelette `Program.cs` avec sous-commandes vides | ✅ | — | Solution qui compile, `nc --help` liste les 7 commandes |
| 1 | `GitClient` : wrapper `Process` autour de `git`, méthodes `Init`, `AddAll`, `Add(spec)`, `Status`, `DiffCachedNameStatus`, `DiffCached`, `Commit`, `UpdateRef`, `ReadRef` ; vérification `git --version` au démarrage | ✅ | 0 | Classe testable indépendamment de Nextcloud |
| 2 | `ICredentialStore` + 3 implémentations (DPAPI / Keychain / libsecret+fallback) + sélection runtime via `RuntimeInformation` | ✅ | 0 | Stockage/lecture chiffrés d'un secret, testé sur au moins Windows |
| 3 | Commande `nc config username/password` : écrit `.nc/config` (username, URL en clair) + credential store pour le mot de passe | ✅ | 2 | `nc config` fonctionnel de bout en bout |
| 4 | `NextcloudWebDavClient` : `HttpClient` configuré (Basic Auth + base URL), méthodes `PropFind`, `Get`, `Put`, `Delete`, `Move`, `MkCol`, parsing XML des réponses `PROPFIND` (dont ETag) | ⬜ | 3 | Client testable contre une instance Nextcloud réelle ou un mock HTTP |
| 5 | `SyncState` : lecture/écriture `.nc/state.json` (ETags connus par chemin, ref Git synced) | ⬜ | 0 | Classe de (dé)sérialisation simple |
| 6 | Commande `nc clone` : `PROPFIND` récursif + téléchargement (`GET`) + `git init`/`add -A`/`commit` + écriture `.nc/state.json` initial + génération `.gitignore` (exclut `.nc/`) | ⬜ | 1, 4, 5 | `nc clone serveur:/chemin dest` fonctionnel pour des petits fichiers |
| 7 | Commande `nc add` | ⬜ | 1 | Passe-plat direct vers `GitClient.Add` |
| 8 | Commande `nc status` | ⬜ | 1 | Passe-plat direct vers `GitClient.Status` |
| 9 | Commande `nc diff` | ⬜ | 1 | Passe-plat direct vers `GitClient.DiffCached` |
| 10 | Commande `nc push` (cas nominal, sans conflit) : diff staged → PUT/DELETE/MOVE selon le status Git → commit + update ref + update `.nc/state.json` | ⬜ | 1, 4, 5, 6 | Push simple fonctionnel sur fichiers modifiés/ajoutés/supprimés |
| 11 | `nc push` — détection de conflit par ETag avant écriture, annulation atomique du batch en cas de conflit | ⬜ | 10 | Test : modifier un fichier via un autre client Nextcloud puis tenter un `push` dessus → refus propre |
| 12 | `nc push` — mapping des renommages Git (`-M`) vers `MOVE` WebDAV | ⬜ | 10 | Test : renommer un fichier localement, `push`, vérifier `MOVE` et pas DELETE+PUT |
| 13 | Commande `nc pull` (cas nominal) : `PROPFIND` pour détecter les fichiers distants changés depuis `.nc/state.json`, téléchargement, `git add -A && commit`, update ref/state | ⬜ | 1, 4, 5, 6 | Pull simple fonctionnel |
| 14 | `nc pull` — refus fichier par fichier en cas de modification locale non poussée en conflit (sans bloquer les fichiers non conflictuels) | ⬜ | 13 | Test avec modif locale + modif distante sur le même fichier |
| 15 | Chunked upload Nextcloud (fichiers volumineux) intégré à `NextcloudWebDavClient`/`nc push` | ⬜ | 4, 10 | Test avec un fichier > seuil de chunking, y compris coupure réseau simulée |
| 16 | Gestion d'erreurs et atomicité de l'état local sur `push`/`pull` partiellement échoués (cf. SPECS.md §7 et §8.6) | ⬜ | 10, 13 | Tests d'échec réseau en cours de batch, état local vérifié cohérent après |
| 17 | Tests automatisés : unitaires (`GitClient`, `SyncState`, parsing WebDAV) + intégration (contre une instance Nextcloud de test, ex. Docker) | ⬜ | au fil de l'eau | Suite de tests exécutable en CI |
| 18 | Packaging cross-platform : publish self-contained par RID (win-x64, osx-x64/arm64, linux-x64), vérification manuelle sur les 3 OS | ⬜ | 0–16 | Binaires distribuables |

## 4. Hors périmètre pour l'instant

Voir CAHIER_DES_CHARGES.md §7. Ne pas anticiper de code pour : résolution de conflit automatique, démon de synchro continue, OCS API, interface graphique.

## 5. Journal des décisions d'implémentation

*(à compléter au fil du développement — une ligne par choix non prévu dans SPECS.md mais nécessaire pour avancer)*

| Date | Décision | Raison |
|---|---|---|
| 2026-08-27 | Un seul projet CLI (`Nc`) plutôt qu'une séparation CLI/Core dès le départ ; `GitClient`, `NextcloudWebDavClient`, `SyncState`, `ICredentialStore` vivront comme classes internes du même projet, exposées aux tests via `InternalsVisibleTo` | Pas de consommateur externe de la logique métier prévu — éviter une abstraction (séparation en assemblies) non justifiée par un besoin actuel |
| 2026-08-27 | `System.CommandLine` épinglé à la version stable `2.0.11` | `dotnet add package` installe par défaut la ligne preview `3.0.0-preview.*`, à l'API différente et non stabilisée ; `2.0.11` est la dernière stable, alignée avec `ymauray/johannes` |
| 2026-08-27 | Commandes non implémentées de la Phase 0 retournent un message explicite sur stderr + code de sortie 1 (fonction `NotImplemented`) plutôt que de planter ou de ne rien afficher | Rend l'état d'avancement visible directement en ligne de commande, cohérent avec le statut `ROADMAP.md` |
| 2026-08-27 | Solution au format `ncsync.slnx` (nouveau format XML sans GUID), généré par défaut par `dotnet new sln` sous .NET 10 SDK — diffère du `.sln` classique de `ymauray/johannes` (généré sous un SDK antérieur) | Format par défaut de l'outillage .NET 10, plus lisible ; les deux formats sont interchangeables et pleinement supportés par le SDK |
| 2026-08-27 | `GitClient.Commit` passe systématiquement `-c user.name=nc -c user.email=nc@localhost` au lieu de compter sur la config git globale de l'utilisateur | Les commits de `nc` sont des points de synchronisation techniques, pas des contributions attribuables ; garantit que `push`/`pull`/`clone` fonctionnent même sans identité git configurée (ex. CI, machine fraîchement installée) |
| 2026-08-27 | `GitClient` n'a qu'une seule méthode privée nommée `Run` (instance) et une méthode statique renommée `RunGit` (au lieu de deux surcharges `Run`) | Deux surcharges `Run` (une d'instance à 1 paramètre `params`, une statique à 2 paramètres dont un `params`) créaient une ambiguïté de résolution de surcharge silencieuse : `Run("init")` était résolu vers la statique avec `workingDirectory="init"` plutôt que vers l'instance — bug détecté par les tests (`Init()` échouait systématiquement) |
| 2026-08-27 | La vérification `git --version` au démarrage (message d'erreur + lien d'installation par OS) n'est **pas** encore branchée dans `Program.cs` à l'issue de la Phase 1 | `GitClient.GetVersion()` existe et est testé, mais son intégration dans l'UX du CLI est repoussée aux phases où les commandes appellent réellement `GitClient` (6 et suivantes), pour ne pas coupler la Phase 1 à des choix de présentation pas encore nécessaires |
| 2026-08-27 | Extraction de `ProcessRunner`/`ProcessResult` (namespace `Nc.Processes`), génériques, à partir du wrapper `Process` initialement privé à `GitClient` | La Phase 2 introduit un deuxième et un troisième consommateur de shell-out (`security`, `secret-tool`) ; dupliquer la logique de capture stdout/stderr aurait été une redite directe. `GitClient` a été mis à jour pour utiliser `ProcessRunner` sans changer son API publique |
| 2026-08-27 | Sélection de plateforme via `OperatingSystem.IsWindows()/IsMacOS()/IsLinux()` plutôt que `RuntimeInformation.IsOSPlatform(...)` mentionné dans SPECS.md §5 | Équivalent fonctionnellement, mais `OperatingSystem.IsXxx()` est le pattern reconnu par l'analyseur de compatibilité de plateforme intégré (`CA1416`) pour garder `DpapiCredentialStore` (annoté `[SupportedOSPlatform("windows")]`) utilisable sans avertissement de build depuis `CredentialStoreFactory` |
| 2026-08-27 | `CredentialKey.ForPath` dérive la clé de stockage d'un secret à partir du chemin absolu (normalisé, insensible à la casse sur Windows) du dossier de travail, hashé en SHA-256 | Non spécifié explicitement dans SPECS.md : un secret doit être associé à un dossier `nc` précis (un dossier = un serveur, cf. CAHIER_DES_CHARGES.md §5), pas stocké sous une clé globale unique qui collisionnerait entre plusieurs clones sur la même machine |
| 2026-08-27 | Sur Linux, le choix entre `SecretToolCredentialStore` et le repli `EncryptedFileCredentialStore` se fait une seule fois à la construction (sonde `secret-tool lookup` sur une clé sentinelle), pas par un essai/repli à chaque opération | Plus simple qu'un décorateur avec repli par appel, et cohérent avec la formulation de SPECS.md §5 (« si aucun trousseau n'est disponible ») qui décrit une condition d'environnement stable, pas une bascule dynamique |
| 2026-08-27 | macOS n'a **pas** de repli fichier chiffré : si `security` est absent, `KeychainCredentialStore` échoue avec une erreur explicite plutôt que de dégrader silencieusement vers `EncryptedFileCredentialStore` | `security` fait partie de macOS de base (contrairement à `secret-tool`, qui nécessite un paquet `libsecret-tools` et un trousseau actif, absents par défaut sur beaucoup de distributions Linux headless) ; son absence signalerait un environnement anormal qui mérite un échec visible plutôt qu'un repli silencieux vers une sécurité moindre |
| 2026-08-27 | `KeychainCredentialStore.Save` passe le secret en argument (`-w <secret>`) à `security`, faute d'option stdin sur `add-generic-password` | Limitation connue et documentée en commentaire dans le code : le secret est brièvement visible via `ps` pour d'autres utilisateurs locaux de la machine. `SecretToolCredentialStore`, lui, transmet le secret par entrée standard (`secret-tool store` le lit sur stdin), donc sans cette exposition |
| 2026-08-27 | `.nc/config` (JSON via `System.Text.Json`) ne contient pour l'instant que `Username` ; les champs serveur/chemin distant ne sont pas encore modélisés | `nc config` s'exécute avant `nc clone` (cf. CAHIER_DES_CHARGES.md §3) : le serveur n'est pas encore connu à cette étape. Le schéma sera étendu à la Phase 6 quand `nc clone` aura besoin d'y écrire ces champs — éviter d'anticiper une forme non encore nécessaire |
| 2026-08-27 | `nc config username/password` opèrent sur `Environment.CurrentDirectory` (pas de paramètre de dossier explicite sur ces sous-commandes) | Reproduit fidèlement le workflow illustré dans CAHIER_DES_CHARGES.md §3 : `nc config` puis `nc clone ... .` s'exécutent depuis le même répertoire courant, qui devient le dossier `nc` une fois le clone effectué |
| 2026-08-27 | `Console.OutputEncoding = Encoding.UTF8` ajouté au tout début de `Main` | Sans ce réglage, les caractères accentués des messages en français du CLI s'affichaient mal (page de code par défaut de la console Windows) — constaté lors du test manuel de bout en bout de la Phase 3 |
| 2026-08-27 | Logique métier des commandes extraite dans `Nc/Commands/ConfigCommandHandlers.cs` (méthodes statiques testables), `Program.cs` ne fait que câbler `System.CommandLine` vers ces méthodes | Permet de tester `SetUsername`/`SetPassword` unitairement sans passer par le parsing CLI ; `Program.cs` reste un pur branchement, cohérent avec le pattern à reproduire pour les commandes des phases suivantes |
| 2026-08-27 | `nc config username`/`password` : argument rendu optionnel (`ArgumentArity.ZeroOrOne`). Sans argument, `username` affiche la valeur enregistrée (rien si aucune) ; `password` ne fait rien et ne affiche rien, y compris si un mot de passe est déjà enregistré | Demande explicite de l'utilisateur après test manuel de la Phase 3. Le mot de passe ne doit jamais être affiché en clair, y compris indirectement (confirmer sa présence serait déjà une fuite d'information) ; `username` n'a pas cette contrainte |

## 6. Questions ouvertes (à trancher avant ou pendant l'étape concernée)

- Format exact du nom de dossier temporaire pour le chunked upload (étape 15).
- Faut-il une commande `nc init-config` séparée ou est-ce que `nc config` suffit à créer `.nc/` avant tout `clone` ?
- `.github/dependabot.yml` ne surveille pour l'instant que la racine (`/`) en NuGet ; à réviser si des projets supplémentaires apparaissent (cf. pattern multi-répertoires utilisé par `ymauray/johannes`).
