# Ortho — Manuel utilisateur

## Premier lancement

1. Lancez **Ortho**. L'écran « Bienvenue » vous invite à créer le compte praticien : identifiant (3 caractères min.), nom affiché, rôle et mot de passe (8 caractères min.).
2. Un **code de secours** (format `XXXX-XXXX-XXXX`) s'affiche **une seule fois** : notez-le et conservez-le en lieu sûr. C'est la seule façon de réinitialiser un mot de passe oublié.

Toutes les données (base, images, documents) sont **chiffrées** sur le poste et liées à votre session Windows.

## Connexion et sécurité

- **Connexion** : identifiant + mot de passe.
- **Verrouillage** : bouton *Verrouiller*, ou automatique après 15 minutes d'inactivité. Le mot de passe est exigé pour reprendre.
- **Mot de passe oublié ?** (écran de connexion) : identifiant + code de secours + nouveau mot de passe. Un **nouveau** code de secours vous est remis (l'ancien ne fonctionne plus).

## Dossiers patients

- **Nouveau patient** : nom et prénom obligatoires ; le numéro de dossier (`P-2026-0001`) est attribué automatiquement.
- **Recherche** : nom, prénom, numéro de dossier ou téléphone.
- Onglet **Consultations** : ajoutez date, motif, notes cliniques.
- Onglet **Documents et photos** : importez et catégorisez (photo intra/extra-orale, radiographie, examen). Suppression avec confirmation.

## Imagerie

- **Importer des images…** : DICOM (`.dcm`), JPG, PNG, TIFF. L'original est conservé intact et chiffré.
- **Calibration** : automatique pour les DICOM qui l'embarquent ; sinon panneau *Calibration* (longueur connue en mm / longueur mesurée en pixels) ou outil **Règle étalon** du viewer (tracez 2 points sur une distance connue puis saisissez les mm). Sans calibration, les mesures s'affichent en pixels.
- **Viewer** (double-clic sur une image) : zoom molette, déplacement, rotation, contraste/luminosité, mesures de **distances** et d'**angles**, annotations (trait, flèche, texte). `Échap` annule une mesure en cours. Tout est conservé, l'image d'origine n'est jamais modifiée.

## Céphalométrie

1. Sélectionnez la téléradiographie → **Céphalométrie**.
2. Choisissez l'analyse : **Steiner, Ricketts, Tweed, McNamara, Downs, Jarabak**.
3. Cliquez sur l'image pour placer chaque point (la liste vous guide ; **loupe** de précision au survol). Un point placé se **glisse** pour être repositionné ; *Annuler/Rétablir* disponibles.
4. Les mesures se calculent en direct : vert = dans la norme, orange = à la limite (1–2 σ), rouge = hors norme (> 2 σ).
5. Les points déjà placés sont **réutilisés automatiquement** si vous ouvrez une autre analyse sur la même image.

## Superposition T0/T1

Onglet Imagerie → **Superposition T0/T1** : choisissez l'analyse et les deux images (chacune doit avoir S et N placés). Les tracés s'affichent recalés sur S–N : T0 en cyan, T1 en orange.

## Rapport PDF

Dans la fenêtre Céphalométrie → **Rapport PDF** : le document (identité, image tracée, tableau des mesures, interprétation) est **archivé automatiquement dans le dossier patient** et peut être exporté en fichier.

## Sauvegarde et restauration

- **Sauvegarder les données** (barre principale) : archive zip complète (chiffrée) dans `Documents\Ortho Sauvegardes`. À copier régulièrement sur un disque externe.
- **Restaurer une sauvegarde…** (écran de connexion) : remplace toutes les données actuelles par celles de l'archive ; un instantané de sécurité est pris avant. ⚠ La restauration doit se faire **sur le même compte Windows** (les clés de chiffrement y sont liées).
- Un instantané de la base est aussi pris automatiquement avant chaque mise à jour du logiciel.

## En cas de problème

**Exporter le diagnostic** (barre principale) : produit un zip des journaux techniques — **aucune donnée patient** — à transmettre au support. Les données vivent dans `%APPDATA%\Ortho`.
