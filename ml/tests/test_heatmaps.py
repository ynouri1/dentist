"""Tests NumPy purs — exécutables sans framework DL."""
import numpy as np

from orthoceph.heatmaps import decode, encode


def test_decode_matches_dotnet_convention():
    # Heatmap 4x4, pic en cellule (col 3, ligne 1), image 400x400.
    hm = np.zeros((1, 4, 4), dtype=np.float32)
    hm[0, 1, 3] = 0.9
    points, conf = decode(hm, (400, 400))
    assert points[0, 0] == 350.0  # (3+0.5)/4*400
    assert points[0, 1] == 150.0  # (1+0.5)/4*400
    assert abs(conf[0] - 0.9) < 1e-6


def test_encode_peak_is_near_true_point():
    pts = np.array([[100.0, 200.0]], dtype=np.float32)
    hm = encode(pts, image_wh=(400, 400), heatmap_wh=(64, 64), sigma=2.0)
    back, _ = decode(hm, (400, 400))
    # L'aller-retour encode->decode retrouve le point à la résolution de la heatmap près.
    assert abs(back[0, 0] - 100.0) < 400 / 64
    assert abs(back[0, 1] - 200.0) < 400 / 64


def test_absent_point_gives_empty_channel():
    pts = np.array([[np.nan, np.nan]], dtype=np.float32)
    hm = encode(pts, image_wh=(100, 100), heatmap_wh=(16, 16))
    assert hm.sum() == 0.0
