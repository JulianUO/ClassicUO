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
"""

from __future__ import annotations

import argparse
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


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--uo", required=True)
    ap.add_argument("--facet", type=int, default=0)
    ap.add_argument("--region", default=None, help="x,y,w,h in cells; default whole facet")
    ap.add_argument("--out", required=True)
    ap.add_argument("--diffs", action="store_true", help="apply mapdifl/mapdif (off: map file as-is)")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    uo = UOData(args.uo)
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
    with open(os.path.join(args.out, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
