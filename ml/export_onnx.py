"""Exporte le checkpoint entraîné vers ONNX + config consommée par l'app .NET.

Produit landmarks.onnx ([1,3,H,W] -> [1,C,h,w]) + landmarks.model.json. Déposer
les deux dans %APPDATA%/Ortho/models/ pour activer le pré-placement IA.
"""
import argparse
import json
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True, help="Checkpoint .pt")
    parser.add_argument("--out", required=True, help="Dossier de sortie")
    args = parser.parse_args()

    import torch

    from orthoceph import LANDMARK_CODES
    from orthoceph.model import HeatmapNet

    checkpoint = torch.load(args.model, map_location="cpu")
    in_w, in_h = checkpoint["input_wh"]
    model = HeatmapNet(len(LANDMARK_CODES))
    model.load_state_dict(checkpoint["state_dict"])
    model.eval()

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    dummy = torch.randn(1, 3, in_h, in_w)
    torch.onnx.export(
        model, dummy, out / "landmarks.onnx",
        input_names=["input"], output_names=["heatmaps"],
        dynamic_axes=None, opset_version=17)

    (out / "landmarks.model.json").write_text(json.dumps({
        "inputWidth": in_w, "inputHeight": in_h, "codes": LANDMARK_CODES,
    }, indent=2), encoding="utf-8")
    print(f"Exporté : {out / 'landmarks.onnx'} + landmarks.model.json")


if __name__ == "__main__":
    main()
