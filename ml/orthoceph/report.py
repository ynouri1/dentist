"""Rapport de validation du modèle : JSON + Markdown, avec verdict vs seuil clinique.

Seuil d'acceptation (ADR-002) : MRE < 2 mm et SDR@2mm >= 80 % sur les landmarks
majeurs. En dessous, le modèle reste en bêta interne (non proposé en cabinet).
"""
from __future__ import annotations

import json
from pathlib import Path

from . import MAJOR_LANDMARKS

MRE_THRESHOLD_MM = 2.0
SDR2_THRESHOLD_PCT = 80.0


def evaluate_acceptance(report: dict) -> dict:
    """Applique le seuil clinique aux landmarks majeurs présents dans le rapport."""
    majors = {c: report["per_landmark"][c] for c in MAJOR_LANDMARKS if c in report["per_landmark"]}
    if not majors:
        return {"passed": False, "reason": "Aucun landmark majeur évalué."}

    worst_mre = max(m["mre_mm"] for m in majors.values())
    worst_sdr = min(m["sdr@2.0mm"] for m in majors.values())
    passed = worst_mre < MRE_THRESHOLD_MM and worst_sdr >= SDR2_THRESHOLD_PCT
    return {
        "passed": passed,
        "worst_major_mre_mm": worst_mre,
        "worst_major_sdr@2mm": worst_sdr,
        "mre_threshold_mm": MRE_THRESHOLD_MM,
        "sdr2_threshold_pct": SDR2_THRESHOLD_PCT,
    }


def write_report(report: dict, out_dir: str, model_version: str) -> Path:
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    acceptance = evaluate_acceptance(report)
    full = {"modelVersion": model_version, "acceptance": acceptance, **report}

    (out / "validation.json").write_text(json.dumps(full, indent=2), encoding="utf-8")

    lines = [
        f"# Rapport de validation — modèle {model_version}",
        "",
        f"**Verdict : {'✅ ACCEPTÉ' if acceptance['passed'] else '❌ REFUSÉ'}** "
        f"(seuil : MRE < {MRE_THRESHOLD_MM} mm et SDR@2mm ≥ {SDR2_THRESHOLD_PCT}% sur les landmarks majeurs)",
        "",
        f"- MRE globale : **{report['overall']['mre_mm']} mm**",
        f"- SDR@2mm globale : **{report['overall']['sdr@2.0mm']}%**",
        "",
        "## Par landmark",
        "",
        "| Point | n | MRE (mm) | SDR@2mm | SDR@2.5mm | SDR@3mm | SDR@4mm |",
        "|-------|---|----------|---------|-----------|---------|---------|",
    ]
    for code, m in report["per_landmark"].items():
        major = " *" if code in MAJOR_LANDMARKS else ""
        lines.append(
            f"| {code}{major} | {m['n']} | {m['mre_mm']} | {m['sdr@2.0mm']}% | "
            f"{m['sdr@2.5mm']}% | {m['sdr@3.0mm']}% | {m['sdr@4.0mm']}% |")
    lines += ["", "_\\* landmark majeur (soumis au seuil d'acceptation)._"]

    (out / "validation.md").write_text("\n".join(lines), encoding="utf-8")
    return out / "validation.json"
