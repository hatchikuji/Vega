# Vega

Vega est une application C# WPF permettant de copier, chiffrer et transférer des dossiers de projets depuis un partage réseau Windows, en utilisant VeraCrypt pour la création et le montage de volumes sécurisés.
Celle-ci a été réalisée lors d'un stage en entreprise et a été présentée lors de mon oral au BTS

## Fonctionnalités

- Connexion à un partage réseau Windows avec authentification.
- Visualisation et sélection des dossiers à copier.
- Calcul automatique de la taille des dossiers.
- Création de volumes VeraCrypt chiffrés (WORK et RAW) avec facteurs de taille ajustables.
- Montage et démontage automatique des volumes VeraCrypt.
- Copie multithreadée des fichiers et dossiers sélectionnés dans les volumes montés.
- Pause et reprise de la copie.
- Journalisation des événements et erreurs dans un fichier log.
- Interface utilisateur graphique simple et intuitive.

## Prérequis

- Windows 10 ou supérieur.
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0).
- [VeraCrypt](https://www.veracrypt.fr/en/Downloads.html) installé dans le dossier par défaut (`C:\Program Files\VeraCrypt`).

## Installation

1. Cloner le dépôt :
   ```
   git clone <url-du-repo>
   ```
2. Ouvrir le projet dans JetBrains Rider ou Visual Studio.
3. Restaurer les dépendances NuGet si nécessaire.
4. Compiler et exécuter l’application.

## Utilisation

1. Saisir le nom de l’hôte distant, le login et le mot de passe réseau.
2. Se connecter au partage réseau.
3. Sélectionner le lecteur partagé et le dossier projet.
4. Choisir les dossiers à copier.
5. Définir le chemin de sauvegarde local.
6. Ajuster les facteurs de taille des volumes si besoin.
7. Cliquer sur « Générer les volumes » pour créer et monter les volumes VeraCrypt.
8. Cliquer sur « Copier les fichiers » pour lancer la copie.
9. Utiliser le bouton « Pause » pour suspendre ou reprendre la copie.
10. À la fin, sauvegarder le mot de passe généré dans votre gestionnaire sécurisé.

## Structure du projet

- `MainWindow.xaml` / `MainWindow.xaml.cs` : Interface principale et logique métier.
- `FileCopyHandler.cs` : Gestion de la copie multithreadée et du suivi de progression.
- `NetworkShareAccesser.cs` : Connexion et déconnexion au partage réseau.
- `Dir.cs` : Modèle de dossier.
- `App.xaml` / `App.xaml.cs` : Configuration de l’application.
- `AssemblyInfo.cs` : Informations d’assemblage et thèmes.

## Sécurité

- Les mots de passe des volumes sont générés automatiquement et stockés localement.
- Les volumes VeraCrypt sont chiffrés avec AES et SHA512.
- Pensez à sauvegarder le mot de passe dans un gestionnaire sécurisé (ex : LastPass).

## Licence

Ce projet est distribué sous licence MIT.
