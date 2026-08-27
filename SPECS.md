# Spécifications techniques — nc (Nextcloud Sync Client)

Implémentation en .NET 10. Voir `CAHIER_DES_CHARGES.md` pour le périmètre fonctionnel.

## 1. Principe général

Git n'est **pas** utilisé comme protocole de synchronisation avec le serveur — Nextcloud n'expose aucun backend Git. Git sert uniquement de **moteur local de suivi des modifications** (staging area, diff, historique), tandis que le transport réel des fichiers vers/depuis le serveur se fait exclusivement via **WebDAV**.

Le dossier de travail contient donc un dépôt Git classique (`.git/`), jamais poussé ni cloné vers Nextcloud, qui sert uniquement à :

- détecter les fichiers ajoutés/modifiés/supprimés/renommés depuis le dernier point de synchronisation,
- réutiliser le staging area de Git (`git add`) tel quel pour `nc add`,
- conserver un historique local des synchronisations réussies.

## 2. Interface avec Git : shell-out

**Décision** : appel du binaire `git` installé sur la machine via `System.Diagnostics.Process`, plutôt que `LibGit2Sharp`.

Raisons :
- pas de dépendance sur des binaires natifs libgit2 par plateforme/RID, dont le support est parfois en retard sur les dernières versions de .NET ;
- comportement garanti identique à celui de la ligne de commande `git` que l'utilisateur connaît déjà ;
- simplicité d'implémentation et de debug (on peut reproduire toute commande à la main).

Contrainte induite : `git` doit être présent dans le `PATH` de l'utilisateur. À vérifier au démarrage (`git --version`) avec message d'erreur clair si absent.

Commandes Git utilisées en interne :

| Opération `nc` | Commande(s) Git shell-out |
|---|---|
| `nc clone` (init) | `git init`, `git add -A`, `git commit -m "initial sync"` |
| `nc add <spec>` | `git add <spec>` |
| `nc status` | `git status --porcelain` |
| `nc diff` | `git diff --cached` (contenu détaillé des changements en staging non poussés) |
| `nc reset <spec>` (fichier connu de `refs/nc/synced`) | `git checkout refs/nc/synced -- <spec>` (restaure contenu + index en un coup) |
| `nc reset <spec>` (fichier jamais synchronisé) | `git rm --cached --ignore-unmatch -- <spec>` puis suppression du fichier local (pas de contenu distant vers lequel revenir) |
| `nc push` (calcul du diff) | `git diff --cached --name-status -M` (détection incluse des renommages) |
| `nc push` (scellement) | `git commit -m "sync <timestamp>"`, puis avancement d'un ref custom |
| `nc pull` (état local avant écrasement) | `git status --porcelain` pour vérifier l'absence de modifications locales non poussées sur les fichiers concernés |
| `nc pull` (application) | écriture des fichiers reçus (WebDAV) sur le disque, puis `git add -A && git commit -m "pull <timestamp>"` et avancement de `refs/nc/synced` |

Binaire `git` requis dans le `PATH` sur les trois plateformes cibles (Windows/macOS/Linux) — vérification au démarrage (`git --version`) avec message d'erreur clair si absent, incluant un lien d'installation adapté à la plateforme détectée.

Point de synchronisation : un ref Git dédié, par exemple `refs/nc/synced`, mis à jour via `git update-ref refs/nc/synced <commit-sha>` après un push réussi. Ce ref permet de calculer à tout moment le diff entre l'état synchronisé et l'état actuellement en staging (`git diff refs/nc/synced --cached`).

Le dossier `.nc/` (config, métadonnées de sync type ETags connus) doit être ajouté à un `.gitignore` généré automatiquement au clone.

## 3. Interface avec Nextcloud : WebDAV

Accès via `HttpClient` avec des méthodes WebDAV personnalisées (pas de librairie WebDAV tierce nécessaire — le nombre de verbes utilisés est réduit) :

| Verbe | Usage |
|---|---|
| `PROPFIND` (profondeur 1 ou infinity) | Listing d'un dossier distant + métadonnées (dont l'en-tête `oc:etag`), utilisé au `clone` et pour la détection de conflit avant `push` |
| `GET` | Téléchargement d'un fichier (au `clone`) |
| `PUT` | Upload d'un fichier ajouté/modifié |
| `DELETE` | Suppression d'un fichier côté serveur |
| `MOVE` | Renommage/déplacement d'un fichier côté serveur |
| `MKCOL` | Création d'un dossier distant si nécessaire |

Endpoint de base : `https://<serveur>/remote.php/dav/files/<username>/<chemin>`.

### Authentification

