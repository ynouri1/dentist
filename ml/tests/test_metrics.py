import numpy as np

from orthoceph.metrics import mre, per_landmark_report, radial_errors_mm, sdr


def test_radial_error_converts_pixels_to_mm():
    pred = np.array([[3.0, 4.0]])
    true = np.array([[0.0, 0.0]])
    # 5 px * 0.5 mm/px = 2.5 mm
    assert radial_errors_mm(pred, true, 0.5)[0] == 2.5


def test_sdr_is_fraction_below_threshold():
    errors = np.array([1.0, 1.5, 2.5, 3.0])
    assert sdr(errors, 2.0) == 50.0  # deux valeurs < 2 mm sur quatre
    assert mre(errors) == 2.0


def test_per_landmark_report_has_overall_and_thresholds():
    report = per_landmark_report({
        "S": np.array([0.5, 1.0]),
        "N": np.array([3.0, 4.0]),
    })
    assert report["overall"]["n"] == 4
    assert "sdr@2.0mm" in report["per_landmark"]["S"]
    assert report["per_landmark"]["S"]["sdr@2.0mm"] == 100.0
    assert report["per_landmark"]["N"]["sdr@2.0mm"] == 0.0
