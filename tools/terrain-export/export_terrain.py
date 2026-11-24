"""Export UO land (no statics) into engine-neutral files.

Usage:
  python3 export_terrain.py --uo "C:/Games/Ascension UO" --facet 0 \
      --region 1220,1390,512,512 --out out/britain [--diffs]

Writes into --out:
  preview_facet.png   whole facet in radar colours (2D sanity check)
  heightmap_r16.png   16-bit heights, one vertex per cell, (W+1)x(H+1)
  tile_ids_r16.png    16-bit land tile id per cell, WxH
  atlas.png           one square texture per land tile id used (top-down art)
  atlas_texmap.png    the same ids using tiledata texmaps (stretched-tile look)
  tiles.csv           per tile id: name, flags, texmap id, atlas slot, use count
  manifest.json       sizes, scale, origin, file list, source files
  viewer_data.bin     compact data the browser viewer reads (+ viewer_data.json, base64)
  viewer_tiles.json   atlas slot -> [tile id, name, flags]
  vertex_class.png    (with --classes) per vertex: R main class, G second class, B its weight
  weights/<class>.png (with --classes) 8-bit layer weight maps, Unreal landscape layers
  materials.png       (with --materials) one 256 px layer (4x4 tiles) per class, modern look
"""

from __future__ import annotations

import argparse
import collections
import csv
import json
import os
import struct

import numpy as np
from PIL import Image

from uofiles import MAP_SIZES, UOData, diamond_to_square

CELL_METERS = 1.5   # Julian's pick: 1 cell = 1.5 m
Z_METERS = 0.15     # 20 z (one wall height) = 3 m
TILE_PX = 44        # atlas slot size


def save_r16(path: str, arr: np.ndarray) -> None:
    Image.fromarray(arr.astype(np.uint16)).save(path)


# Fixed class order: index = value in vertex_class.png and layer in materials.
CLASS_ORDER = ["void", "water", "grass", "forest", "jungle", "dirt", "furrows", "sand", "seabed",
               "rock", "cave", "snow", "swamp", "paved", "wood_floor", "obsidian", "lava", "acid", "unknown"]
SOLID = {"void": (12, 11, 10), "water": (28, 62, 92), "lava": (200, 80, 20), "acid": (90, 160, 40),
         "unknown": (128, 128, 128)}
MAT_TILES = 8           # one material layer covers 8 x 8 tiles, aligned to the tile grid
MAT_PX = 64 * MAT_TILES # 64 px per tile, the density of UO's own texmaps
# How many tiles one copy of each photo covers (1, 2, 4 or 8, so copies stay on
# the tile grid). Picked by eye so stones, planks, blades and pebbles come out
# the same size as on UO's own texmap for that terrain, e.g. about five
# cobbles across a paved tile. Not the photos' real-world size: UO draws
# ground detail bigger than life.
TILES_PER_PHOTO = {"grass": 2, "forest": 2, "jungle": 2, "dirt": 2, "furrows": 2, "sand": 4,
                   "seabed": 2, "rock": 4, "cave": 2, "snow": 4, "swamp": 2, "paved": 2,
                   "wood_floor": 1, "obsidian": 2}
# Terrains with no good photo: use UO's own texmap for them.
UO_ONLY = {"lava", "acid", "water", "void", "unknown"}
# Blend priority, low to high: where two terrains meet, the higher one paints over
# the lower one with a ragged edge instead of the two being averaged.
PRIORITY = ["void", "unknown", "water", "seabed", "sand", "dirt", "furrows", "grass", "jungle",
            "forest", "swamp", "snow", "rock", "cave", "obsidian", "lava", "acid", "paved", "wood_floor"]


_UO = None


def class_colour(tiles: list) -> np.ndarray | None:
    """Average colour of a class's pure tiles, weighted by how often they are used."""
    if not tiles or _UO is None:
        return None
    acc, n = np.zeros(3), 0
    for t, cells in sorted(tiles, key=lambda x: -x[1])[:8]:
        art = _UO.land_art(t)
        if art is None:
            continue
        sq = diamond_to_square(art, TILE_PX)
        acc += sq[..., :3].reshape(-1, 3).mean(0) * cells
        n += cells
    return acc / n if n else None


def _class_texmaps(tiles: list, land) -> list[np.ndarray]:
    out = []
    for t, _ in sorted(tiles, key=lambda x: -x[1])[:8]:
        tm = _UO.texmap(land[t].tex_id) if land[t].tex_id else None
        if tm is not None:
            out.append(np.array(Image.fromarray(tm).resize((64, 64), Image.BILINEAR))[..., :3])
    return out


