"""Exporte un modèle entraîné vers ONNX + config consommée par l'app .NET.

Squelette du Sprint 8 : la couture d'export est fixée (format et noms de fichiers
attendus par OnnxLandmarkDetector) ; l'architecture réelle du modèle sera branchée
au Sprint 9 lors du premier entraînement.
"""
import argparse
import json
from pathlib import Path

# Ordre des canaux de sortie = ordre des codes. À aligner avec LandmarkCatalog (.NET).
LANDMARK_CODES = [
    "S", "N", "A", "B", "D", "Pog", "Gn", "Me", "Go", "Po", "Or",
    "Ar", "Co", "ANS", "PNS", "U1E", "U1A", "L1E", "L1A",
]
INPUT_SIZE = 256


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True, help="Checkpoint entraîné (.pt)")
    parser.add_argument("--out", required=True, help="Préfixe de sortie (dossier)")
    parser.add_argument("--input-size", type=int, default=INPUT_SIZE)
    args = parser.parse_args()

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    # TODO(Sprint 9) : charger le modèle et torch.onnx.export(...) en [1,3,H,W] -> [1,C,h,w].
    #   import torch
    #   model = load_model(args.model); model.eval()
    #   dummy = torch.randn(1, 3, args.input_size, args.input_size)
    #   torch.onnx.export(model, dummy, out / "landmarks.onnx",
    #                     input_names=["input"], output_names=["heatmaps"], opset_version=17)

    config = {
        "inputWidth": args.input_size,
        "inputHeight": args.input_size,
        "codes": LANDMARK_CODES,
    }
    (out / "landmarks.model.json").write_text(json.dumps(config, indent=2), encoding="utf-8")
    print(f"Config écrite : {out / 'landmarks.model.json'}")
    print("TODO: brancher l'export du modèle .onnx (Sprint 9).")


if __name__ == "__main__":
    main()
