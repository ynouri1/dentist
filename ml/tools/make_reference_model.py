"""Construit un petit modèle ONNX DÉTERMINISTE pour la validation croisée .NET.

Ce n'est PAS un modèle entraîné : ses heatmaps ont un pic à une cellule connue
par canal, ce qui rend l'inférence prévisible et permet au test d'intégration
.NET de vérifier que OnnxLandmarkDetector charge et infère réellement un modèle
au bon contrat d'I/O ([1,3,H,W] -> [1,C,h,w]).

Dépendances légères : onnx + numpy (pas de torch).
"""
import argparse
import json
from pathlib import Path

import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper

# Sous-ensemble suffisant pour le test .NET (ordre = canaux).
CODES = ["S", "N", "A", "B", "Go", "Me", "Or", "Po"]
IN_W = IN_H = 256
HM_W = HM_H = 64


def peak_cell(channel: int) -> tuple[int, int]:
    """Cellule (col, ligne) du pic pour un canal donné — déterministe."""
    return (channel * 7) % HM_W, (channel * 5) % HM_H


def build() -> onnx.ModelProto:
    c = len(CODES)
    heatmaps = np.zeros((1, c, HM_H, HM_W), dtype=np.float32)
    for ch in range(c):
        col, row = peak_cell(ch)
        heatmaps[0, ch, row, col] = 1.0

    # input (non entraîné) consommé puis annulé, pour un graphe valide qui « utilise »
    # bien l'entrée : out = const_heatmaps + 0 * reduce_mean(input).
    inp = helper.make_tensor_value_info("input", TensorProto.FLOAT, [1, 3, IN_H, IN_W])
    out = helper.make_tensor_value_info("heatmaps", TensorProto.FLOAT, [1, c, HM_H, HM_W])

    const_hm = helper.make_node(
        "Constant", [], ["const_hm"],
        value=numpy_helper.from_array(heatmaps, name="const_hm_val"))
    mean = helper.make_node("ReduceMean", ["input"], ["mean"], keepdims=0)
    zero = helper.make_node(
        "Constant", [], ["zero"],
        value=numpy_helper.from_array(np.array(0.0, dtype=np.float32), name="zero_val"))
    scaled = helper.make_node("Mul", ["mean", "zero"], ["scaled"])
    add = helper.make_node("Add", ["const_hm", "scaled"], ["heatmaps"])

    graph = helper.make_graph(
        [const_hm, mean, zero, scaled, add], "reference_landmarks", [inp], [out])
    model = helper.make_model(graph, opset_imports=[helper.make_operatorsetid("", 17)])
    model.ir_version = 10
    onnx.checker.check_model(model)
    return model


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    onnx.save(build(), out / "landmarks.onnx")
    (out / "landmarks.model.json").write_text(json.dumps({
        "inputWidth": IN_W, "inputHeight": IN_H, "codes": CODES,
    }, indent=2), encoding="utf-8")

    # Positions attendues (pour l'assertion .NET), à titre indicatif.
    expected = {code: [round((peak_cell(i)[0] + 0.5) * IN_W / HM_W, 4),
                       round((peak_cell(i)[1] + 0.5) * IN_H / HM_H, 4)]
                for i, code in enumerate(CODES)}
    print("Modèle de référence écrit dans", out)
    print("Positions attendues (image 256×256) :", json.dumps(expected))


if __name__ == "__main__":
    main()
