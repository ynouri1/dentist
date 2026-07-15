# Découpage en sprints

> Hypothèses : équipe de 2–3 développeurs + 1 orthodontiste référent (temps partiel). Sprints de **2 semaines**. Architecture de référence : [architecture.md](architecture.md).
>
> Cadence : démo à chaque fin de sprint devant l'orthodontiste référent. Le MVP (P0) vise **~5 mois** (Sprints 0–7), pilote en cabinet inclus.

## Phase P0 — MVP (Sprints 0 à 7)

### Sprint 0 — Fondations (semaines 1–2)
**Objectif : un squelette qui compile, se déploie et persiste des données.**
- Mise en place du repo, CI (build + tests), packaging Windows (installeur MSIX ou Inno Setup).
- Solution .NET 10 : Domain / Application / Infrastructure / UI (Avalonia + CommunityToolkit.Mvvm).
- EF Core + SQLite chiffré (SQLCipher) : entités Patient/Consultation, migrations, sauvegarde auto.
- `IObjectStore` fichiers local (arborescence par patient, chiffrement AES).
- Serilog + journal d'audit minimal.
- **Actions risque** : lancement de la collecte de fichiers DICOM réels auprès de 3–5 cabinets (R1) ; registre des licences (R8).
- ✅ *Démo : créer un patient, le retrouver après redémarrage, données chiffrées sur disque.*

### Sprint 1 — Gestion des patients (semaines 3–4)
**Objectif : le module patients est utilisable au quotidien.**
- CRUD complet dossiers patients + recherche (nom, téléphone, n° dossier).
- Historique des consultations (notes datées).
- Rattachement de documents et photos intra/extra-orales (import, vignettes, galerie).
- Authentification applicative (login praticien/assistante), verrouillage de session (R7).
- Chaînes en `.resx` (français) dès maintenant.
- ✅ *Démo : parcours complet réception d'un patient avec photos.*

### Sprint 2 — Import d'imagerie (semaines 5–6)
**Objectif : toute image utile entre dans le logiciel.**
- Import DICOM via fo-dicom : parsing, métadonnées, extraction pixel data, fenêtrage.
- Suite de tests sur le **corpus réel** collecté au Sprint 0 (R1).
- Import JPG/PNG/TIFF (ImageSharp), rattachement à un patient/étude.
- **Calibration** : lecture pixel spacing DICOM + calibration manuelle par règle étalon pour les images non-DICOM (R2).
- Fallback documenté quand un DICOM est illisible.
- ✅ *Démo : import d'une téléradiographie DICOM d'un céphalostat tunisien.*

### Sprint 3 — Visualisation 2D (semaines 7–8)
**Objectif : un viewer fluide, au clavier et à la souris.**
- Canvas SkiaSharp : zoom (molette), pan, rotation, ajustement contraste/luminosité (fenêtrage).
- Outils de mesure : distance (calibrée en mm), angle 3 points.
- Annotations (texte, flèches, traits), persistées séparément de l'image (jamais de modification du fichier source).
- Performance : image 4K fluide sur un PC de cabinet moyen de gamme.
- ✅ *Démo : mesures sur radio réelle, valeurs vérifiées par l'orthodontiste référent.*

### Sprint 4 — Moteur céphalométrique (semaines 9–10)
**Objectif : le cœur métier, exact et testé.**
- Modèle déclaratif `ANALYSIS_TEMPLATE` : landmarks → mesures (angles, distances, ratios) → normes ± écart-type.
- Implémentation **Steiner + Tweed** (les plus demandées) de bout en bout.
- Placement manuel des landmarks : loupe de précision, undo/redo, repositionnement.
- Calcul automatique des mesures en temps réel, tableau comparatif aux normes.
- **Tests unitaires exhaustifs contre des cas publiés dans la littérature** (R2).
- ✅ *Démo : analyse Steiner complète en < 10 min, validée cliniquement.*

### Sprint 5 — Les 6 analyses + superposition (semaines 11–12)
**Objectif : couverture fonctionnelle céphalométrique complète.**
- Ajout Ricketts, McNamara, Downs, Jarabak (données déclaratives + tests).
- Tracé céphalométrique (lignes/plans dessinés automatiquement à partir des landmarks).
- Superposition de deux tracés (T0/T1) avec recalage sur structures stables.
- Bibliothèque de landmarks partagée entre analyses (placer une fois, réutiliser partout).
- ✅ *Démo : même patient analysé selon les 6 écoles, superposition début/fin de traitement.*

