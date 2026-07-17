# ADR-002 — Détection automatique des landmarks (IA)

**Statut** : décidé (spike Sprint 8) · **Risque adressé** : R4 (manque de données locales)

## Décision d'architecture

- **Entraînement** hors client, en Python/PyTorch (repo `ml/`), sur **heatmaps** (HRNet /
  MMPose). Chaque landmark = un canal de sortie ; la position = l'argmax de la carte.
- **Inférence embarquée** dans l'application via **ONNX Runtime** (CPU par défaut) : le modèle
  entraîné est exporté en `.onnx` et déposé dans `%APPDATA%\Ortho\models\`. **Aucune dépendance
  Python chez le client.** En l'absence de modèle, le placement reste 100 % manuel.
- **Le praticien valide toujours** : l'IA *pré-place*, ne décide jamais. Chaque point porte sa
  provenance (`Ai` + score de confiance) — traçabilité exigée pour le futur dossier MDR.

Coutures déjà livrées au Sprint 8 : `ILandmarkDetector`, `HeatmapDecoder` (testé),
`OnnxLandmarkDetector`, le bouton « Pré-placer (IA) », et l'export de dataset anonymisé
(`TrainingDataExporter`, patients consentants uniquement).

## Seuil d'acceptation clinique

Métrique : **erreur radiale moyenne (MRE)** entre point prédit et point de référence, en mm.

- **Objectif MVP IA** : MRE < **2 mm** sur les landmarks majeurs (S, N, A, B, Go, Me, Or, Po),
  et **taux de détections réussies < 2 mm ≥ 80 %** (SDR@2mm), aligné sur l'état de l'art
  (ISBI 2015). En dessous, la fonction reste en bêta interne (non proposée en cabinet).
- Chaque livraison de modèle produit un **rapport de validation par landmark** (MRE, SDR@2mm,
  SDR@2.5/3/4mm) — pièce du dossier réglementaire.

## Données

1. **Amorçage** — datasets publics : **ISBI 2015 Cephalometric Challenge** (400 images, 19 pts),
   **CEPHA29 / CL-Detection 2023**. Servent au modèle baseline et à fixer la barre.
2. **Fine-tuning local** — les analyses **validées** dans le MVP deviennent le dataset local
   (appareils et population tunisiens), via l'export anonymisé (consentement obligatoire).
   C'est la sortie de R4 : plus le logiciel est utilisé, meilleur devient le modèle.

## Pipeline (repo `ml/`)

`prepare_dataset.py` (public + export app → format commun) → `train.py` (HRNet, heatmaps) →
`evaluate.py` (MRE, SDR, rapport) → `export_onnx.py` (→ `landmarks.onnx` + `landmarks.model.json`).

## Prochaines actions (hors code, côté équipe)

- Télécharger ISBI 2015 + CL-Detection 2023.
- Entraîner le baseline sur GPU, produire le premier rapport de validation.
- Faire valider le seuil (< 2 mm) et les normes céphalométriques par l'orthodontiste référent.
