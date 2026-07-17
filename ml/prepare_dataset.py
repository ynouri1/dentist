"""Unifie les sources (ISBI 2015, CL-Detection, export de l'app) au format commun.

Sortie : {out}/samples/<id>/image.png + landmarks.json. L'export de l'application
est DÉJÀ au bon format : il est simplement copié. Les datasets publics sont
convertis (coordonnées + table de correspondance des points).
"""
import argparse
import json
import shutil
from pathlib import Path

# Correspondance indices ISBI 2015 (19 points) -> codes internes.
ISBI_INDEX_TO_CODE = {
    0: "S", 1: "N", 2: "Or", 3: "Po", 4: "A", 5: "B", 6: "Pog", 7: "Me",
    8: "Gn", 9: "Go", 10: "L1E", 11: "U1E", 12: "U1A", 13: "L1A",
    14: "ANS", 15: "PNS", 16: "Ar", 17: "D", 18: "Co",
}


def copy_app_export(app_dir: Path, out_samples: Path) -> int:
    src = app_dir / "samples"
    if not src.exists():
        return 0
    count = 0
    for sample in sorted(src.iterdir()):
        if not (sample / "landmarks.json").exists():
            continue
        shutil.copytree(sample, out_samples / f"app-{sample.name}", dirs_exist_ok=True)
        count += 1
    return count


def convert_isbi(isbi_dir: Path, out_samples: Path) -> int:
    """Attendu : {isbi}/images/*.bmp|png + {isbi}/labels/*.txt (une ligne 'x,y' par point)."""
    images = isbi_dir / "images"
    labels = isbi_dir / "labels"
    if not images.exists() or not labels.exists():
        return 0

    count = 0
    for label_file in sorted(labels.glob("*.txt")):
        coords = []
        for line in label_file.read_text().splitlines():
            line = line.strip()
            if not line:
                continue
            x, y = line.replace(";", ",").split(",")[:2]
            coords.append((float(x), float(y)))

        image_src = next((images / f"{label_file.stem}{ext}"
                          for ext in (".bmp", ".png", ".jpg") if (images / f"{label_file.stem}{ext}").exists()), None)
        if image_src is None:
            continue

        points = [{"code": ISBI_INDEX_TO_CODE[i], "x": x, "y": y}
                  for i, (x, y) in enumerate(coords) if i in ISBI_INDEX_TO_CODE]

        try:
            from PIL import Image
            with Image.open(image_src) as im:
                width, height = im.size
        except Exception:
            width, height = 0, 0

        dest = out_samples / f"isbi-{label_file.stem}"
        dest.mkdir(parents=True, exist_ok=True)
        shutil.copy(image_src, dest / "image.png")
        (dest / "landmarks.json").write_text(json.dumps({
            "imageWidth": width, "imageHeight": height,
            "pixelSpacingMm": 0.1,  # ISBI : 0,1 mm/px (documenté)
            "points": points,
        }, indent=2), encoding="utf-8")
        count += 1
    return count


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--isbi", type=Path, default=None)
    parser.add_argument("--app-export", type=Path, default=None)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()

    out_samples = args.out / "samples"
    out_samples.mkdir(parents=True, exist_ok=True)

    total = 0
    if args.app_export:
        n = copy_app_export(args.app_export, out_samples)
        print(f"Export app : {n} échantillon(s)")
        total += n
    if args.isbi:
        n = convert_isbi(args.isbi, out_samples)
        print(f"ISBI 2015 : {n} échantillon(s)")
        total += n
    print(f"Total préparé : {total} -> {out_samples}")


if __name__ == "__main__":
    main()
