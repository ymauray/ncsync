# nc — Nextcloud Sync Client

[![Build .NET](https://github.com/ymauray/ncsync/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ymauray/ncsync/actions/workflows/dotnet.yml)
[![Licence MIT](https://img.shields.io/badge/Licence-MIT-yellow.svg)](LICENSE)

`nc` est un client en ligne de commande (CLI) écrit en .NET 10 pour synchroniser un dossier Nextcloud, avec un workflow inspiré de Git — sans branches, sans tags, sans fusion.

Git n'est pas utilisé comme protocole de communication avec le serveur : Nextcloud n'expose pas de backend Git. Il sert uniquement de **moteur local de suivi des modifications** (staging area, diff, historique), tandis que le transport réel des fichiers se fait via **WebDAV**.

## Statut du projet

🚧 En développement actif. La plupart des commandes fonctionnent déjà et sont testées, mais l'outil n'est pas encore complet et aucun artefact installable n'est publié (pas de build/release, voir [`ROADMAP.md`](ROADMAP.md) phase 18).

| Commande | Statut |
|---|---|
| `nc config username`/`password` | ✅ Fonctionnel |
| `nc clone` | ✅ Fonctionnel, validé contre une instance Nextcloud réelle |
| `nc add` | ✅ Fonctionnel |
| `nc reset` | ✅ Fonctionnel |
| `nc status` | ✅ Fonctionnel |
| `nc diff` | ✅ Fonctionnel |
| `nc push` | ⬜ Pas encore implémenté |
| `nc pull` | ⬜ Pas encore implémenté |

- [`CAHIER_DES_CHARGES.md`](CAHIER_DES_CHARGES.md) — besoin fonctionnel et périmètre.
- [`SPECS.md`](SPECS.md) — design technique (architecture, choix Git/WebDAV, gestion des conflits).
- [`ROADMAP.md`](ROADMAP.md) — découpage en étapes d'implémentation, statut détaillé et journal des décisions.

## Installation (depuis les sources)

```bash
git clone https://github.com/ymauray/ncsync.git
cd ncsync
dotnet build
```

Aucun package publié pour l'instant : exécuter via `dotnet run`, ou publier un binaire autonome soi-même.

## Utilisation

```bash
dotnet run --project Nc -- config username <nom>
dotnet run --project Nc -- config password <mot-de-passe>

dotnet run --project Nc -- clone mon-serveur-nextcloud.ch:/Chemin/vers/dossier .
cd Chemin/vers/dossier   # ou le dossier de destination choisi

# ... édition, ajout, suppression de fichiers en local ...

dotnet run --project Nc -- add <spec>
dotnet run --project Nc -- status
dotnet run --project Nc -- diff
dotnet run --project Nc -- reset <spec>   # annule des modifications locales
```

`nc config username`/`nc config password` n'ont besoin d'être exécutés qu'une seule fois : l'identité est enregistrée globalement (`~/.config/ncsync`) et réutilisable pour n'importe quel futur `nc clone`, quel que soit le dossier courant.

`nc push`/`nc pull` ne sont pas encore implémentés (voir tableau ci-dessus) — le workflow complet visé (incluant l'envoi vers le serveur) est décrit dans [`CAHIER_DES_CHARGES.md`](CAHIER_DES_CHARGES.md) §3.

Commandes prévues : `config`, `clone`, `add`, `reset`, `push`, `pull`, `diff`, `status`. Voir [`CAHIER_DES_CHARGES.md`](CAHIER_DES_CHARGES.md) pour le détail et les contraintes (un seul serveur distant par dossier, pas de fusion automatique).

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- `git` disponible dans le `PATH`

## Développement

Ce logiciel est développé par un agent IA (Claude Code, Anthropic), piloté par [Yannick Mauray](https://github.com/ymauray) : chaque décision de conception, chaque étape de la [`ROADMAP.md`](ROADMAP.md) et chaque changement sont revus et validés par lui avant d'être intégrés.

## Contribuer

Voir [`CONTRIBUTING.md`](CONTRIBUTING.md) et [`ROADMAP.md`](ROADMAP.md) pour savoir où reprendre le travail.
