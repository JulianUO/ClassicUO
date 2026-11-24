"""Classify every land tile used on a facet into a terrain class, and decode
transition tiles into which corner belongs to which terrain.

Two independent signals:
  1. Art: the four corners of each tile's un-rotated art are colour-matched
     against "pure" tiles (tiles whose four corners look alike) of known classes.
  2. Map: which classes actually sit next to the tile across the whole facet.
Where they disagree, or a tile has no usable name, it goes on the review list.

Usage:
  python3 classify_tiles.py --uo "C:/Games/Ascension UO" --facet 0 --out out/classes
Writes tile_classes.csv, review.csv and review_sheet.png.
First pass: corners are judged from 12 px patches, so diagonal transitions
can lose a corner. The review list is where those show up.
"""

from __future__ import annotations

import argparse
import collections
import csv
import os

import numpy as np
from PIL import Image, ImageDraw

from uofiles import UOData, diamond_to_square

# tiledata name -> terrain class. Names not listed are decided by colour.
NAME_CLASS = {
    "water": "water", "grass": "grass", "forest": "forest", "leaves": "forest",
    "tree": "forest", "jungle": "jungle", "rock": "rock", "embank": "rock",
    "cave": "cave", "cave exit": "cave", "dirt": "dirt", "furrows": "furrows",
    "sand": "sand", "snow": "snow", "lava": "lava", "acid": "acid",
    "obsidian": "obsidian", "wooden floor": "wood_floor", "planks": "wood_floor",
    "stone": "paved", "cobblestones": "paved", "flagstone": "paved",
    "marble": "paved", "tile": "paved", "brick": "paved", "sand stone": "paved",
    "void": "void", "void!!!!!!": "void", "nodraw": "void", "ed": "void",
}
CORNERS = ("N", "E", "S", "W")  # vertex (x,y), (x+1,y), (x+1,y+1), (x,y+1)
PATCH = 12
MARGIN = 0.6  # another class must be this much closer (ratio) for a corner to leave its own class


