"""Readers for the classic UO .mul files the terrain exporter needs.

Pure Python + numpy, no client code required. Formats match what
ClassicUO.Assets reads (MapLoader, TileDataLoader, ArtLoader, TexmapsLoader).
"""

from __future__ import annotations

import os
import struct
from dataclasses import dataclass

import numpy as np

# Facet sizes in cells, as ClassicUO's MapLoader defines them.
MAP_SIZES = {
    0: (7168, 4096),  # Felucca
    1: (7168, 4096),  # Trammel
    2: (2304, 1600),  # Ilshenar
    3: (2560, 2048),  # Malas
    4: (1448, 1448),  # Tokuno
    5: (1280, 4096),  # Ter Mur
}

BLOCK_BYTES = 4 + 64 * 3  # header + 8x8 cells of (u16 tile id, s8 z)

# Land tile flags from tiledata (TileFlag in ClassicUO).
FLAG_WET = 0x00000080
FLAG_IMPASSABLE = 0x00000040


@dataclass
class LandTile:
    flags: int
    tex_id: int
    name: str

    @property
    def is_wet(self) -> bool:
        return bool(self.flags & FLAG_WET)

    @property
    def is_impassable(self) -> bool:
        return bool(self.flags & FLAG_IMPASSABLE)


class UOData:
    def __init__(self, folder: str):
        self.folder = folder

    def path(self, name: str) -> str:
        return os.path.join(self.folder, name)

    # ------------------------------------------------------------------ map
    def load_map(self, facet: int, apply_diffs: bool = True):
        """Return (tile_ids uint16 [H, W], z int8 [H, W], diff_blocks)."""
        width, height = MAP_SIZES[facet]
        bw, bh = width // 8, height // 8
        raw = np.fromfile(self.path(f"map{facet}.mul"), dtype=np.uint8)
        nblocks = bw * bh
        if raw.size < nblocks * BLOCK_BYTES:
            raise ValueError(f"map{facet}.mul is {raw.size} bytes, expected {nblocks * BLOCK_BYTES}")
        blocks = raw[: nblocks * BLOCK_BYTES].reshape(nblocks, BLOCK_BYTES)

        diff_count = 0
        if apply_diffs:
            difl = self.path(f"mapdifl{facet}.mul")
            dif = self.path(f"mapdif{facet}.mul")
            if os.path.exists(difl) and os.path.exists(dif):
                idx = np.fromfile(difl, dtype="<i4")
                draw = np.fromfile(dif, dtype=np.uint8)
                n = min(idx.size, draw.size // BLOCK_BYTES)
                dblocks = draw[: n * BLOCK_BYTES].reshape(n, BLOCK_BYTES)
                blocks = blocks.copy()
                for i in range(n):  # later entries win, same as the client
                    b = int(idx[i])
                    if 0 <= b < nblocks:
                        blocks[b] = dblocks[i]
                        diff_count += 1

        cells = blocks[:, 4:].reshape(nblocks, 64, 3)
        ids = (cells[:, :, 0].astype(np.uint16) | (cells[:, :, 1].astype(np.uint16) << 8))
        zs = cells[:, :, 2].view(np.int8)
        # block index = bx * bh + by; cell index inside block = cy * 8 + cx
        ids = ids.reshape(bw, bh, 8, 8).transpose(1, 2, 0, 3).reshape(height, width)
        zs = zs.reshape(bw, bh, 8, 8).transpose(1, 2, 0, 3).reshape(height, width)
        return ids, zs, diff_count

    # ------------------------------------------------------------- tiledata
    def load_land_tiledata(self) -> list[LandTile]:
        data = open(self.path("tiledata.mul"), "rb").read()
        # High Seas+ layout: 8-byte flags. Older layout: 4-byte flags.
        new_format = len(data) >= 512 * (4 + 32 * 30) + 2048 * (4 + 32 * 41)
        entry = 30 if new_format else 26
        tiles: list[LandTile] = []
        off = 0
        for _group in range(512):
            off += 4
            for _ in range(32):
                if new_format:
                    flags = struct.unpack_from("<Q", data, off)[0]
                    tex = struct.unpack_from("<H", data, off + 8)[0]
                    name = data[off + 10: off + 30]
                else:
                    flags = struct.unpack_from("<I", data, off)[0]
                    tex = struct.unpack_from("<H", data, off + 4)[0]
                    name = data[off + 6: off + 26]
                tiles.append(LandTile(flags, tex, name.split(b"\0")[0].decode("latin-1").strip()))
                off += entry
        return tiles

    # ------------------------------------------------------------------ art
    def _open_indexed(self, idx_name: str, mul_name: str):
        idx = np.fromfile(self.path(idx_name), dtype="<i4").reshape(-1, 3)
        mul = open(self.path(mul_name), "rb")
        return idx, mul

    def land_art(self, tile_id: int, _cache={}) -> np.ndarray | None:
        """44x44 RGBA uint8 diamond for a land tile, or None if missing."""
        key = (self.folder, "artidx")
        if key not in _cache:
            _cache[key] = self._open_indexed("artidx.mul", "art.mul")
        idx, mul = _cache[key]
        if tile_id >= len(idx):
            return None
        offset, length, _ = idx[tile_id]
        if offset < 0 or length <= 0:
            return None
        mul.seek(int(offset))
        px = np.frombuffer(mul.read(2024), dtype="<u2")
        if px.size < 1012:
            return None
        out = np.zeros((44, 44), dtype=np.uint16)
        mask = np.zeros((44, 44), dtype=bool)
        p = 0
        x, w = 21, 2
        for y in range(22):
            out[y, x:x + w] = px[p:p + w]
            mask[y, x:x + w] = True
            p += w
            x -= 1
            w += 2
        x, w = 0, 44
        for y in range(22, 44):
            out[y, x:x + w] = px[p:p + w]
            mask[y, x:x + w] = True
            p += w
            x += 1
            w -= 2
        rgba = color1555_to_rgba(out)
        rgba[..., 3] = np.where(mask, 255, 0)
        return rgba

    def texmap(self, tex_id: int, _cache={}) -> np.ndarray | None:
        """Square RGBA texmap (64 or 128 px), or None."""
        key = (self.folder, "texidx")
        if key not in _cache:
            _cache[key] = self._open_indexed("texidx.mul", "texmaps.mul")
        idx, mul = _cache[key]
        if tex_id >= len(idx):
            return None
        offset, length, _ = idx[tex_id]
        if offset < 0 or length <= 0:
            return None
        size = 128 if length >= 0x8000 else 64
        mul.seek(int(offset))
        px = np.frombuffer(mul.read(size * size * 2), dtype="<u2").reshape(size, size)
        rgba = color1555_to_rgba(px)
        rgba[..., 3] = 255
        return rgba

    def radar_colors(self) -> np.ndarray:
        cols = np.fromfile(self.path("radarcol.mul"), dtype="<u2")
        return color1555_to_rgba(cols)


def color1555_to_rgba(c: np.ndarray) -> np.ndarray:
    c = c.astype(np.uint32)
    r = ((c >> 10) & 0x1F) * 255 // 31
    g = ((c >> 5) & 0x1F) * 255 // 31
    b = (c & 0x1F) * 255 // 31
    a = np.full_like(r, 255)
    return np.stack([r, g, b, a], axis=-1).astype(np.uint8)


def diamond_to_square(diamond: np.ndarray, size: int = 44) -> np.ndarray:
    """Un-rotate a 44x44 land diamond into a top-down square.

    u runs along map +x (north corner to east corner on screen),
    v runs along map +y (north corner to west corner on screen).
    """
    u = (np.arange(size) + 0.5) / size
    uu, vv = np.meshgrid(u, u)  # rows = v, cols = u
    px = 22 + 22 * uu - 22 * vv - 0.5
    py = 22 * uu + 22 * vv - 0.5
    x0 = np.clip(np.floor(px).astype(int), 0, 43)
    y0 = np.clip(np.floor(py).astype(int), 0, 43)
    x1 = np.clip(x0 + 1, 0, 43)
    y1 = np.clip(y0 + 1, 0, 43)
    fx = np.clip(px - x0, 0, 1)[..., None]
    fy = np.clip(py - y0, 0, 1)[..., None]
    d = diamond.astype(np.float32)
    # Pull edge pixels inward where the diamond mask is empty.
    a = d[..., 3:4] / 255.0
    dp = d[..., :3] * a  # premultiplied so empty pixels add no black
    top = dp[y0, x0] * (1 - fx) + dp[y0, x1] * fx
    bot = dp[y1, x0] * (1 - fx) + dp[y1, x1] * fx
    ta = a[y0, x0] * (1 - fx) + a[y0, x1] * fx
    ba = a[y1, x0] * (1 - fx) + a[y1, x1] * fx
    col = top * (1 - fy) + bot * fy
    alpha = ta * (1 - fy) + ba * fy
    rgb = col / np.maximum(alpha, 0.01)
    out = np.zeros((size, size, 4), dtype=np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[..., 3] = 255
    return out


# ---------------------------------------------------------------- statics
STATIC_DTYPE = np.dtype([("id", "<u2"), ("x", "u1"), ("y", "u1"), ("z", "i1"), ("hue", "<u2")])

S_BACKGROUND = 0x00000001
S_SURFACE = 0x00000200
S_FOLIAGE = 0x00020000
S_NODIAGONAL = 0x02000000
S_ROOF = 0x10000000


@dataclass
class StaticTile:
    flags: int
    height: int
    name: str


def load_static_tiledata(uo: "UOData") -> list[StaticTile]:
    data = open(uo.path("tiledata.mul"), "rb").read()
    new_format = len(data) >= 512 * (4 + 32 * 30) + 2048 * (4 + 32 * 41)
    land_entry, entry = (30, 41) if new_format else (26, 37)
    off = 512 * (4 + 32 * land_entry)
    out: list[StaticTile] = []
    while off + 4 + 32 * entry <= len(data):
        off += 4
        for _ in range(32):
            if new_format:
                flags = struct.unpack_from("<Q", data, off)[0]
                height = data[off + 20]
                name = data[off + 21: off + 41]
            else:
                flags = struct.unpack_from("<I", data, off)[0]
                height = data[off + 16]
                name = data[off + 17: off + 37]
            out.append(StaticTile(flags, height, name.split(b"\0")[0].decode("latin-1").strip()))
            off += entry
    return out


def load_statics(uo: "UOData", facet: int, x0: int, y0: int, w: int, h: int) -> np.ndarray:
    """Statics inside a cell rectangle, as a structured array with absolute x, y."""
    width, height = MAP_SIZES[facet]
    bh = height // 8
    idx = np.fromfile(uo.path(f"staidx{facet}.mul"), dtype="<i4").reshape(-1, 3)
    mul = open(uo.path(f"statics{facet}.mul"), "rb")
    rows = []
    for bx in range(x0 // 8, (x0 + w + 7) // 8):
        for by in range(y0 // 8, (y0 + h + 7) // 8):
            b = bx * bh + by
            if b >= len(idx):
                continue
            off, length, _ = idx[b]
            if off < 0 or length <= 0 or off == -1:
                continue
            mul.seek(int(off))
            arr = np.frombuffer(mul.read(int(length) // 7 * 7), dtype=STATIC_DTYPE).copy()
            ax = arr["x"].astype(np.int32) + bx * 8
            ay = arr["y"].astype(np.int32) + by * 8
            keep = (ax >= x0) & (ax < x0 + w) & (ay >= y0) & (ay < y0 + h)
            for a, xx, yy in zip(arr[keep], ax[keep], ay[keep]):
                rows.append((int(a["id"]), xx, yy, int(a["z"]), int(a["hue"])))
    return np.array(rows, dtype=[("id", "<u2"), ("x", "<i4"), ("y", "<i4"), ("z", "i1"), ("hue", "<u2")])


def static_art(uo: "UOData", graphic: int, _cache={}) -> np.ndarray | None:
    """RGBA art for a static item graphic (run-length encoded in art.mul)."""
    key = (uo.folder, "artidx")
    if key not in _cache:
        _cache[key] = uo._open_indexed("artidx.mul", "art.mul")
    idx, mul = _cache[key]
    i = graphic + 0x4000
    if i >= len(idx):
        return None
    offset, length, _ = idx[i]
    if offset < 0 or length <= 0:
        return None
    mul.seek(int(offset))
    buf = np.frombuffer(mul.read(int(length)), dtype="<u2")
    if buf.size < 4:
        return None
    w, h = int(buf[2]), int(buf[3])
    if w <= 0 or h <= 0 or w > 1024 or h > 1024:
        return None
    lookup = buf[4:4 + h].astype(np.int64)
    start = 4 + h
    px = np.zeros((h, w), dtype=np.uint16)
    alpha = np.zeros((h, w), dtype=bool)
    for y in range(h):
        p = start + int(lookup[y])
        x = 0
        while p + 1 < buf.size:
            xoff, run = int(buf[p]), int(buf[p + 1])
            p += 2
            if xoff + run == 0:
                break
            x += xoff
            if run and x + run <= w:
                px[y, x:x + run] = buf[p:p + run]
                alpha[y, x:x + run] = True
            p += run
            x += run
    rgba = color1555_to_rgba(px)
    rgba[..., 3] = np.where(alpha, 255, 0)
    return rgba
