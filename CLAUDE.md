# Standards de Codage et d'Interaction (ncsync)

Ce document définit les règles de développement et d'interaction pour le projet `nc` (ncsync). Ces règles s'appliquent en priorité à toute intelligence artificielle intervenant sur le codebase.

## 0. Reprise du travail

Avant toute intervention, lire dans l'ordre : `CAHIER_DES_CHARGES.md` (besoin fonctionnel), `SPECS.md` (design technique), `ROADMAP.md` (découpage en étapes et statut d'avancement). `ROADMAP.md` indique la prochaine étape non terminée et les décisions déjà actées à ne pas rouvrir sans raison documentée.

Après une étape d'implémentation terminée, mettre à jour son statut dans `ROADMAP.md` et compléter le journal des décisions (§5) si un choix non prévu par `SPECS.md` a dû être fait.

## 1. Messages de Commit

- Tous les messages de commit doivent suivre la norme **Conventional Commits**.
- Les messages doivent être rédigés **exclusivement en français**.
- Format : `<type>(<scope>): <description>` (ex: `feat(webdav): ajout du client PROPFIND`).

## 2. Gestion des Commits par l'IA

- **Invitation uniquement** : L'IA ne doit JAMAIS effectuer de commit de sa propre initiative. Elle doit attendre une instruction explicite (Directive) de l'utilisateur.
- **Discrétion** : L'IA ne doit PAS demander à l'utilisateur s'il souhaite committer après chaque modification. C'est à l'utilisateur de décider du moment opportun pour consolider les changements.
- **Co-auteur** : L'agent IA doit obligatoirement s'identifier dans les commits comme co-auteur (via la mention `Co-authored-by: <Nom> <email>` dans le message de commit).

## 3. Gestion des Branches et Pull Requests

- La branche `main` est protégée. Toute modification doit être développée sur une branche dédiée et intégrée via une pull request.
- L'IA ne doit pousser une branche ou créer une pull request que sur instruction explicite de l'utilisateur.

## 4. Cycle de Développement (Plan-Act-Validate)

- **Tests Unitaires** : Après chaque modification de code, si un test unitaire peut être ajouté pour valider le changement, il doit être implémenté immédiatement dans le projet de tests.
- **Validation Systématique** : Le projet doit être recompilé (`dotnet build`) après chaque modification pour garantir l'absence de régressions de compilation.
- **Exécution des Tests** : Les tests unitaires doivent être lancés (`dotnet test`) après chaque modification significative.

## 5. Standards Techniques

- **C# / .NET 10** : Utilisation des fonctionnalités modernes du langage.
- **Indentation** : Tabulations pour le C#, espaces pour le Markdown/YAML/JSON (configuré via `.editorconfig`).
- **Interface Git** : shell-out vers le binaire `git` (`Process`), jamais LibGit2Sharp (décision actée, voir `SPECS.md` §2).
- **Interface Nextcloud** : WebDAV natif (`HttpClient` + verbes custom), jamais de librairie WebDAV tierce (voir `SPECS.md` §3).
- **Cross-platform** : le code doit rester compatible Windows/macOS/Linux (voir `CAHIER_DES_CHARGES.md` §6) — éviter toute dépendance spécifique à une plateforme sans passer par une abstraction (`ICredentialStore` notamment).
