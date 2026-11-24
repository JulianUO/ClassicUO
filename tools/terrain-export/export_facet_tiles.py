"""Export a whole facet as Unreal-ready landscape tiles.

Each tile is 1008x1008 cells = 1009x1009 vertices, one of Unreal's recommended
landscape resolutions, named <name>_x<i>_y<j>.png so World Partition's tiled
landscape import picks them up. Outside the facet is padded with void at the
edge height.

Usage:
  python3 export_facet_tiles.py --uo "C:/Games/Ascension UO" --facet 0 \
      --classes out/classes/tile_classes.csv --out out/felucca
Writes heightmap/, tile_ids/, weights/<class>/ and facet_manifest.json.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import types

import numpy as np
from PIL import Image

from export_terrain import CELL_METERS, Z_METERS, export_blend
from uofiles import MAP_SIZES, UOData

TILE = 1008
VOID_TILE = 0x0244


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--uo", required=True)
    ap.add_argument("--facet", type=int, default=0)
    ap.add_argument("--classes", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--diffs", action="store_true")
    args = ap.parse_args()

    uo = UOData(args.uo)
    ids, zs, _ = uo.load_map(args.facet, apply_diffs=args.diffs)
    land = uo.load_land_tiledata()
    fw, fh = MAP_SIZES[args.facet]
    nx, ny = -(-fw // TILE), -(-fh // TILE)
    pw, ph = nx * TILE + 1, ny * TILE + 1
    pid = np.full((ph, pw), VOID_TILE, dtype=np.uint16)
    pid[:fh, :fw] = ids
    pz = np.pad(zs.astype(np.int16), ((0, ph - fh), (0, pw - fw)), mode="edge")

    for sub in ("heightmap", "tile_ids", "weights"):
        os.makedirs(os.path.join(args.out, sub), exist_ok=True)
    layers = set()
    for j in range(ny):
        for i in range(nx):
            x0, y0 = i * TILE, j * TILE
            name = f"x{i}_y{j}"
            vz = pz[y0:y0 + TILE + 1, x0:x0 + TILE + 1]
            Image.fromarray(((vz.astype(np.int32) + 128) * 256).astype(np.uint16)).save(
                os.path.join(args.out, "heightmap", f"felucca_{name}.png"))
            rid = pid[y0:y0 + TILE, x0:x0 + TILE]
            Image.fromarray(rid).save(os.path.join(args.out, "tile_ids", f"felucca_{name}.png"))
            tmp = os.path.join(args.out, "_tmp")
            os.makedirs(tmp, exist_ok=True)
            info = export_blend(types.SimpleNamespace(out=tmp, classes=args.classes, materials=None,
                                                      match_colours=0), rid, land)
            for cls, rel in info["weight_maps"].items():
                os.makedirs(os.path.join(args.out, "weights", cls), exist_ok=True)
                os.replace(os.path.join(tmp, rel), os.path.join(args.out, "weights", cls, f"felucca_{name}.png"))
                layers.add(cls)
            print("tile", name, "layers", len(info["weight_maps"]), flush=True)

    # Layers missing from a tile get an all-zero map so every layer has every tile.
    zero = Image.fromarray(np.zeros((TILE + 1, TILE + 1), np.uint8))
    for cls in layers:
        for j in range(ny):
            for i in range(nx):
                p = os.path.join(args.out, "weights", cls, f"felucca_x{i}_y{j}.png")
                if not os.path.exists(p):
                    zero.save(p)

    manifest = {
        "facet": args.facet, "facet_size": [fw, fh], "tiles": [nx, ny],
        "tile_cells": TILE, "tile_vertices": TILE + 1,
        "cell_meters": CELL_METERS, "z_meters": Z_METERS,
        "heightmap_encoding": "value = (z + 128) * 256; Unreal Z scale so 1 z = 0.15 m",
        "unreal_scale": {"x_cm": CELL_METERS * 100, "y_cm": CELL_METERS * 100,
                               "z_scale": Z_METERS * 100 * 128 / 256,
                               "note": "Unreal: Scale X/Y = x_cm, Scale Z = z_scale (128 raw units per Z-scale cm)"},
        "layers": sorted(layers),
        "padding": "outside the facet: tile 0x0244 (void), heights clamped to the edge",
    }
    shutil.rmtree(os.path.join(args.out, "_tmp"), ignore_errors=True)
    with open(os.path.join(args.out, "facet_manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
