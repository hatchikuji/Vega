# Définition des variables
$rcloneUrl = "https://downloads.rclone.org/rclone-current-windows-amd64.zip"
$rcloneZip = "$env:TEMP\rclone.zip"
$rcloneExtractPath = "$env:TEMP\rclone"
$rcloneExe = "$rcloneExtractPath\rclone.exe"
$rcloneConfig = "$PSScriptRoot\rclone.conf"  # Assure-toi que ton script et rclone.conf sont au même endroit
$uploadSource = "C:\chemin\vers\fichiers"  # À adapter
$uploadDest = "gdrive:/dossier"  # À adapter

# Vérifie si Rclone est déjà installé
if (-Not (Test-Path $rcloneExe)) {
    Write-Host "Rclone non trouvé, téléchargement en cours..."
    Invoke-WebRequest -Uri $rcloneUrl -OutFile $rcloneZip
    Expand-Archive -Path $rcloneZip -DestinationPath $rcloneExtractPath -Force
    $rcloneExe = Get-ChildItem -Path $rcloneExtractPath -Recurse -Filter "rclone.exe" | Select-Object -ExpandProperty FullName
    Write-Host "Rclone installé temporairement dans $rcloneExtractPath"
}

# Vérifie si la config existe
if (-Not (Test-Path $rcloneConfig)) {
    Write-Host "Erreur : fichier de configuration rclone.conf introuvable. Ajoutez-le au même dossier que ce script."
    exit 1
}