- Utilisation recommandée d'un **app password** Nextcloud (généré côté serveur) plutôt que le mot de passe réel du compte, envoyé en Basic Auth sur HTTPS.
- `nc config username` / `nc config password` stockent ces valeurs chiffrées localement (voir §5).

### Upload de fichiers volumineux

Pour les fichiers dépassant un seuil (~10 Mo), utilisation de l'API de chunked upload v2 de Nextcloud :
`/remote.php/dav/uploads/<username>/<upload-id>/...` — upload par morceaux dans un dossier temporaire, puis `MOVE` final vers la destination réelle. C'est le point technique le plus délicat de l'implémentation et doit être testé en priorité (gros fichiers, reprise après coupure réseau).

## 4. Détection de conflit

Comme il n'y a pas de fusion (cf. cahier des charges §5), la stratégie de conflit est un **refus explicite**, pas une résolution automatique :

1. Au `clone` et après chaque `push` réussi, on enregistre localement l'ETag distant connu de chaque fichier (dans `.nc/state.json`).
2. Avant d'écrire un fichier lors d'un `push`, on effectue un `PROPFIND` ciblé pour comparer l'ETag distant actuel à l'ETag connu.
3. Si l'ETag a changé (modification par un autre client depuis la dernière synchro) → le `push` est annulé pour l'ensemble du batch, avec la liste des fichiers en conflit affichée à l'utilisateur. Aucune écriture partielle.

Le même mécanisme s'applique en sens inverse pour `nc pull` : avant d'écraser un fichier local avec la version distante, on vérifie via `git status --porcelain` que ce fichier n'a pas de modification locale non poussée. Si c'est le cas → `nc pull` refuse d'écraser ce fichier précis, le signale, et poursuit pour les autres fichiers non conflictuels (contrairement à `push` qui annule tout le batch, car `pull` est une opération de lecture qui ne risque pas de compromettre l'état serveur).

## 5. Stockage des identifiants

