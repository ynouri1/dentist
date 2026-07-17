"""Métriques de validation du modèle (NumPy pur).

- MRE : erreur radiale moyenne (mm) entre point prédit et point de référence.
- SDR@t : taux de détections réussies à moins de t mm (Successful Detection Rate).

Ces métriques alimentent le rapport de validation, pièce du futur dossier MDR (R6).
"""
from __future__ import annotations

import numpy as np


def radial_errors_mm(pred_xy: np.ndarray, true_xy: np.ndarray, mm_per_px: float) -> np.ndarray:
    """Distances euclidiennes prédiction/référence, converties en mm."""
    diff = (pred_xy - true_xy) * mm_per_px
    return np.sqrt(np.sum(diff ** 2, axis=-1))


def mre(errors_mm: np.ndarray) -> float:
    return float(np.mean(errors_mm)) if errors_mm.size else float("nan")


def sdr(errors_mm: np.ndarray, threshold_mm: float) -> float:
    """Fraction des erreurs strictement sous le seuil, en pourcentage."""
    if errors_mm.size == 0:
        return float("nan")
    return float(np.mean(errors_mm < threshold_mm) * 100.0)


def per_landmark_report(errors_by_landmark: dict[str, np.ndarray],
                        thresholds_mm=(2.0, 2.5, 3.0, 4.0)) -> dict:
    """Rapport structuré par landmark + agrégat, prêt à sérialiser en JSON."""
    landmarks = {}
    all_errors = []
    for code, errors in errors_by_landmark.items():
        all_errors.append(errors)
        landmarks[code] = {
            "n": int(errors.size),
            "mre_mm": round(mre(errors), 3),
            **{f"sdr@{t}mm": round(sdr(errors, t), 1) for t in thresholds_mm},
        }
    stacked = np.concatenate(all_errors) if all_errors else np.array([])
    overall = {
        "n": int(stacked.size),
        "mre_mm": round(mre(stacked), 3),
        **{f"sdr@{t}mm": round(sdr(stacked, t), 1) for t in thresholds_mm},
    }
    return {"overall": overall, "per_landmark": landmarks}