def class_reference(tiles: list, land) -> np.ndarray | None:
    """Pixels of UO's own texmaps for a terrain class, used as the target palette."""
    if not tiles or _UO is None:
        return None
    tms = _class_texmaps(tiles, land)
    return np.concatenate([t.reshape(-1, 3) for t in tms]) if tms else None


def ref_tile(cls: str, tiles: list, land) -> np.ndarray:
    tm = _class_texmaps(tiles, land)[0]
    return np.tile(tm, (MAT_TILES, MAT_TILES, 1))


def match_histogram(im: np.ndarray, ref: np.ndarray) -> np.ndarray:
    out = np.empty(im.shape, np.float32)
    for ch in range(3):
        src = im[..., ch].ravel()
        order = np.argsort(src, kind="stable")
        q = np.linspace(0, 1, src.size)
        target = np.quantile(ref[:, ch].astype(np.float32), q)
        res = np.empty(src.size, np.float32)
        res[order] = target
        out[..., ch] = res.reshape(im.shape[:2])
    return out


def export_blend(args, rid: np.ndarray, land) -> dict:
    """Per-vertex terrain class (for the viewer) and per-class weight maps (for
    Unreal landscape layers). Vertex (x, y) is corner N of tile (x, y), E of
    tile (x-1, y), S of tile (x-1, y-1) and W of tile (x, y-1)."""
    ci = {c: i for i, c in enumerate(CLASS_ORDER)}
    corner = {}
    pure_of = collections.defaultdict(list)  # class -> pure tile ids, for colour matching
    with open(args.classes) as f:
        for r in csv.DictReader(f):
            t = int(r["tile_id"])
            if r["kind"] == "pure":
                pure_of[r["class"]].append((t, int(r["cells"])))
            cls = [r[k] or r["class"] for k in ("corner_N", "corner_E", "corner_S", "corner_W")]
            if land[t].is_wet:
                cls = ["water"] * 4
            corner[t] = [ci.get(c, ci["unknown"]) for c in cls]
    lut = np.full((0x10000, 4), ci["unknown"], dtype=np.uint8)
    for t, c in corner.items():
        lut[t] = c
    tc = lut[rid]  # [h, w, 4] corner classes per tile: N, E, S, W
    h, w = rid.shape
    votes = np.zeros((h + 1, w + 1, len(CLASS_ORDER)), dtype=np.uint8)
    ys, xs = np.mgrid[0:h, 0:w]
    for k, (dy, dx) in enumerate(((0, 0), (0, 1), (1, 1), (1, 0))):  # N, E, S, W -> vertex offsets
        np.add.at(votes, (ys + dy, xs + dx, tc[..., k]), 1)
    # Tiles the client never draws (void, NODRAW) only win a vertex no real terrain touches.
    vi = ci["void"]
    has_other = (votes.sum(-1) - votes[..., vi]) > 0
    votes[..., vi] = np.where(has_other, 0, votes[..., vi])
    n = votes.sum(-1, keepdims=True).clip(min=1)
    weights = votes.astype(np.float32) / n
    # Top two classes per vertex: R = main class, G = second class, B = second's weight.
    order = np.argsort(-weights, axis=-1, kind="stable")
    c1, c2 = order[..., 0], order[..., 1]
    w2 = np.take_along_axis(weights, c2[..., None], -1)[..., 0]
    c2 = np.where(w2 > 0, c2, c1)
    vclass = np.stack([c1, c2, (w2 * 255).round()], -1).astype(np.uint8)
    Image.fromarray(vclass, "RGB").save(os.path.join(args.out, "vertex_class.png"))

    wdir = os.path.join(args.out, "weights")
    os.makedirs(wdir, exist_ok=True)
    layers = []
    for i, c in enumerate(CLASS_ORDER):
        if weights[..., i].max() > 0:
            Image.fromarray((weights[..., i] * 255).round().astype(np.uint8)).save(os.path.join(wdir, f"{c}.png"))
            layers.append(c)

    info = {"vertex_class": "vertex_class.png", "vertex_class_encoding": "R=class, G=second class, B=second weight*255", "class_order": CLASS_ORDER, "weight_maps": {c: f"weights/{c}.png" for c in layers}}
    if args.materials:
        strip = np.zeros((MAT_PX * len(CLASS_ORDER), MAT_PX, 3), dtype=np.uint8)
        # Normal maps in the same layout, so the viewer can light the ground's
        # surface detail (stones, ruts, roots). Flat where there is no photo.
        nstrip = np.zeros_like(strip) + np.array([128, 128, 255], np.uint8)
        for i, c in enumerate(CLASS_ORDER):
            ref = class_reference(pure_of.get(c, []), land)
            p = os.path.join(args.materials, f"{c}.jpg")
            if os.path.exists(p) and c not in UO_ONLY:
                n = TILES_PER_PHOTO.get(c, MAT_TILES)
                im = np.array(Image.open(p).convert("RGB").resize((64 * n, 64 * n), Image.LANCZOS))
                im = np.tile(im, (MAT_TILES // n, MAT_TILES // n, 1))
                pn = os.path.join(args.materials, f"{c}_nor.jpg")
                if os.path.exists(pn):
                    nm = np.array(Image.open(pn).convert("RGB").resize((64 * n, 64 * n), Image.LANCZOS))
                    nstrip[i * MAT_PX:(i + 1) * MAT_PX] = np.tile(nm, (MAT_TILES // n, MAT_TILES // n, 1))
                if ref is not None and args.match_colours:
                    # Match each channel's whole distribution (brightness AND contrast) to
                    # UO's own texmaps for this terrain, so the photo takes Britannia's palette.
                    im = (1 - args.match_colours) * im + args.match_colours * match_histogram(im, ref)
            elif ref is not None:
                im = ref_tile(c, pure_of.get(c, []), land)
            else:
                im = np.zeros((MAT_PX, MAT_PX, 3), np.uint8) + np.array(SOLID.get(c, SOLID["unknown"]), np.uint8)
            # UO stores colour as 15-bit (5 bits a channel): quantise to the same depth.
            im = (np.clip(np.asarray(im, np.float32), 0, 255) / 255 * 31).round() * 255 / 31
            strip[i * MAT_PX:(i + 1) * MAT_PX] = im.astype(np.uint8)
        Image.fromarray(strip).save(os.path.join(args.out, "materials.png"), optimize=True)
        Image.fromarray(nstrip).save(os.path.join(args.out, "materials_normal.png"), optimize=True)
        info["materials"] = {"file": "materials.png", "layer_px": MAT_PX, "tiles_per_layer": MAT_TILES, "normals": "materials_normal.png",
                             "tiles_per_photo": {c: TILES_PER_PHOTO[c] for c in CLASS_ORDER if c in TILES_PER_PHOTO},
                             "priority": PRIORITY}
        src = os.path.join(args.materials, "sources.json")
        if os.path.exists(src):
            info["materials"]["sources"] = json.load(open(src))
    return info


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--uo", required=True)
    ap.add_argument("--facet", type=int, default=0)
    ap.add_argument("--region", default=None, help="x,y,w,h in cells; default whole facet")
    ap.add_argument("--out", required=True)
    ap.add_argument("--diffs", action="store_true", help="apply mapdifl/mapdif (off: map file as-is)")
    ap.add_argument("--classes", default=None, help="tile_classes.csv from classify_tiles.py: adds blend maps")
    ap.add_argument("--materials", default=None, help="folder of <class>.jpg textures for the modern look")
    ap.add_argument("--match-colours", type=float, default=1.0,
                    help="0..1, how far to pull each material's average colour to UO's colour for that terrain")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    uo = UOData(args.uo)
    global _UO
    _UO = uo
    ids, zs, diff_count = uo.load_map(args.facet, apply_diffs=args.diffs)
    land = uo.load_land_tiledata()
    fw, fh = MAP_SIZES[args.facet]

    # 1. Whole-facet radar preview (downscaled 4x) for a quick visual check.
    radar = uo.radar_colors()
    prev = radar[ids[::4, ::4]]
    Image.fromarray(prev).convert("RGB").save(os.path.join(args.out, "preview_facet.png"))

    # 2. Region.
    if args.region:
        rx, ry, rw, rh = (int(v) for v in args.region.split(","))
    else:
        rx, ry, rw, rh = 0, 0, fw, fh
    # Vertex grid is one larger: tile (x, y) takes its corners from cells
    # (x, y), (x+1, y), (x+1, y+1), (x, y+1), like the client's stretched land.
    vx1, vy1 = min(rx + rw + 1, fw), min(ry + rh + 1, fh)
    vz = np.zeros((rh + 1, rw + 1), dtype=np.int16)
    sub = zs[ry:vy1, rx:vx1].astype(np.int16)
    vz[: sub.shape[0], : sub.shape[1]] = sub
    if sub.shape[0] < rh + 1:
        vz[sub.shape[0]:, :] = vz[sub.shape[0] - 1, :]
    if sub.shape[1] < rw + 1:
        vz[:, sub.shape[1]:] = vz[:, sub.shape[1] - 1: sub.shape[1]]
    rid = ids[ry:ry + rh, rx:rx + rw]

    # Heightmap: z + 128 scaled to 16 bits (z * 256 + 32768), Unreal-friendly.
    save_r16(os.path.join(args.out, "heightmap_r16.png"), (vz.astype(np.int32) + 128) * 256)
    save_r16(os.path.join(args.out, "tile_ids_r16.png"), rid)

    # 3. Atlas of every land tile id used in the region.
    used, counts = np.unique(rid, return_counts=True)
    slot_of = {int(t): i for i, t in enumerate(used)}
    cols = 32
    rows = (len(used) + cols - 1) // cols
    atlas = np.zeros((rows * TILE_PX, cols * TILE_PX, 4), dtype=np.uint8)
    atlas_tex = np.zeros_like(atlas)
    for i, t in enumerate(used):
        t = int(t)
        cy, cx = divmod(i, cols)
        sl = (slice(cy * TILE_PX, (cy + 1) * TILE_PX), slice(cx * TILE_PX, (cx + 1) * TILE_PX))
        art = uo.land_art(t)
        sq = diamond_to_square(art, TILE_PX) if art is not None else np.full((TILE_PX, TILE_PX, 4), 255, np.uint8)
        atlas[sl] = sq
        tm = uo.texmap(land[t].tex_id) if land[t].tex_id else None
        if tm is not None:
            atlas_tex[sl] = np.array(Image.fromarray(tm).resize((TILE_PX, TILE_PX), Image.BILINEAR))
        else:
            atlas_tex[sl] = sq
    Image.fromarray(atlas).save(os.path.join(args.out, "atlas.png"))
    Image.fromarray(atlas_tex).save(os.path.join(args.out, "atlas_texmap.png"))

    with open(os.path.join(args.out, "tiles.csv"), "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["tile_id", "hex", "name", "texmap_id", "wet", "impassable", "atlas_slot", "cells"])
        for t, c in zip(used, counts):
            t = int(t)
            lt = land[t]
            w.writerow([t, f"0x{t:04X}", lt.name, lt.tex_id, int(lt.is_wet), int(lt.is_impassable), slot_of[t], int(c)])

    # 4. Compact viewer data: header, heights int8, slot uint16, wet uint8.
    slots = np.vectorize(slot_of.get, otypes=[np.uint16])(rid)
    wet = np.array([land[int(t)].is_wet for t in used], dtype=np.uint8)[slots]
    with open(os.path.join(args.out, "viewer_data.bin"), "wb") as f:
        f.write(struct.pack("<4sIIIIff", b"UOT1", rw, rh, len(used), cols, CELL_METERS, Z_METERS))
        f.write(vz.astype(np.int8).tobytes())
        f.write(slots.astype("<u2").tobytes())
        f.write(wet.tobytes())

    # The web viewer is served from a host that only allows text/image types,
    # so ship the same bytes base64-encoded in JSON as well.
    import base64
    with open(os.path.join(args.out, "viewer_data.bin"), "rb") as f:
        b64 = base64.b64encode(f.read()).decode("ascii")
    with open(os.path.join(args.out, "viewer_data.json"), "w") as f:
        json.dump({"format": "UOT1", "base64": b64}, f)

    # Slot table for the viewer: tile id, name, flags (1 = wet, 2 = not drawn).
    def slot_flags(t: int) -> int:
        lt = land[t]
        f = 1 if lt.is_wet else 0
        if lt.name.upper() in ("NODRAW", "VOID!!!!!!", "ED") or t in (0x0000, 0x0002):
            f |= 2
        return f

    with open(os.path.join(args.out, "viewer_tiles.json"), "w") as f:
        json.dump([[int(t), land[int(t)].name, slot_flags(int(t))] for t in used], f)

    # 5. Terrain classes per vertex, from the decoded tile corners.
    blend = None
    if args.classes:
        blend = export_blend(args, rid, land)

    manifest = {
        "facet": args.facet,
        "facet_size": [fw, fh],
        "region": {"x": rx, "y": ry, "w": rw, "h": rh},
        "cell_meters": CELL_METERS,
        "z_meters": Z_METERS,
        "heightmap": {"file": "heightmap_r16.png", "size": [rw + 1, rh + 1],
                      "encoding": "value = (z + 128) * 256"},
        "tile_ids": {"file": "tile_ids_r16.png", "size": [rw, rh]},
        "atlas": {"file": "atlas.png", "texmap_file": "atlas_texmap.png", "slot_px": TILE_PX,
                  "columns": cols, "slots": len(used)},
        "diffs_applied": bool(args.diffs),
        "diff_blocks": diff_count,
        "z_range": [int(vz.min()), int(vz.max())],
        "source": os.path.abspath(args.uo),
    }
    if blend:
        manifest["blend"] = blend
    with open(os.path.join(args.out, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
