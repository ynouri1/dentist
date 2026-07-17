"""Modèle de détection par heatmaps (PyTorch).

Encodeur-décodeur convolutif compact produisant C canaux de heatmaps à 1/4 de
la résolution d'entrée. Volontairement simple pour le baseline ; remplaçable par
HRNet/MMPose sans changer le contrat d'I/O ([1,3,H,W] -> [1,C,H/4,W/4]).

Nécessite torch : s'exécute sur la machine d'entraînement, pas dans la CI .NET.
"""
from __future__ import annotations

import torch
import torch.nn as nn


def _block(in_ch: int, out_ch: int) -> nn.Sequential:
    return nn.Sequential(
        nn.Conv2d(in_ch, out_ch, 3, padding=1),
        nn.BatchNorm2d(out_ch),
        nn.ReLU(inplace=True),
        nn.Conv2d(out_ch, out_ch, 3, padding=1),
        nn.BatchNorm2d(out_ch),
        nn.ReLU(inplace=True),
    )


class HeatmapNet(nn.Module):
    def __init__(self, num_landmarks: int):
        super().__init__()
        self.enc1 = _block(3, 32)
        self.enc2 = _block(32, 64)
        self.enc3 = _block(64, 128)
        self.pool = nn.MaxPool2d(2)
        self.up = nn.Upsample(scale_factor=2, mode="bilinear", align_corners=False)
        self.dec = _block(128 + 64, 64)
        self.head = nn.Conv2d(64, num_landmarks, 1)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        e1 = self.enc1(x)             # H
        e2 = self.enc2(self.pool(e1)) # H/2
        e3 = self.enc3(self.pool(e2)) # H/4
        d = self.dec(torch.cat([self.up(e3), e2], dim=1))  # H/2
        return torch.sigmoid(self.head(d))                  # (B, C, H/2, W/2)
