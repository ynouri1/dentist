# ml/ — Pipeline IA de détection des landmarks

Repo Python (séparable) pour entraîner le modèle de détection des landmarks
céphalométriques et l'exporter en ONNX, consommé par l'application .NET via
ONNX Runtime. Voir [../docs/adr-002-ia-landmarks.md](../docs/adr-002-ia-landmarks.md).

## Principe

Détection par **heatmaps** : le réseau (HRNet) produit une carte de probabilité par
landmark ; la position = argmax de la carte, remise à l'échelle de l'image. Le décodage
côté client est déjà implémenté et testé (`HeatmapDecoder`).

## Étapes

```bash
python -m venv .venv && source .venv/bin/activate   # (Windows : .venv\Scripts\activate)
pip install -r requirements.txt

python prepare_dataset.py --isbi data/isbi2015 --app-export data/app --out data/prepared
python train.py --data data/prepared --out runs/hrnet
python evaluate.py --model runs/hrnet/best.pt --data data/prepared   # MRE, SDR@2mm
python export_onnx.py --model runs/hrnet/best.pt --out ../artifacts/landmarks
```

`export_onnx.py` produit `landmarks.onnx` + `landmarks.model.json`. Déposer les deux dans
`%APPDATA%\Ortho\models\` sur le poste pour activer le bouton « Pré-placer (IA) ».

## Sources de données

- **ISBI 2015 Cephalometric X-ray Challenge** (400 images, 19 points de référence).
- **CEPHA29 / CL-Detection 2023**.
- **Export local de l'application** (`Exporter le dataset IA`) : analyses validées de
  patients consentants, anonymisées. Format : `samples/<id>/image.png` + `landmarks.json`.

## Seuil d'acceptation

MRE < 2 mm sur les landmarks majeurs, SDR@2mm ≥ 80 %. Sous ce seuil, la fonction reste en
bêta interne. `evaluate.py` produit le rapport de validation par landmark (dossier MDR).

## Format `landmarks.model.json`

```json
{ "inputWidth": 256, "inputHeight": 256, "codes": ["S", "N", "A", "B", "..."] }
```

L'ordre de `codes` correspond aux canaux de sortie du modèle (canal i ↔ codes[i]).
