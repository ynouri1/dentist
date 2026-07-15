# Corpus DICOM réel

Déposez ici des fichiers `.dcm` **anonymisés** provenant de céphalostats et
panoramiques réels (cabinets tunisiens en priorité — risque R1 du registre).

Le test `ImagingTests.Real_dicom_corpus_decodes_when_present` décode
automatiquement tous les fichiers de ce dossier : chaque fichier qui échoue
révèle une incompatibilité à corriger **avant** qu'un client ne la rencontre.

Règles :
- Anonymisation obligatoire (aucun nom, date de naissance ou identifiant réel).
- Un fichier par appareil/format rencontré suffit.
- Nommer les fichiers `<marque>-<modalite>.dcm` (ex. `carestream-pano.dcm`).
