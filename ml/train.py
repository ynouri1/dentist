"""Entraînement du modèle de heatmaps (PyTorch). S'exécute sur la machine ML.

Reproductible via params.yaml + DVC (voir dvc.yaml). Sauvegarde le meilleur
checkpoint dans {out}/best.pt.
"""
import argparse
from pathlib import Path

import numpy as np
import yaml


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--params", default="params.yaml")
    args = parser.parse_args()

    params = yaml.safe_load(Path(args.params).read_text(encoding="utf-8"))["train"]

    import torch
    from torch.utils.data import DataLoader, Dataset
    from PIL import Image

    from orthoceph import LANDMARK_CODES
    from orthoceph.dataset import load_dataset
    from orthoceph.heatmaps import encode
    from orthoceph.model import HeatmapNet

    torch.manual_seed(params["seed"])
    np.random.seed(params["seed"])
    in_w, in_h = params["input_width"], params["input_height"]
    hm_w, hm_h = in_w // 2, in_h // 2

    samples = load_dataset(args.data)
    if not samples:
        raise SystemExit(f"Aucun échantillon dans {args.data} — lancez prepare_dataset.py d'abord.")

    class CephDataset(Dataset):
        def __len__(self):
            return len(samples)

        def __getitem__(self, i):
            s = samples[i]
            with Image.open(s.image_path).convert("RGB") as im:
                im = im.resize((in_w, in_h))
                x = np.asarray(im, dtype=np.float32).transpose(2, 0, 1) / 255.0
            target = encode(s.target_array(), s.image_wh, (hm_w, hm_h), sigma=params["sigma"])
            return torch.from_numpy(x), torch.from_numpy(target)

    loader = DataLoader(CephDataset(), batch_size=params["batch_size"], shuffle=True)
    device = "cuda" if torch.cuda.is_available() else "cpu"
    model = HeatmapNet(len(LANDMARK_CODES)).to(device)
    optimizer = torch.optim.Adam(model.parameters(), lr=params["lr"])
    loss_fn = torch.nn.MSELoss()

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    best = float("inf")
    for epoch in range(params["epochs"]):
        model.train()
        epoch_loss = 0.0
        for x, target in loader:
            x, target = x.to(device), target.to(device)
            optimizer.zero_grad()
            loss = loss_fn(model(x), target)
            loss.backward()
            optimizer.step()
            epoch_loss += loss.item()
        epoch_loss /= len(loader)
        print(f"epoch {epoch + 1}/{params['epochs']}  loss={epoch_loss:.5f}")
        if epoch_loss < best:
            best = epoch_loss
            torch.save({"state_dict": model.state_dict(), "codes": LANDMARK_CODES,
                        "input_wh": [in_w, in_h]}, out / "best.pt")
    print(f"Meilleur checkpoint : {out / 'best.pt'} (loss={best:.5f})")


if __name__ == "__main__":
    main()
