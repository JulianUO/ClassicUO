"""Export a region's statics as sprites for the classic-view viewer.

Each static keeps its original art. The client draws a static so the bottom
centre of its art sits on the bottom corner of its tile's diamond, lifted
z * 4 px (ChunkMesh.cs: posX = baseX - (w/2 - 22), posY = baseY - (h - 44)).
The viewer reproduces that exactly on top of the 3D terrain.

Usage:
  python3 export_statics.py --uo "C:/Games/Ascension UO" --facet 0 \
      --region 1220,1390,512,512 --out out/britain
Writes statics_pages_<n>.png, statics_sprites.json and statics_data.json.
"""

from __future__ import annotations

import argparse
import base64
import json
import os

import numpy as np
from PIL import Image

from uofiles import (S_BACKGROUND, S_NODIAGONAL, S_SURFACE, UOData, load_static_tiledata,
                     load_statics, static_art)

PAGE = 2048
NEVER_DRAWN = {0x0001, 0x21BC, 0xA1FE, 0xA1FF, 0xA200, 0xA201} | set(range(0x2198, 0x21A5))


def drawable(g: int, tile) -> bool:
    """Same rules as ClassicUO's GameObject.CanBeDrawn for statics."""
    if g in NEVER_DRAWN:
        return False
    if g in (0x9E4C, 0x9E64, 0x9E65, 0x9E7D):
        return not (tile.flags & S_BACKGROUND) and not (tile.flags & S_SURFACE)
    if g == 0x63D3:
        return True
    if tile.name.lower().startswith("nodraw"):
        return False
    return not (tile.flags & S_NODIAGONAL)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--uo", required=True)
    ap.add_argument("--facet", type=int, default=0)
    ap.add_argument("--region", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    rx, ry, rw, rh = (int(v) for v in args.region.split(","))

    uo = UOData(args.uo)
    tiles = load_static_tiledata(uo)
    st = load_statics(uo, args.facet, rx, ry, rw, rh)
    keep = np.array([drawable(int(g), tiles[int(g)]) for g in st["id"]], dtype=bool)
    st = st[keep]

    # Pack each graphic's art once (shelf packing, tallest first).
    arts = {}
    for g in np.unique(st["id"]):
        a = static_art(uo, int(g))
        if a is not None:
            arts[int(g)] = a
    order = sorted(arts, key=lambda g: -arts[g].shape[0])
    pages, sprites = [], {}
    page = None
    x = y = shelf = 0
    for g in order:
        a = arts[g]
        h, w = a.shape[:2]
        if page is None or x + w > PAGE:
            x, y, shelf = 0, y + shelf, 0
        if page is None or y + h > PAGE:
            page = np.zeros((PAGE, PAGE, 4), np.uint8)
            pages.append(page)
            x = y = shelf = 0
        page[y:y + h, x:x + w] = a
        sprites[g] = [len(pages) - 1, x, y, w, h]
        x += w + 1
        shelf = max(shelf, h + 1)
    for i, p in enumerate(pages):
        Image.fromarray(p).save(os.path.join(args.out, f"statics_page_{i}.png"), optimize=True)

    st = st[np.isin(st["id"], list(sprites))]
    slot = {g: i for i, g in enumerate(sorted(sprites))}
    # Per static: sprite slot, x, y (region-relative), z, priority bias, hue.
    flags = np.array([tiles[int(g)].flags for g in st["id"]], dtype=np.uint64)
    heights = np.array([tiles[int(g)].height for g in st["id"]], dtype=np.int16)
    # Draw-order bias within a tile, like the client: background first, then
    # surfaces, then everything else; taller items later.
    prio = np.where(flags & S_BACKGROUND, 0, np.where(flags & S_SURFACE, 1, 2)) * 64 + np.minimum(heights, 63)
    rec = np.zeros(len(st), dtype=[("slot", "<u2"), ("x", "<i2"), ("y", "<i2"), ("z", "i1"), ("prio", "u1"), ("hue", "<u2")])
    rec["slot"] = [slot[int(g)] for g in st["id"]]
    rec["x"] = st["x"] - rx
    rec["y"] = st["y"] - ry
    rec["z"] = st["z"]
    rec["prio"] = prio
    rec["hue"] = st["hue"]
    with open(os.path.join(args.out, "statics_data.json"), "w") as f:
        json.dump({"format": "slot u16, x i16, y i16, z i8, prio u8, hue u16", "count": int(len(rec)),
                   "base64": base64.b64encode(rec.tobytes()).decode("ascii")}, f)
    with open(os.path.join(args.out, "statics_sprites.json"), "w") as f:
        json.dump({"page_px": PAGE, "pages": len(pages),
                   "sprites": [[g] + sprites[g] for g in sorted(sprites)]}, f)
    print(f"statics {len(rec)}  graphics {len(sprites)}  pages {len(pages)}")


if __name__ == "__main__":
    main()
