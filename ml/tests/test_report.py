import json

import numpy as np

from orthoceph.metrics import per_landmark_report
from orthoceph.report import evaluate_acceptance, write_report


def _report(mre_major: float):
    # Tous les landmarks majeurs à la même erreur pour piloter le verdict.
    from orthoceph import MAJOR_LANDMARKS
    errors = {c: np.full(10, mre_major) for c in MAJOR_LANDMARKS}
    return per_landmark_report(errors)


def test_acceptance_passes_when_below_threshold():
    verdict = evaluate_acceptance(_report(1.5))
    assert verdict["passed"] is True


def test_acceptance_fails_when_mre_too_high():
    verdict = evaluate_acceptance(_report(2.5))
    assert verdict["passed"] is False


def test_write_report_emits_json_and_markdown(tmp_path):
    path = write_report(_report(1.0), str(tmp_path), "test-1.0")
    data = json.loads(path.read_text(encoding="utf-8"))
    assert data["acceptance"]["passed"] is True
    assert (tmp_path / "validation.md").exists()
