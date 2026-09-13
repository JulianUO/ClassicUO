// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Scenes
{
    /// <summary>
    /// Draws the Fog of War overlay and animated mist/cloud effects on top of the game world.
    /// <para/>
    /// For every chunk currently in view:
    /// - Undiscovered chunks are covered with a dark shroud and animated rolling mist clouds.
    /// - Border chunks (discovered chunks adjacent to undiscovered ones) have soft rolling mist
    ///   spilling onto the edges.
    /// - Atmospheric drifting mist particles float across the scene to provide depth and motion.
    /// </summary>
    internal sealed class FogOfWarRenderer
    {
        // Isometric tile constants (matching GetViewPort)
        private const int TILE_HALF_W = 22;  // half tile width  in screen pixels
        private const int TILE_HALF_H = 22;  // half tile height in screen pixels

        // When drawn with DepthStencilState.None, 0f renders on top of the world
        private const float LAYER_DEPTH = 0f;

        private const int CLOUD_VARIATIONS = 6;
        private Texture2D[] _cloudTextures;
        private readonly World _world;
        private float _animationTime;

        public FogOfWarRenderer(World world)
        {
            _world = world;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Call once from <c>GameScene.Load</c> to prime GPU textures.
        /// </summary>
        public void Initialize(GraphicsDevice gd)
        {
            _cloudTextures = BuildCloudTextures(gd, CLOUD_VARIATIONS, 256);
        }

        /// <summary>
        /// Advance mist animations. Call once per frame from <c>GameScene.Update</c>.
        /// </summary>
        public void Update(
            float dt,
            int drawOffsetX,
            int drawOffsetY,
            int viewportWidth,
            int viewportHeight)
        {
            _animationTime += dt;
        }

        /// <summary>
        /// Draw realistic dark charcoal storm clouds strictly over undiscovered chunks.
        /// </summary>
        public void Draw(
            UltimaBatcher2D batcher,
            ref Matrix worldTransform,
            int mapId,
            int drawOffsetX,
            int drawOffsetY,
            int minChunkX, int minChunkY,
            int maxChunkX, int maxChunkY,
            int mapHeightInBlocks)
        {
            if (ProfileManager.CurrentProfile == null) return;
            if (!ProfileManager.CurrentProfile.EnableFogOfWar) return;

            if ((_cloudTextures == null || _cloudTextures.Length == 0 || _cloudTextures[0] == null || _cloudTextures[0].IsDisposed) && batcher.GraphicsDevice != null)
            {
                Initialize(batcher.GraphicsDevice);
            }

            var fogMgr = FogOfWarManager.Instance;
            if (!fogMgr.IsActive)
            {
                if (UltimaLive.UltimaLiveActive && !string.IsNullOrEmpty(UltimaLive.ShardPath) && Client.Game.UO.FileManager.Maps != null)
                {
                    int mCount = Client.Game.UO.FileManager.Maps.MapBlocksSize.GetLength(0);
                    var allMaps = new System.Collections.Generic.List<int>(mCount);
                    for (int i = 0; i < mCount; i++) allMaps.Add(i);
                    fogMgr.Initialise(
                        UltimaLive.ShardPath,
                        allMaps,
                        Client.Game.UO.FileManager.Maps.MapBlocksSize);
                }
            }

            bool drawMist = ProfileManager.CurrentProfile.EnableFogMistEffect && _cloudTextures != null && _cloudTextures.Length > 0;
            if (!drawMist) return;

            float time = _animationTime;

            for (int chunkX = minChunkX - 1; chunkX <= maxChunkX + 1; chunkX++)
            {
                for (int chunkY = minChunkY - 1; chunkY <= maxChunkY + 1; chunkY++)
                {
                    int block = chunkX * mapHeightInBlocks + chunkY;
                    if (fogMgr.IsBlockDiscovered(mapId, block))
                    {
                        continue;
                    }

                    // Check neighbors to avoid overflowing clouds onto discovered territory
                    bool nwDisc = fogMgr.IsBlockDiscovered(mapId, (chunkX - 1) * mapHeightInBlocks + chunkY);
                    bool seDisc = fogMgr.IsBlockDiscovered(mapId, (chunkX + 1) * mapHeightInBlocks + chunkY);
                    bool neDisc = fogMgr.IsBlockDiscovered(mapId, chunkX * mapHeightInBlocks + (chunkY - 1));
                    bool swDisc = fogMgr.IsBlockDiscovered(mapId, chunkX * mapHeightInBlocks + (chunkY + 1));

                    DrawChunkClouds(batcher, drawOffsetX, drawOffsetY, chunkX, chunkY, time, nwDisc, seDisc, neDisc, swDisc);
                }
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        // Anchor points within the 8x8 chunk (in local tile units [0..7])
        // 17 distributed anchors ensure 100% gapless chunk-to-chunk continuity and smooth border blending
        private static readonly (float lx, float ly, float weight)[] ChunkAnchors = new (float, float, float)[]
        {
            // Center core
            (3.5f, 3.5f, 1.25f),

            // Inner ring
            (2.0f, 2.0f, 1.10f),
            (2.0f, 5.0f, 1.10f),
            (5.0f, 2.0f, 1.10f),
            (5.0f, 5.0f, 1.10f),
            (3.5f, 1.5f, 1.05f),
            (3.5f, 5.5f, 1.05f),
            (1.5f, 3.5f, 1.05f),
            (5.5f, 3.5f, 1.05f),

            // Perimeter anchors (eliminates black seams between chunks & mixes softly with borders)
            (0.5f, 0.5f, 1.00f),
            (0.5f, 3.5f, 1.00f),
            (0.5f, 6.5f, 1.00f),
            (6.5f, 0.5f, 1.00f),
            (6.5f, 3.5f, 1.00f),
            (6.5f, 6.5f, 1.00f),
            (3.5f, 0.5f, 1.00f),
            (3.5f, 6.5f, 1.00f)
        };

        /// <summary>
        /// Draws dense, dark rolling storm clouds with full chunk-to-chunk continuity and soft border mixing.
        /// </summary>
        private void DrawChunkClouds(
            UltimaBatcher2D batcher,
            int offsetX, int offsetY,
            int chunkX,  int chunkY,
            float time,
            bool nwDisc, bool seDisc, bool neDisc, bool swDisc)
        {
            int baseTileX = chunkX * 8;
            int baseTileY = chunkY * 8;
            float seed = (chunkX * 17.13f) + (chunkY * 31.37f);

            // 1. Draw solid core and perimeter cloud anchors for this undiscovered block
            for (int i = 0; i < ChunkAnchors.Length; i++)
            {
                var (lx, ly, weight) = ChunkAnchors[i];

                float pSeed = seed + i * 3.41f;
                int texIndex = (int)MathF.Abs(pSeed * 7f) % _cloudTextures.Length;
                Texture2D tex = _cloudTextures[texIndex];
                if (tex == null || tex.IsDisposed) continue;

                // Slow atmospheric drift
                float driftX = MathF.Sin(time * 0.12f + pSeed) * 8f + MathF.Cos(time * 0.07f + pSeed * 1.2f) * 5f;
                float driftY = MathF.Cos(time * 0.10f + pSeed * 0.9f) * 6f + MathF.Sin(time * 0.05f + pSeed) * 4f;

                // Sample actual map tile elevation Z so clouds follow terrain and building heights
                int tileXInt = baseTileX + (int)MathF.Round(lx);
                int tileYInt = baseTileY + (int)MathF.Round(ly);
                float elevationZ = GetTileElevationZ(tileXInt, tileYInt);

                // Isometric screen position of this anchor, adjusted for map elevation Z (- Z * 4)
                float tileX = baseTileX + lx;
                float tileY = baseTileY + ly;
                float px = (tileX - tileY) * TILE_HALF_W - offsetX + driftX;
                float py = (tileX + tileY) * TILE_HALF_H - (elevationZ * 4f) + 22 - offsetY + driftY;

                // Dense, large puff size (~280px to 340px) to completely shroud undiscovered territory
                float baseSize = (280f + MathF.Sin(pSeed * 2.5f) * 35f) * weight;
                if ((swDisc && ly > 4.5f) || (seDisc && lx > 4.5f))
                {
                    baseSize *= 0.65f;
                }

                float breathe = 1.0f + 0.06f * MathF.Sin(time * 0.25f + pSeed);
                float puffSize = baseSize * breathe;

                float posX = px - puffSize * 0.5f;
                float posY = py - puffSize * 0.5f;

                var hue = new Vector3(0f, 0f, 1f);

                batcher.Draw(
                    tex,
                    new Rectangle((int)posX, (int)posY, (int)puffSize, (int)puffSize),
                    hue,
                    LAYER_DEPTH
                );
            }


            // 2. Dark boundary overlap mist: only allowed from top and right boundaries
            if (neDisc || nwDisc)
            {
                DrawBorderOverlap(batcher, offsetX, offsetY, baseTileX, baseTileY, time, seed, nwDisc, neDisc);
            }
        }

        /// <summary>
        /// Casts soft, billowing dark mist across the border onto discovered terrain strictly from top and right boundaries.
        /// </summary>
        private void DrawBorderOverlap(
            UltimaBatcher2D batcher,
            int offsetX, int offsetY,
            int baseTileX, int baseTileY,
            float time, float seed,
            bool nwDisc, bool neDisc)
        {
            void DrawOverlapPuff(float lx, float ly, float borderSeed, float alphaMultiplier)
            {
                int texIndex = (int)MathF.Abs(borderSeed * 11f) % _cloudTextures.Length;
                Texture2D tex = _cloudTextures[texIndex];
                if (tex == null || tex.IsDisposed) return;

                float driftX = MathF.Sin(time * 0.15f + borderSeed) * 10f + MathF.Cos(time * 0.09f + borderSeed) * 6f;
                float driftY = MathF.Cos(time * 0.12f + borderSeed) * 8f + MathF.Sin(time * 0.06f + borderSeed) * 5f;

                int tileXInt = baseTileX + (int)MathF.Round(lx);
                int tileYInt = baseTileY + (int)MathF.Round(ly);
                float elevationZ = GetTileElevationZ(tileXInt, tileYInt);

                float tileX = baseTileX + lx;
                float tileY = baseTileY + ly;
                float px = (tileX - tileY) * TILE_HALF_W - offsetX + driftX;
                float py = (tileX + tileY) * TILE_HALF_H - (elevationZ * 4f) + 22 - offsetY + driftY;

                float puffSize = 220f + MathF.Sin(borderSeed * 3f) * 20f;
                float posX = px - puffSize * 0.5f;
                float posY = py - puffSize * 0.5f;

                var hue = new Vector3(0f, 0f, alphaMultiplier);

                batcher.Draw(
                    tex,
                    new Rectangle((int)posX, (int)posY, (int)puffSize, (int)puffSize),
                    hue,
                    LAYER_DEPTH
                );
            }

            if (neDisc)
            {
                DrawOverlapPuff(1.5f, -1.0f, seed + 301.3f, 0.70f);
                DrawOverlapPuff(4.0f, -1.0f, seed + 302.7f, 0.75f);
                DrawOverlapPuff(6.5f, -1.0f, seed + 303.9f, 0.70f);
            }

            if (nwDisc)
            {
                DrawOverlapPuff(-1.0f, 1.5f, seed + 101.3f, 0.60f);
                DrawOverlapPuff(-1.0f, 4.0f, seed + 102.7f, 0.65f);
            }
        }


        /// <summary>
        /// Retrieves the real map elevation Z for a world tile (checking chunk objects and raw map data).
        /// </summary>
        private float GetTileElevationZ(int tileX, int tileY)
        {
            if (_world?.Map == null) return 0f;

            int cellX = tileX % 8;
            int cellY = tileY % 8;
            if (cellX < 0) cellX += 8;
            if (cellY < 0) cellY += 8;

            int cx = tileX >> 3;
            int cy = tileY >> 3;

            var chunk = _world.Map.GetChunk2(cx, cy, false);
            if (chunk != null)
            {
                sbyte maxZ = -125;
                for (var obj = chunk.Tiles[cellX, cellY]; obj != null; obj = obj.TNext)
                {
                    if (obj is GameObjects.Land l)
                    {
                        sbyte lz = l.IsStretched ? (sbyte)l.AverageZ : l.Z;
                        if (lz > maxZ) maxZ = lz;
                    }
                    else if (obj is GameObjects.Static s)
                    {
                        if (s.Z > maxZ) maxZ = s.Z;
                    }
                    else if (obj.Z > maxZ)
                    {
                        maxZ = obj.Z;
                    }
                }

                if (maxZ > -125)
                {
                    return maxZ;
                }
            }

            try
            {
                sbyte rawZ = _world.Map.GetTileZ(tileX, tileY);
                if (rawZ > -125) return rawZ;
            }
            catch { }

            return 0f;
        }

        /// <summary>
        /// Generates realistic volumetric dark charcoal storm cloud textures using multi-scale domain-warped FBM noise.
        /// Colors are strictly deep charcoal/slate grey and PREMULTIPLIED to prevent alpha accumulation whiteout.
        /// </summary>
        private static Texture2D[] BuildCloudTextures(GraphicsDevice gd, int count = 6, int size = 256)
        {
            var textures = new Texture2D[count];
            float invSize = 1f / size;

            int[] perm = new int[512];
            var rng = new Random(2026);
            byte[] p = new byte[256];
            for (int i = 0; i < 256; i++) p[i] = (byte)i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];

            float Grad(int hash, float x, float y)
            {
                int h = hash & 7;
                float u = h < 4 ? x : y;
                float v = h < 4 ? y : x;
                return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
            }

            float Noise2D(float x, float y)
            {
                int X = (int)MathF.Floor(x) & 255;
                int Y = (int)MathF.Floor(y) & 255;
                float xf = x - MathF.Floor(x);
                float yf = y - MathF.Floor(y);

                float u = xf * xf * xf * (xf * (xf * 6f - 15f) + 10f);
                float v = yf * yf * yf * (yf * (yf * 6f - 15f) + 10f);

                int a = perm[X] + Y;
                int aa = perm[a];
                int ab = perm[a + 1];
                int b = perm[X + 1] + Y;
                int ba = perm[b];
                int bb = perm[b + 1];

                float g1 = Grad(perm[aa], xf, yf);
                float g2 = Grad(perm[ba], xf - 1f, yf);
                float g3 = Grad(perm[ab], xf, yf - 1f);
                float g4 = Grad(perm[bb], xf - 1f, yf - 1f);

                float x1 = MathHelper.Lerp(g1, g2, u);
                float x2 = MathHelper.Lerp(g3, g4, u);
                return MathHelper.Lerp(x1, x2, v);
            }

            float Fbm(float x, float y, int octaves = 4)
            {
                float total = 0f;
                float amp = 0.55f;
                float freq = 2.4f;
                for (int oct = 0; oct < octaves; oct++)
                {
                    total += amp * (Noise2D(x * freq, y * freq) * 0.5f + 0.5f);
                    freq *= 2.15f;
                    amp *= 0.48f;
                }
                return total;
            }

            float WarpedNoise(float u, float v, float seedOffset)
            {
                float qx = Fbm(u * 2.5f + seedOffset, v * 2.5f + seedOffset * 1.3f, 3);
                float qy = Fbm(u * 2.5f + seedOffset * 1.7f + 5.2f, v * 2.5f + seedOffset * 0.8f + 1.3f, 3);

                float rx = Fbm(u * 2.5f + 0.5f * qx + 1.7f, v * 2.5f + 0.5f * qy + 9.2f, 4);
                float ry = Fbm(u * 2.5f + 0.5f * qx + 8.3f, v * 2.5f + 0.5f * qy + 2.8f, 4);

                return Fbm(u * 2.5f + 0.6f * rx, v * 2.5f + 0.6f * ry, 4);
            }

            float center = size * 0.5f;
            float rad = center * 0.95f;

            for (int t = 0; t < count; t++)
            {
                var data = new Color[size * size];
                float seedOffset = t * 17.39f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float nx = (x - center) / rad;
                        float ny = (y - center) / rad;
                        float baseDist = MathF.Sqrt(nx * nx + ny * ny);

                        float u = (float)x * invSize;
                        float v = (float)y * invSize;

                        // Non-circular organic shape distortion using low-frequency noise
                        float shapePerturb = (Fbm(u * 3f + seedOffset, v * 3f + seedOffset * 1.5f, 3) - 0.5f) * 0.45f;
                        float effectiveDist = baseDist + shapePerturb;

                        if (effectiveDist >= 1f)
                        {
                            data[y * size + x] = Color.Transparent;
                            continue;
                        }

                        // Smooth organic boundary falloff
                        float edgeFalloff = Math.Clamp((1f - effectiveDist) / 0.65f, 0f, 1f);
                        edgeFalloff = edgeFalloff * edgeFalloff * (3f - 2f * edgeFalloff);

                        float n = WarpedNoise(u, v, seedOffset);
                        float density = Math.Clamp((n - 0.20f) / 0.60f, 0f, 1f);
                        density = density * density * (3f - 2f * density);

                        float alpha = Math.Clamp(density * edgeFalloff * 0.95f, 0f, 1f);
                        if (alpha <= 0.005f)
                        {
                            data[y * size + x] = Color.Transparent;
                            continue;
                        }

                        // Deep atmospheric dark charcoal storm palette (STRICTLY NO WHITE)
                        // Base luminance ranges strictly between 20 (deep shadow) and 42 (mid storm grey)
                        float baseShade = 20.0f + 22.0f * density;
                        float rShade = baseShade * 0.92f;
                        float gShade = baseShade * 0.96f;
                        float bShade = baseShade * 1.05f;

                        // CRITICAL: Premultiply RGB by alpha for BlendState.AlphaBlend (One, InvSrcAlpha)
                        // This mathematically guarantees layers will NEVER sum up into white!
                        byte r = (byte)Math.Clamp(rShade * alpha, 0f, 255f);
                        byte g = (byte)Math.Clamp(gShade * alpha, 0f, 255f);
                        byte b = (byte)Math.Clamp(bShade * alpha, 0f, 255f);
                        byte a = (byte)Math.Clamp(alpha * 255f, 0f, 255f);

                        data[y * size + x] = new Color(r, g, b, a);
                    }
                }

                var tex = new Texture2D(gd, size, size, false, SurfaceFormat.Color);
                tex.SetData(data);
                textures[t] = tex;
            }

            return textures;
        }
    }
}
