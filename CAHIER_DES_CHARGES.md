# Cahier des charges — nc (Nextcloud Sync Client)

## 1. Contexte et objectif

Nextcloud propose une synchronisation de fichiers via son client officiel, mais celui-ci fonctionne selon un modèle de synchronisation bidirectionnelle continue. L'objectif de ce projet est de fournir un **client en ligne de commande alternatif**, inspiré du fonctionnement de Git, mais volontairement simplifié :

- pas de branches,
- pas de tags,
- pas de fusion (merge),
- un seul serveur/chemin distant par dossier de travail (pas de notion de remote multiple),
- push possible uniquement vers le serveur depuis lequel le dossier a été cloné.

Git n'est pas utilisé comme protocole de communication avec le serveur (Nextcloud n'expose pas de backend Git), mais comme **moteur local de suivi des modifications** : détection des ajouts, modifications, suppressions et renommages entre deux synchronisations.

## 2. Utilisateurs cibles

Développeurs ou utilisateurs techniques souhaitant versionner/synchroniser un dossier Nextcloud depuis la ligne de commande, avec un vocabulaire et des réflexes proches de Git, sans la complexité d'un vrai système de contrôle de version distribué.

## 3. Workflow attendu

```
nc config username myname
nc config password mypassword

nc clone mon-serveur-nextcloud.ch:/Chemin/vers/dossier .

# ... édition, ajout, suppression de fichiers en local ...

nc add <spec>
nc push
```

## 4. Commandes fonctionnelles

| Commande | Rôle |
|---|---|
| `nc config username <nom>` | Enregistre l'identifiant de connexion au serveur Nextcloud |
| `nc config password <mdp>` | Enregistre le mot de passe (ou app password) de connexion |
| `nc clone <serveur>:<chemin> <dest>` | Récupère l'intégralité d'un dossier distant Nextcloud dans un dossier local `<dest>`, et initialise le suivi des modifications |
| `nc add <spec>` | Marque un ou plusieurs fichiers comme prêts à être synchronisés (ajout, modification ou suppression) |
| `nc reset <spec>` | Réinitialise un ou plusieurs fichiers à partir de la dernière synchronisation connue, annulant toute modification locale (équivalent local de `git checkout -- <spec>`) ; un fichier jamais synchronisé est supprimé localement |
| `nc push` | Envoie vers le serveur d'origine les modifications marquées par `nc add` |
| `nc pull` | Récupère depuis le serveur d'origine les modifications apportées côté distant depuis la dernière synchronisation, et met à jour le dossier local |
| `nc diff` | Affiche le détail des changements locaux non encore poussés (contenu, pas seulement la liste des fichiers) |
| `nc status` | Affiche l'état local (fichiers modifiés/ajoutés/supprimés non encore poussés) |

Les commandes hors scope volontairement : `branch`, `tag`, `merge`, `rebase`, `remote add`, `fetch` multi-remote.

`nc pull` reste soumis aux mêmes contraintes que le reste de l'outil : pas de fusion. S'il existe des modifications locales non poussées sur un fichier également modifié côté serveur, `nc pull` doit refuser d'écraser le fichier local et signaler le conflit, plutôt que de tenter une fusion automatique.

## 5. Contraintes fonctionnelles

- **Un seul serveur distant par dossier de travail**, fixé au moment du `clone`. Impossible de pousser ailleurs.
- **Pas de branches ni de tags** : un dossier de travail suit une ligne d'historique unique.
- **Pas de fusion automatique** : si le contenu distant a changé depuis la dernière synchronisation sur un fichier concerné par le `push`, l'opération doit être refusée avec un message explicite plutôt que d'écraser silencieusement les données du serveur.
- **Historique local** : chaque `push` réussi doit constituer un point de synchronisation identifiable, permettant de savoir plus tard quels fichiers ont été envoyés et quand.
- **Traçabilité** : la liste des fichiers à pousser (déterminée par `nc add`) doit être fiable, y compris pour les renommages et les suppressions.

## 6. Contraintes non fonctionnelles

- **Doit fonctionner nativement sur Windows, macOS et Linux** — cible cross-platform dès la v1, pas une extension future.
- Les identifiants de connexion doivent être stockés de façon chiffrée localement, jamais en clair.
- Doit supporter des fichiers de taille importante (pas seulement de petits fichiers texte).
- Les erreurs réseau ou serveur doivent être gérées explicitement (pas de corruption de l'état local en cas d'échec partiel d'un `push`).

## 7. Hors périmètre (v1)

- Résolution de conflits automatique.
- Synchronisation bidirectionnelle continue / démon en arrière-plan.
- Gestion de plusieurs utilisateurs ou de partages Nextcloud (OCS API).
- Interface graphique.
