"""Chargement du dataset unifié et adaptateurs de sources.

Format unifié (produit par prepare_dataset.py) : un dossier par échantillon avec
image.png + landmarks.json ({imageWidth, imageHeight, pixelSpacingMm, points:[{code,x,y}]}).
C'est EXACTEMENT le format exporté par l'application (TrainingDataExporter), donc
les données locales du cabinet s'intègrent sans conversion (mitigation R4).
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

import numpy as np

from . import LANDMARK_CODES


@dataclass
class Sample:
    image_path: Path
    image_wh: tuple[int, int]
    mm_per_px: float | None
    points: dict[str, tuple[float, float]]  # code -> (x, y) en pixels image

    def target_array(self) -> np.ndarray:
        """(len(LANDMARK_CODES), 2) ; NaN pour les points absents de cet échantillon."""
        out = np.full((len(LANDMARK_CODES), 2), np.nan, dtype=np.float32)
        for i, code in enumerate(LANDMARK_CODES):
            if code in self.points:
                out[i] = self.points[code]
        return out


def load_sample(sample_dir: Path) -> Sample:
    meta = json.loads((sample_dir / "landmarks.json").read_text(encoding="utf-8"))
    points = {p["code"]: (float(p["x"]), float(p["y"])) for p in meta["points"]}
    return Sample(
        image_path=sample_dir / "image.png",
        image_wh=(int(meta["imageWidth"]), int(meta["imageHeight"])),
        mm_per_px=meta.get("pixelSpacingMm"),
        points=points,
    )


def load_dataset(prepared_dir: str) -> list[Sample]:
    root = Path(prepared_dir) / "samples"
    if not root.exists():
        return []
    return [load_sample(d) for d in sorted(root.iterdir()) if (d / "landmarks.json").exists()]
