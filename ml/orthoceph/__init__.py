"""Pipeline IA de détection des landmarks céphalométriques (Ortho).

Cœur numérique (heatmaps, métriques) en NumPy pur — testable sans framework DL.
L'entraînement (model.py, train.py) utilise PyTorch et s'exécute sur une machine ML.
"""

# Ordre des canaux de sortie du modèle = ordre des codes. Doit rester aligné avec
# LandmarkCatalog (.NET) et landmarks.model.json.
LANDMARK_CODES = [
    "S", "N", "A", "B", "D", "Pog", "Gn", "Me", "Go", "Po", "Or",
    "Ar", "Co", "ANS", "PNS", "U1E", "U1A", "L1E", "L1A",
]

# Landmarks « majeurs » sur lesquels porte le seuil d'acceptation clinique.
MAJOR_LANDMARKS = ["S", "N", "A", "B", "Go", "Me", "Or", "Po"]
