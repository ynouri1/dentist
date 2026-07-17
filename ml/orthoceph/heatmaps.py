"""Encodage/décodage des heatmaps de landmarks (NumPy pur).

Le décodage reproduit EXACTEMENT HeatmapDecoder.cs côté .NET : argmax en
ordre ligne-major (index = y*W + x), point remis à l'échelle image au centre
de la cellule ((hx+0.5)/W*imgW, (hy+0.5)/H*imgH). Un écart ici casserait la
correspondance entre l'entraînement et l'inférence embarquée.
"""
from __future__ import annotations

import numpy as np


def encode(points_xy: np.ndarray, image_wh: tuple[int, int],
           heatmap_wh: tuple[int, int], sigma: float = 2.0) -> np.ndarray:
    """(N,2) points image -> (N,H,W) heatmaps gaussiennes. NaN => canal nul (point absent)."""
    img_w, img_h = image_wh
    hm_w, hm_h = heatmap_wh
    n = points_xy.shape[0]
    heatmaps = np.zeros((n, hm_h, hm_w), dtype=np.float32)

    ys, xs = np.mgrid[0:hm_h, 0:hm_w]
    for i in range(n):
        px, py = points_xy[i]
        if not np.isfinite(px) or not np.isfinite(py):
            continue
        cx = px / img_w * hm_w
        cy = py / img_h * hm_h
        heatmaps[i] = np.exp(-((xs - cx) ** 2 + (ys - cy) ** 2) / (2 * sigma ** 2))
    return heatmaps


def decode(heatmaps: np.ndarray, image_wh: tuple[int, int]) -> tuple[np.ndarray, np.ndarray]:
    """(C,H,W) -> ((C,2) points image, (C,) confiances). Identique à HeatmapDecoder.cs."""
    img_w, img_h = image_wh
    c, hm_h, hm_w = heatmaps.shape
    points = np.empty((c, 2), dtype=np.float64)
    confidences = np.empty(c, dtype=np.float64)

    for i in range(c):
        flat = heatmaps[i].reshape(-1)
        idx = int(np.argmax(flat))
        hx = idx % hm_w
        hy = idx // hm_w
        points[i, 0] = (hx + 0.5) * img_w / hm_w
        points[i, 1] = (hy + 0.5) * img_h / hm_h
        confidences[i] = float(np.clip(flat[idx], 0.0, 1.0))
    return points, confidences
