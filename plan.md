# Projet : Logiciel d'orthodontie pour le marché tunisien

## Vision

Créer une alternative locale à OnyxCeph destinée aux orthodontistes
tunisiens, puis étendre le produit au Maghreb.

## Objectifs du MVP

-   Réduire le temps d'analyse céphalométrique.
-   Fournir un outil simple et abordable.
-   Offrir une interface en français avec possibilité d'arabe
    ultérieurement.

## Fonctionnalités P0 (MVP)

### 1. Gestion des patients

-   Création et modification des dossiers patients
-   Historique des consultations
-   Stockage des examens et documents
-   Photos intra-orales et extra-orales

### 2. Import d'imagerie

-   DICOM
-   JPG
-   PNG
-   TIFF

### 3. Visualisation 2D

-   Zoom
-   Rotation
-   Contraste et luminosité
-   Mesure de distances
-   Mesure d'angles
-   Annotations

### 4. Céphalométrie manuelle

Analyses à supporter : - Steiner - Ricketts - Tweed - McNamara - Downs -
Jarabak

Fonctions : - Placement manuel des landmarks - Calcul automatique des
mesures - Superposition des tracés

### 5. Rapport PDF

-   Résumé des mesures
-   Comparaison aux normes
-   Export PDF

## Fonctionnalités P1

### IA Céphalométrique

-   Détection automatique des landmarks
-   Score de confiance
-   Validation manuelle par l'orthodontiste

### STL Viewer

-   Import STL
-   Rotation
-   Zoom
-   Mesures
-   Comparaison des modèles

## Fonctionnalités P2

### CBCT Viewer

-   Reconstruction volumique
-   Coupes axiales
-   Coupes sagittales
-   Coupes coronales

### Fusion STL + CBCT

-   Alignement automatique
-   Ajustement manuel

## Fonctionnalités P3

### Set-up orthodontique

-   Segmentation des dents
-   Déplacement individuel
-   Contrôle des collisions

### Génération des aligneurs

-   Définition de la position finale
-   Calcul des étapes intermédiaires
-   Export STL pour chaque étape

## Architecture technique proposée

### Frontend

-   C# .NET
-   WPF ou Avalonia

### Imagerie médicale

-   fo-dicom
-   VTK

### IA

-   Python
-   PyTorch

### Base de données

-   PostgreSQL

### Stockage

-   MinIO ou S3 compatible

## Contraintes

-   Fonctionnement local possible sans cloud.
-   Respect des exigences de confidentialité médicale.
-   Prévoir la conformité MDR à moyen terme.

## Livrables attendus pour le MVP

1.  Import DICOM.
2.  Visualisation 2D.
3.  Céphalométrie manuelle.
4.  Génération de rapport PDF.
5.  Gestion des patients.

## Objectif commercial

-   Lancement en Tunisie.
-   Extension vers l'Algérie puis le Maroc.
