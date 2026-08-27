# nc — Nextcloud Sync Client

[![Build .NET](https://github.com/ymauray/ncsync/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ymauray/ncsync/actions/workflows/dotnet.yml)
[![Licence MIT](https://img.shields.io/badge/Licence-MIT-yellow.svg)](LICENSE)

`nc` est un client en ligne de commande (CLI) écrit en .NET 10 pour synchroniser un dossier Nextcloud, avec un workflow inspiré de Git — sans branches, sans tags, sans fusion.

Git n'est pas utilisé comme protocole de communication avec le serveur : Nextcloud n'expose pas de backend Git. Il sert uniquement de **moteur local de suivi des modifications** (staging area, diff, historique), tandis que le transport réel des fichiers se fait via **WebDAV**.

## Statut du projet

🚧 En phase de conception. Aucun artefact installable n'est disponible pour l'instant.

- [`CAHIER_DES_CHARGES.md`](CAHIER_DES_CHARGES.md) — besoin fonctionnel et périmètre.
- [`SPECS.md`](SPECS.md) — design technique (architecture, choix Git/WebDAV, gestion des conflits).
- [`ROADMAP.md`](ROADMAP.md) — découpage en étapes d'implémentation et statut d'avancement.

## Workflow visé

```bash
nc config username myname
nc config password mypassword

nc clone mon-serveur-nextcloud.ch:/Chemin/vers/dossier .

# ... édition, ajout, suppression de fichiers en local ...

nc add <spec>
nc push
```

Commandes prévues : `config`, `clone`, `add`, `push`, `pull`, `diff`, `status`. Voir [`CAHIER_DES_CHARGES.md`](CAHIER_DES_CHARGES.md) pour le détail et les contraintes (un seul serveur distant par dossier, pas de fusion automatique).

## Prérequis (une fois l'outil implémenté)

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- `git` disponible dans le `PATH`

## Contribuer

Voir [`CONTRIBUTING.md`](CONTRIBUTING.md) et [`ROADMAP.md`](ROADMAP.md) pour savoir où reprendre le travail.