def corner_colors(sq: np.ndarray) -> np.ndarray:
    s = sq.shape[0]
    p = PATCH
    rgb = sq[..., :3].astype(np.float32)
    # rows = v (map +y), cols = u (map +x)
    return np.array([
        rgb[:p, :p].reshape(-1, 3).mean(0),            # N: u=0, v=0
        rgb[:p, s - p:].reshape(-1, 3).mean(0),        # E: u=1, v=0
        rgb[s - p:, s - p:].reshape(-1, 3).mean(0),    # S: u=1, v=1
        rgb[s - p:, :p].reshape(-1, 3).mean(0),        # W: u=0, v=1
    ])


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--uo", required=True)
    ap.add_argument("--facet", type=int, default=0)
    ap.add_argument("--out", required=True)
    ap.add_argument("--diffs", action="store_true")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    uo = UOData(args.uo)
    ids, _, _ = uo.load_map(args.facet, apply_diffs=args.diffs)
    land = uo.load_land_tiledata()
    used, counts = np.unique(ids, return_counts=True)
    used = [int(t) for t in used]
    count_of = dict(zip(used, (int(c) for c in counts)))

    # 1. Art features.
    squares, corners = {}, {}
    for t in used:
        art = uo.land_art(t)
        if art is None:
            continue
        sq = diamond_to_square(art, 44)
        squares[t] = sq
        corners[t] = corner_colors(sq)

    name_class = {t: NAME_CLASS.get(land[t].name.lower()) for t in used}
    for t in used:  # near-black art is void whatever its name
        if t in corners and corners[t].mean() < 12:
            name_class[t] = "void"

    def spread(t):
        c = corners[t]
        return float(np.max(np.linalg.norm(c - c.mean(0), axis=1)))

    # Pure prototypes: named tiles whose four corners look alike.
    protos = [(t, name_class[t]) for t in used
              if t in corners and name_class[t] and name_class[t] != "void" and spread(t) < 10]
    proto_cols = np.array([corners[t].mean(0) for t, _ in protos])
    proto_cls = [c for _, c in protos]

    proto_cls_arr = np.array(proto_cls)

    def nearest(col, own=None, allowed=None):
        """Closest prototype class. A corner keeps the tile's own class unless
        another class is clearly closer AND that class really borders the tile
        on the map (so dark cobblestone does not turn into obsidian)."""
        d = np.linalg.norm(proto_cols - col, axis=1)
        i = int(np.argmin(d))
        best, dbest = proto_cls[i], float(d[i])
        if own is None or best == own:
            return best, dbest
        own_mask = proto_cls_arr == own
        if not own_mask.any():
            return best, dbest
        down = float(d[own_mask].min())
        if allowed is not None and best not in allowed:
            # closest class that does border the tile, if any
            ok = np.isin(proto_cls_arr, list(allowed))
            if not ok.any():
                return own, down
            j = int(np.argmin(np.where(ok, d, np.inf)))
            best, dbest = proto_cls[j], float(d[j])
            if best == own:
                return own, down
        return (best, dbest) if dbest < MARGIN * down else (own, down)

    # 2. Map neighbours: class of each cell by name, then neighbour classes per tile id.
    max_id = max(used) + 1
    cls_names = sorted({c for c in NAME_CLASS.values()} | {"unknown"})
    cls_index = {c: i for i, c in enumerate(cls_names)}
    lut = np.full(max_id, cls_index["unknown"], dtype=np.uint8)
    for t in used:
        lut[t] = cls_index[name_class[t] or "unknown"]
    cell_cls = lut[ids]
    neigh = collections.defaultdict(collections.Counter)
    H, W = ids.shape
    for dy, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
        y0, y1 = max(0, -dy), H - max(0, dy)
        x0, x1 = max(0, -dx), W - max(0, dx)
        a = ids[y0:y1, x0:x1]
        b = cell_cls[y0 + dy:y1 + dy, x0 + dx:x1 + dx]
        pairs = a.astype(np.uint32) * 64 + b
        u, c = np.unique(pairs, return_counts=True)
        for p, n in zip(u, c):
            neigh[int(p // 64)][cls_names[int(p % 64)]] += int(n)

    # 3. Decide.
    rows, review = [], []
    for t in used:
        lt = land[t]
        nm = name_class[t]
        nb = [c for c, _ in neigh[t].most_common() if c != "unknown"][:3]
        if t not in corners:
            rows.append([t, f"0x{t:04X}", lt.name, count_of[t], nm or "unknown", "missing art", "", "", "", "", "", ";".join(nb), "low"])
            review.append(t)
            continue
        allowed = set([c for c, _ in neigh[t].most_common() if c != "unknown"][:4])
        labs = [nearest(c, nm, allowed) for c in corners[t]]
        corner_cls = [l for l, _ in labs]
        if nm == "void":
            corner_cls = ["void"] * 4
        uniq = sorted(set(corner_cls))
        if len(uniq) == 1 and spread(t) < 22:
            kind = "pure"
            cls = nm or uniq[0]
        elif len(uniq) == 1:
            kind = "pure (varied art)"
            cls = nm or uniq[0]
        else:
            kind = "transition"
            cls = nm or max(uniq, key=corner_cls.count)
        if lt.is_wet:
            cls = "water"
        mask = "".join(k for k, c in zip(CORNERS, corner_cls) if c != cls)
        # Confidence: art and neighbours agree, and the name (if any) agrees with art.
        art_ok = nm is None or nm in corner_cls or kind != "pure" or nm == cls
        neigh_ok = all(c in nb or c == cls for c in uniq) if nb else False
        conf = "high" if (art_ok and neigh_ok and nm) else ("medium" if (art_ok and (neigh_ok or nm)) else "low")
        rows.append([t, f"0x{t:04X}", lt.name, count_of[t], cls, kind, *corner_cls, mask, ";".join(nb), conf])
        if conf == "low" and count_of[t] >= 20:
            review.append(t)

    header = ["tile_id", "hex", "name", "cells", "class", "kind", "corner_N", "corner_E", "corner_S", "corner_W",
              "other_corners", "neighbour_classes", "confidence"]
    with open(os.path.join(args.out, "tile_classes.csv"), "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)

    by_id = {r[0]: r for r in rows}
    review.sort(key=lambda t: -count_of[t])
    with open(os.path.join(args.out, "review.csv"), "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(header + ["your_class"])
        for t in review:
            w.writerow(by_id[t] + [""])

    # Contact sheet of the review list.
    n = len(review)
    cols, cw, ch = 8, 150, 120
    sheet = Image.new("RGB", (cols * cw, max(1, (n + cols - 1) // cols) * ch), (24, 21, 17))
    dr = ImageDraw.Draw(sheet)
    for i, t in enumerate(review):
        x, y = (i % cols) * cw, (i // cols) * ch
        if t in squares:
            sheet.paste(Image.fromarray(squares[t]).convert("RGB").resize((66, 66), Image.NEAREST), (x + 42, y + 6))
        r = by_id[t]
        dr.text((x + 6, y + 76), f"0x{t:04X} {r[2][:12]}", fill=(236, 228, 212))
        dr.text((x + 6, y + 90), f"{r[4]} / {r[5][:10]}", fill=(210, 165, 75))
        dr.text((x + 6, y + 104), f"{r[3]} cells", fill=(169, 157, 136))
    sheet.save(os.path.join(args.out, "review_sheet.png"))

    kinds = collections.Counter(r[5] for r in rows)
    confs = collections.Counter(r[12] for r in rows)
    classes = collections.Counter(r[4] for r in rows)
    print("tiles", len(rows), dict(kinds), dict(confs))
    print("classes", dict(classes))
    print("review", len(review))


if __name__ == "__main__":
    main()
