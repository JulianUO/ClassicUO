# UO terrain export

Turns a facet's land (no statics) into engine-neutral files for the 3D rebuild,
plus a small browser viewer to check the result.

Needs Python 3.10+ with `numpy` and `Pillow`.

```
python3 export_terrain.py --uo "C:/Games/Ascension UO" --facet 0 \
    --region 1220,1390,512,512 --out out/britain
```

- `--region x,y,w,h` in cells. Leave it out for the whole facet.
- `--diffs` applies `mapdifl/mapdif`. Off by default: Ascension's `map0.mul`
  is already the complete map, and the stock diffs would overwrite about 48,700
  cells of it outside Britain.

## What it writes

| File | Contents |
| --- | --- |
| `heightmap_r16.png` | 16-bit heights, one vertex per cell, `(z + 128) * 256` |
| `tile_ids_r16.png` | 16-bit land tile id per cell |
| `atlas.png` | each land tile's 44x44 art diamond un-rotated into a top-down square |
| `atlas_texmap.png` | the same slots from tiledata texmaps |
| `tiles.csv` | per tile id: name, texmap, wet, impassable, atlas slot, cell count |
| `manifest.json` | sizes, origin, scale, encodings, source |
| `viewer_data.*`, `viewer_tiles.json` | compact data for the browser viewer |
| `preview_facet.png` | whole facet in radar colours, 1/4 scale |

## Conventions

- Tile `(x, y)` takes its four corners from cells `(x, y)`, `(x+1, y)`,
  `(x+1, y+1)`, `(x, y+1)`, the same as the client's stretched land.
- Scale: 1 cell = 1.5 m, 1 z = 0.15 m (a 20 z wall is 3 m).
- Map file layout: 8x8 blocks, block index `bx * (height / 8) + by`,
  cell `(u16 tile id, s8 z)`.

## Viewer

`viewer/index.html` loads the export files from its own folder
(three.js from jsDelivr). Copy the outputs next to it and serve the folder.