### Sprint 6 — Rapport PDF + finitions (semaines 13–14)
**Objectif : le livrable que l'orthodontiste remet ou archive.**
- Templates QuestPDF : identité patient, image annotée, tracé, tableau mesures vs normes, interprétation succincte, en-tête cabinet personnalisable.
- Export PDF + archivage automatique dans le dossier patient.
- Écran de comparaison aux normes avec code couleur (dans la norme / limite / hors norme).
- Passe UX complète avec l'orthodontiste référent ; raccourcis clavier.
- ✅ *Démo : du DICOM au PDF remis au patient en une séance.*

### Sprint 7 — Durcissement + pilote (semaines 15–16)
**Objectif : mise en production chez 2–3 cabinets pilotes.**
- Sauvegarde/restauration chiffrée (disque externe), migration de schéma testée (R10).
- Récupération de mot de passe oublié (constaté en test interne : le chiffrement étant découplé du mot de passe, une réinitialisation sécurisée locale est possible — code de secours imprimé à la création du compte, par exemple).
- Installeur final, mise à jour in-app, manuel utilisateur FR.
- Crash reporting local + export de diagnostic.
- Correction des retours pilotes, gel du périmètre P0.
- 🎯 **Jalon MVP : 3 cabinets utilisent le logiciel sur de vrais patients.**

## Phase P1 — IA + STL (Sprints 8 à 12, ~2,5 mois)

### Sprint 8 — Spikes techniques (décisions avant investissement)
- **Spike 3D (R3)** : ActiViz payant vs C++/CLI + VTK vs vtk.js en WebView vs HelixToolkit → décision documentée.
- **Spike IA (R4)** : entraînement baseline HRNet/MMPose sur ISBI 2015 + CL-Detection 2023, mesure de l'erreur radiale moyenne ; définition du seuil d'acceptation clinique (< 2 mm sur les landmarks majeurs).
- Mise en place du pipeline d'annotation : les analyses validées au MVP deviennent des données d'entraînement (consentement + anonymisation).

### Sprint 9 — Pipeline IA industrialisé
- Pipeline Python reproductible : data versioning (DVC), entraînement, évaluation, export ONNX.
- Fine-tuning sur les données locales collectées.
- Rapport de validation du modèle (par landmark : erreur moyenne, taux < 2 mm) — base du dossier MDR futur (R6).

### Sprint 10 — Intégration IA dans le client
- Inférence ONNX Runtime embarquée (CPU, DirectML si GPU).
- UX de validation : landmarks proposés avec score de confiance, code couleur, correction manuelle obligatoire avant calcul (le praticien reste décisionnaire — positionnement réglementaire).
- Provenance IA/manuel tracée en base.
- ✅ *Démo : analyse Steiner en < 2 min avec pré-placement IA.*

### Sprints 11–12 — STL Viewer
- Import STL, rendu 3D (techno issue du spike Sprint 8) : rotation, zoom, éclairage.
- Mesures 3D (distances, sections), comparaison côte à côte de deux modèles.
- Superposition de deux maillages avec recalage ICP (début de la brique "fusion" de P2).

## Phase P2 — CBCT (Sprints 13 à 17, ordre de grandeur)
- S13–14 : chargement de volumes CBCT (DICOM multi-frame), MPR (coupes axiales/sagittales/coronales), fenêtrage — attention performance (R5).
- S15–16 : rendu volumique GPU (VTK), presets tissus/os.
- S17 : fusion STL + CBCT — recalage automatique (Elastix) + ajustement manuel 6 DDL.

## Phase P3 — Set-up et aligneurs (exploratoire, ne pas planifier finement avant fin P2)
- Segmentation des dents sur maillage (R&D : MeshSegNet/TSegNet, Open3D).
- Déplacement individuel des dents, détection de collisions.
- Calcul des étapes intermédiaires, export STL par étape.
- ⚠ Cette phase fait basculer le produit vers un dispositif médical de classe supérieure : instruire le volet réglementaire (R6) **avant** le développement.

## Fil rouge transverse (toutes phases)
- Registre des risques et des licences mis à jour à chaque sprint.
- Audit trail et traçabilité maintenus sur toute nouvelle fonctionnalité (préparation MDR).
- Corpus de tests DICOM/images enrichi en continu avec les fichiers problématiques rencontrés en cabinet.
