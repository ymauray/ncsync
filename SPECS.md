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

Cross-platform dès la v1 (cf. cahier des charges §6) : une abstraction `ICredentialStore` avec une implémentation par plateforme, sélectionnée au runtime via `RuntimeInformation.IsOSPlatform` :

| Plateforme | Mécanisme |
|---|---|
| Windows | `System.Security.Cryptography.ProtectedData` (DPAPI), scope utilisateur courant |
| macOS | Keychain, via appel `security` en ligne de commande (shell-out, cohérent avec l'approche adoptée pour Git) ou binding natif |
| Linux | libsecret (Secret Service API, via `secret-tool` en shell-out) avec repli sur un fichier chiffré par une clé dérivée d'un secret local si aucun trousseau n'est disponible (ex. environnement headless/serveur) |

Fichier de config local hors du dépôt Git (`.nc/config`), jamais commité (cf. `.gitignore` généré). Seul le mot de passe/app-password est chiffré via `ICredentialStore` ; le reste (username, URL serveur) peut être stocké en clair dans `.nc/config`.

## 6. Architecture logicielle (.NET 10)

- **CLI** : `System.CommandLine` pour le parsing des sous-commandes (`config`, `clone`, `add`, `push`, `pull`, `diff`, `status`).
- **Couche Git** : `GitClient` — wrapper autour de `Process` exécutant `git`, parsing de la sortie texte (`--porcelain` pour un format stable).
- **Couche WebDAV** : `NextcloudWebDavClient` — wrapper `HttpClient`, requêtes XML (construction avec `XDocument`, parsing des réponses `PROPFIND` avec `XDocument`/LINQ to XML).
- **Couche état local** : `SyncState` — lecture/écriture de `.nc/state.json` (dernier ref synchronisé, ETags connus par chemin).
- **Couche credentials** : `ICredentialStore` / `DpapiCredentialStore`.

## 7. Gestion des erreurs

- Un `push` doit être **atomique du point de vue de l'état local** : si une des requêtes WebDAV échoue en cours de batch, on n'avance ni le ref Git `refs/nc/synced` ni `.nc/state.json`. Les fichiers déjà envoyés avant l'échec restent sur le serveur (WebDAV ne fournit pas de transaction multi-fichiers), mais l'état local reflète fidèlement ce qui a réellement été confirmé par le serveur (accusé de réception par fichier), permettant un `nc push` de reprise cohérent.
- Toute erreur réseau/HTTP doit être reportée avec le chemin du fichier concerné, jamais une erreur générique.

## 8. Points d'attention identifiés (à traiter en priorité de risque)

1. Chunked upload Nextcloud (gros fichiers, reprise).
2. Fiabilité de la détection de conflit par ETag (cas limites : dossier renommé côté serveur, fichier supprimé côté serveur pendant qu'il est modifié en local).
3. Détection de renommage côté Git (`-M` sur `git diff`) correctement mappée vers un `MOVE` WebDAV plutôt qu'un DELETE+PUT.
4. Présence de `git` dans le `PATH` sur les trois OS cibles — vérification et message d'erreur au démarrage.
5. Stockage des identifiants sur macOS/Linux : dépendance à des outils externes (`security`, `secret-tool`) potentiellement absents (ex. conteneur, serveur headless) — prévoir le repli fichier chiffré dès la v1, pas comme correctif ultérieur.
6. `nc pull` partiel : gérer proprement le cas où certains fichiers sont appliqués et d'autres bloqués par conflit dans le même appel, sans laisser l'état local (`refs/nc/synced`, `.nc/state.json`) incohérent.
