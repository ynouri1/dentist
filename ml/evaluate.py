"""Évaluation du modèle : MRE, SDR@t par landmark + rapport de validation.

Produit reports/validation.json et validation.md, avec verdict vs seuil clinique
(ADR-002). S'exécute sur la machine ML (PyTorch).
"""
import argparse
from pathlib import Path

import numpy as np


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True)
    parser.add_argument("--data", required=True)
    parser.add_argument("--out", default="reports")
    parser.add_argument("--version", default="dev")
    parser.add_argument("--default-mm-per-px", type=float, default=0.1)
    args = parser.parse_args()

    import torch
    from PIL import Image

    from orthoceph import LANDMARK_CODES
    from orthoceph.dataset import load_dataset
    from orthoceph.heatmaps import decode
    from orthoceph.metrics import radial_errors_mm, per_landmark_report
    from orthoceph.model import HeatmapNet
    from orthoceph.report import write_report

    checkpoint = torch.load(args.model, map_location="cpu")
    in_w, in_h = checkpoint["input_wh"]
    model = HeatmapNet(len(LANDMARK_CODES))
    model.load_state_dict(checkpoint["state_dict"])
    model.eval()

    errors: dict[str, list[float]] = {c: [] for c in LANDMARK_CODES}
    for s in load_dataset(args.data):
        with Image.open(s.image_path).convert("RGB") as im:
            x = np.asarray(im.resize((in_w, in_h)), dtype=np.float32).transpose(2, 0, 1) / 255.0
        with torch.no_grad():
            heatmaps = model(torch.from_numpy(x)[None])[0].numpy()
        pred, _ = decode(heatmaps, s.image_wh)
        truth = s.target_array()
        mm_per_px = s.mm_per_px or args.default_mm_per_px

        for i, code in enumerate(LANDMARK_CODES):
            if np.all(np.isfinite(truth[i])):
                err = radial_errors_mm(pred[i][None], truth[i][None], mm_per_px)[0]
                errors[code].append(float(err))

    errors_np = {c: np.asarray(v) for c, v in errors.items() if v}
    report = per_landmark_report(errors_np)
    path = write_report(report, args.out, args.version)
    print(f"Rapport de validation : {path}")
    print(f"MRE globale : {report['overall']['mre_mm']} mm | SDR@2mm : {report['overall']['sdr@2.0mm']}%")


if __name__ == "__main__":
    main()