Cross-platform dès la v1 (cf. cahier des charges §6) : une abstraction `ICredentialStore` avec une implémentation par plateforme, sélectionnée au runtime via `OperatingSystem.IsWindows()`/`IsMacOS()`/`IsLinux()` (pas `RuntimeInformation.IsOSPlatform`, écarté — voir ROADMAP.md, journal des décisions, pour la raison liée à l'analyseur `CA1416`) :

| Plateforme | Mécanisme |
|---|---|
| Windows | `System.Security.Cryptography.ProtectedData` (DPAPI), scope utilisateur courant |
| macOS | Keychain, via appel `security` en ligne de commande (shell-out, cohérent avec l'approche adoptée pour Git) |
| Linux | libsecret (Secret Service API, via `secret-tool` en shell-out) avec repli sur un fichier chiffré (AES-256-GCM) par une clé locale si aucun trousseau n'est disponible (ex. environnement headless/serveur) — macOS n'a volontairement pas ce repli (`security` fait partie du système de base, son absence est anormale) |

### Deux niveaux de configuration : identité globale et config par dossier

- **Identité par défaut (globale, multi-projets)** : `nc config username`/`nc config password` écrivent dans un emplacement global réutilisable depuis n'importe quel dossier — `~/.config/ncsync/config` pour le nom d'utilisateur (`IdentityConfigStore`), une clé de trousseau fixe `CredentialKey.Global` pour le mot de passe (`IdentityCredentialStore`, symétrique). Écriture : tente le global, replie sur `.nc/config`/une clé locale (par dossier) du dossier courant avec un message d'erreur si l'écriture globale échoue. Lecture : priorité au global, repli **silencieux** sur le local si absent. But : configurer une fois, réutiliser pour n'importe quel futur `nc clone`.
- **Config par dossier (locale, propre à chaque clone)** : `.nc/config` (`NcConfigStore`) et une entrée `ICredentialStore` sous `CredentialKey.ForPath(dossier)` — écrits par `nc clone` dans le dossier de destination (copie de l'identité utilisée pour ce clone + `ServerUrl`/`RemotePath`, propres à ce dossier et non globalisables). C'est cette config locale que `push`/`pull`/`status`/etc. liront une fois un dossier cloné, puisqu'elle seule connaît le serveur/chemin distant de ce dossier précis.

Fichiers hors du dépôt Git dans les deux cas (`.nc/` exclu via `.gitignore` généré au clone). Seul le mot de passe/app-password est chiffré via `ICredentialStore` ; le reste (username, URL serveur, chemin distant) est stocké en clair dans les fichiers `config` JSON correspondants — mais ces fichiers restent sensibles et ne doivent jamais être lus par un agent IA (voir `CLAUDE.md` §5).

## 6. Architecture logicielle (.NET 10)

- **CLI** (`Nc/Program.cs`) : `System.CommandLine`, actions asynchrones uniformément (`SetAction((parseResult, cancellationToken) => Task<int>)`, `Main` retourne `Task<int>` via `InvokeAsync()`). Sous-commandes : `config` (`username`, `password`), `clone`, `add`, `reset`, `push`, `pull`, `diff`, `status`.
- **`Nc.Processes`** : `ProcessRunner`/`ProcessResult` — invocation générique d'un exécutable externe (git, security, secret-tool) avec capture stdout/stderr et distinction « exécutable introuvable » vs « commande en échec ». Base commune réutilisée par `Nc.Git` et `Nc.Credentials`.
- **`Nc.Git`** : `GitClient` — wrapper `Process` autour de `git` (`Init`, `AddAll`, `Add`, `Status`, `DiffCachedNameStatus`, `DiffCached`, `Commit`, `UpdateRef`, `ReadRef`, `PathExistsInRef`, `CheckoutFromRef`, `Unstage`, `GetVersion`).
- **`Nc.WebDav`** : `NextcloudWebDavClient` (`HttpClient` injectable pour les tests, factory `Create` pour l'usage réel), `WebDavPropFindParser` (parsing pur du XML `multistatus`), `WebDavHrefResolver` (résout un `href` absolu en chemin relatif au dossier demandé), `WebDavEntry`, `WebDavDepth`.
- **`Nc.Sync`** : `NcCloneService` (algorithme de `nc clone`, pur — prend un `NextcloudWebDavClient` déjà construit), `SyncState`/`SyncStateStore` (`.nc/state.json`, ETags par chemin).
- **`Nc.Storage`** : `JsonFileStore` — (dé)sérialisation JSON générique (`Load<T>`/`Save<T>`), réutilisée par `NcConfigStore` et `SyncStateStore`.
- **`Nc.Configuration`** : `NcConfig` (schéma : `Username`, `ServerUrl`, `RemotePath`), `NcConfigStore` (`.nc/config`, local par dossier), `IdentityConfigStore` (identité globale `~/.config/ncsync/config` avec repli local, voir §5), `GlobalConfigLocation`.
- **`Nc.Credentials`** : `ICredentialStore`, `DpapiCredentialStore`, `KeychainCredentialStore`, `SecretToolCredentialStore`, `EncryptedFileCredentialStore`, `CredentialStoreFactory` (sélection par plateforme), `CredentialKey` (dérivation de clé par dossier + clé globale fixe), `IdentityCredentialStore` (symétrique à `IdentityConfigStore` pour le mot de passe, voir §5).
- **`Nc.Commands`** : un handler testable par commande (ou groupe de commandes passe-plat), câblé depuis `Program.cs` : `ConfigCommandHandlers`, `CloneCommandHandler` (+ `RemoteSpec`), `ResetCommandHandler`, `GitPassthroughCommandHandlers` (`add`/`status`/`diff`).

## 7. Gestion des erreurs

- Un `push` doit être **atomique du point de vue de l'état local** : si une des requêtes WebDAV échoue en cours de batch, on n'avance ni le ref Git `refs/nc/synced` ni `.nc/state.json`. Les fichiers déjà envoyés avant l'échec restent sur le serveur (WebDAV ne fournit pas de transaction multi-fichiers), mais l'état local reflète fidèlement ce qui a réellement été confirmé par le serveur (accusé de réception par fichier), permettant un `nc push` de reprise cohérent.
- Toute erreur réseau/HTTP doit être reportée avec le chemin du fichier concerné, jamais une erreur générique.

## 8. Points d'attention identifiés (à traiter en priorité de risque)

1. Chunked upload Nextcloud (gros fichiers, reprise).
2. Fiabilité de la détection de conflit par ETag (cas limites : dossier renommé côté serveur, fichier supprimé côté serveur pendant qu'il est modifié en local).
3. Détection de renommage côté Git (`-M` sur `git diff`) correctement mappée vers un `MOVE` WebDAV plutôt qu'un DELETE+PUT.
4. Présence de `git` dans le `PATH` sur les trois OS cibles — vérification et message d'erreur au démarrage.
5. ~~Stockage des identifiants sur macOS/Linux : dépendance à des outils externes (`security`, `secret-tool`) potentiellement absents~~ — traité en Phase 2 (`EncryptedFileCredentialStore`, repli Linux uniquement, voir §5 et ROADMAP.md).
6. `nc pull` partiel : gérer proprement le cas où certains fichiers sont appliqués et d'autres bloqués par conflit dans le même appel, sans laisser l'état local (`refs/nc/synced`, `.nc/state.json`) incohérent.
