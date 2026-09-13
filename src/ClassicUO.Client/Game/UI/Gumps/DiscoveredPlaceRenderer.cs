// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ClassicUO.Game.UI.Gumps
{
    internal static class DiscoveredPlaceRenderer
    {
        private static FontFamily _fontFamily;
        private static bool _fontLoaded;
        private static readonly object _lock = new object();
        private static readonly ConcurrentDictionary<string, Texture2D> _textureCache = new ConcurrentDictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private static void EnsureFontLoaded()
        {
            if (_fontLoaded)
                return;

            lock (_lock)
            {
                if (_fontLoaded)
                    return;

                FontCollection collection = new FontCollection();
                Stream fontStream = null;

                try
                {
                    // 1. Try embedded resource (Avatar.ttf first, then CLF-UoClassic.ttf)
                    var assembly = Assembly.GetExecutingAssembly();
                    string[] names = assembly.GetManifestResourceNames();
                    foreach (var name in names)
                    {
                        if (name.EndsWith("Avatar.ttf", StringComparison.OrdinalIgnoreCase))
                        {
                            fontStream = assembly.GetManifestResourceStream(name);
                            break;
                        }
                    }

                    if (fontStream == null)
                    {
                        foreach (var name in names)
                        {
                            if (name.EndsWith("CLF-UoClassic.ttf", StringComparison.OrdinalIgnoreCase))
                            {
                                fontStream = assembly.GetManifestResourceStream(name);
                                break;
                            }
                        }
                    }

                    // 2. Try file system search paths (Avatar.ttf priority)
                    if (fontStream == null)
                    {
                        string[] candidatePaths = new[]
                        {
                            System.IO.Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Client", "Fonts", "Avatar.ttf"),
                            System.IO.Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Fonts", "Avatar.ttf"),
                            System.IO.Path.Combine(CUOEnviroment.ExecutablePath, "Resources", "Fonts", "Avatar.ttf"),
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatar.ttf"),
                            System.IO.Path.Combine(CUOEnviroment.ExecutablePath, "Avatar.ttf"),
                            System.IO.Path.Combine(CUOEnviroment.ExecutablePath, "..", "..", "Avatar.ttf"),
                            System.IO.Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Client", "Fonts", "CLF-UoClassic.ttf"),
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLF-UoClassic.ttf"),
                            System.IO.Path.Combine(CUOEnviroment.ExecutablePath, "CLF-UoClassic.ttf"),
                            System.IO.Path.Combine(CUOEnviroment.ExecutablePath, "..", "..", "CLF-UoClassic.ttf"),
                            "Avatar.ttf",
                            "CLF-UoClassic.ttf"
                        };

                        foreach (var path in candidatePaths)
                        {
                            if (System.IO.File.Exists(path))
                            {
                                fontStream = System.IO.File.OpenRead(path);
                                Log.Trace($"DiscoveredPlaceRenderer: Found font at {path}");
                                break;
                            }
                        }
                    }

                    if (fontStream != null)
                    {
                        using (fontStream)
                        {
                            _fontFamily = collection.Add(fontStream);
                        }
                        _fontLoaded = true;
                        Log.Trace($"DiscoveredPlaceRenderer: Successfully loaded font '{_fontFamily.Name}'.");
                    }
                    else
                    {
                        if (SystemFonts.TryGet("Times New Roman", out var fallback) ||
                            SystemFonts.TryGet("Georgia", out fallback) ||
                            SystemFonts.TryGet("Arial", out fallback))
                        {
                            _fontFamily = fallback;
                            _fontLoaded = true;
                            Log.Warn("DiscoveredPlaceRenderer: Avatar.ttf not found, using system fallback font.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"DiscoveredPlaceRenderer: Failed to load font: {ex.Message}");
                }
            }
        }

        public static Texture2D GetOrCreateBannerTexture(GraphicsDevice graphicsDevice, string placeName)
        {
            if (graphicsDevice == null)
                return null;

            string key = string.IsNullOrWhiteSpace(placeName) ? "Britannia" : placeName.Trim();

            if (_textureCache.TryGetValue(key, out Texture2D cached) && cached != null && !cached.IsDisposed)
            {
                return cached;
            }

            EnsureFontLoaded();

            if (!_fontLoaded)
                return null;

            try
            {
                Texture2D texture = GenerateBannerTexture(graphicsDevice, key);
                if (texture != null)
                {
                    _textureCache[key] = texture;
                }
                return texture;
            }
            catch (Exception ex)
            {
                Log.Error($"DiscoveredPlaceRenderer: Error generating texture for '{key}': {ex.Message}");
                return null;
            }
        }

        private static Texture2D GenerateBannerTexture(GraphicsDevice graphicsDevice, string placeName)
        {
            string titleText = placeName;

            // Larger typography size (64pt) for grand solid presence
            Font titleFont = _fontFamily.CreateFont(64f, SixLabors.Fonts.FontStyle.Regular);

            TextOptions titleOptions = new TextOptions(titleFont)
            {
                HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment.Center,
                VerticalAlignment = SixLabors.Fonts.VerticalAlignment.Center
            };

            IPathCollection titleGlyphs = TextBuilder.GenerateGlyphs(titleText, titleOptions);
            var titleBounds = titleGlyphs.Bounds;

            float titleW = titleBounds.Width;
            float titleH = titleBounds.Height;

            int paddingX = 54;
            int paddingTop = 24;
            int middlePadding = 7; // Reduced padding towards the bottom divider line
            int barHeight = 8;
            int paddingBottom = 20;

            float contentWidth = Math.Max(titleW + 60, 340f);
            int totalWidth = (int)Math.Ceiling(contentWidth + paddingX * 2);
            int totalHeight = (int)Math.Ceiling(paddingTop + titleH + middlePadding + barHeight + paddingBottom);

            float centerX = totalWidth / 2f;

            // Title position (Region name on top with clean balanced padding)
            float titleY = paddingTop - titleBounds.Y;
            float titleX = centerX - titleBounds.X - (titleW / 2f);
            var translatedTitle = titleGlyphs.Translate(titleX, titleY);

            // Divider bar position (Tighter below region name)
            float barY = paddingTop + titleH + middlePadding;
            float barWidth = Math.Max(titleW + 30, contentWidth - 30);
            float barStartX = centerX - (barWidth / 2f);

            // 1. Create 8-bit Alpha Mask for the entire banner (Text + Ornamental Divider)
            using (Image<L8> maskImage = new Image<L8>(totalWidth, totalHeight))
            {
                maskImage.Mutate(ctx =>
                {
                    ctx.Fill(SixLabors.ImageSharp.Color.White, translatedTitle);
                    DrawOrnamentalDividerMask(ctx, barStartX, barY, barWidth, barHeight, centerX);
                });

                // 2. Render Forged Iron / Burnished Steel 3D Metallic Surface
                using (Image<Rgba32> outputImage = Render3DMetallicSurface(maskImage, totalWidth, totalHeight))
                {
                    byte[] pixelBytes = new byte[totalWidth * totalHeight * 4];
                    outputImage.CopyPixelDataTo(pixelBytes);

                    Texture2D texture = new Texture2D(graphicsDevice, totalWidth, totalHeight, false, SurfaceFormat.Color);
                    texture.SetData(pixelBytes);
                    return texture;
                }
            }
        }

        private static void DrawOrnamentalDividerMask(IImageProcessingContext ctx, float startX, float y, float width, float height, float centerX)
        {
            float rodH = 3.5f;
            float rodY = y + (height - rodH) / 2f;

            // Main rod
            ctx.Fill(SixLabors.ImageSharp.Color.White, new RectangularPolygon(startX + 8, rodY, width - 16, rodH));

            // Central Diamond
            float diamondSize = 10f;
            PointF[] centerDiamond = new[]
            {
                new PointF(centerX, rodY + rodH / 2f - diamondSize / 2f),
                new PointF(centerX + diamondSize / 2f, rodY + rodH / 2f),
                new PointF(centerX, rodY + rodH / 2f + diamondSize / 2f),
                new PointF(centerX - diamondSize / 2f, rodY + rodH / 2f)
            };
            ctx.Fill(SixLabors.ImageSharp.Color.White, new Polygon(new LinearLineSegment(centerDiamond)));

            // Left arrow finial
            float finialSize = 8f;
            PointF[] leftArrow = new[]
            {
                new PointF(startX, rodY + rodH / 2f),
                new PointF(startX + finialSize, rodY + rodH / 2f - finialSize / 2f),
                new PointF(startX + finialSize * 0.7f, rodY + rodH / 2f),
                new PointF(startX + finialSize, rodY + rodH / 2f + finialSize / 2f)
            };
            ctx.Fill(SixLabors.ImageSharp.Color.White, new Polygon(new LinearLineSegment(leftArrow)));

            // Right arrow finial
            PointF[] rightArrow = new[]
            {
                new PointF(startX + width, rodY + rodH / 2f),
                new PointF(startX + width - finialSize, rodY + rodH / 2f - finialSize / 2f),
                new PointF(startX + width - finialSize * 0.7f, rodY + rodH / 2f),
                new PointF(startX + width - finialSize, rodY + rodH / 2f + finialSize / 2f)
            };
            ctx.Fill(SixLabors.ImageSharp.Color.White, new Polygon(new LinearLineSegment(rightArrow)));
        }

        private static Image<Rgba32> Render3DMetallicSurface(Image<L8> mask, int width, int height)
        {
            // Extract raw mask alpha values (0 to 255 continuous subpixel coverage)
            float[,] alphaGrid = new float[width, height];
            mask.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        alphaGrid[x, y] = row[x].PackedValue / 255.0f;
                    }
                }
            });

            // 1. Subpixel Continuous Distance Transform inside the shape
            float[,] distInside = ComputeSubpixelDistanceTransform(alphaGrid, width, height);

            // 2. Bevel Height Map Generation with smooth fillet curve
            const float maxBevelRadius = 4.8f;
            float[,] heightMap = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float d = distInside[x, y];
                    if (d > 0.001f)
                    {
                        float t = Math.Min(1.0f, d / maxBevelRadius);
                        // Smooth chiseled profile with rounded specular crown
                        heightMap[x, y] = (float)Math.Sin(t * (Math.PI / 2.0)) * maxBevelRadius;
                    }
                }
            }

            // 3. Lighting Setup (Forged Iron / Burnished Cold Steel)
            // Primary elevated light from top-left
            float lx = -0.60f, ly = -0.65f, lz = 0.75f;
            float lLen = (float)Math.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= lLen; ly /= lLen; lz /= lLen;

            // View vector V = (0, 0, 1), Half vector H = normalize(L + V)
            float hx = lx, hy = ly, hz = lz + 1.0f;
            float hLen = (float)Math.Sqrt(hx * hx + hy * hy + hz * hz);
            hx /= hLen; hy /= hLen; hz /= hLen;

            // Secondary ambient fill light from bottom-right to capture forged iron bevel facets
            float l2x = 0.50f, l2y = 0.50f, l2z = 0.40f;
            float l2Len = (float)Math.Sqrt(l2x * l2x + l2y * l2y + l2z * l2z);
            l2x /= l2Len; l2y /= l2Len; l2z /= l2Len;

            Image<Rgba32> output = new Image<Rgba32>(width, height);

            // First pass: Soft Ambient Drop Shadow
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sx = x - 3;
                    int sy = y - 4;
                    if (sx >= 0 && sx < width && sy >= 0 && sy < height)
                    {
                        float a = alphaGrid[sx, sy];
                        if (a > 0.001f)
                        {
                            byte shadowA = (byte)Math.Clamp((int)(a * 180f), 0, 255);
                            output[x, y] = new Rgba32(0, 0, 0, shadowA);
                        }
                    }
                }
            }

            // Second pass: 3D Forged Iron / Steel Surface Shading
            const float bevelStrength = 1.7f;

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float coverage = alphaGrid[x, y];
                    if (coverage < 0.01f)
                        continue;

                    // Smooth surface normal calculation
                    float dzdx = (heightMap[x + 1, y] - heightMap[x - 1, y]) * 0.5f * bevelStrength;
                    float dzdy = (heightMap[x, y + 1] - heightMap[x, y - 1]) * 0.5f * bevelStrength;

                    float nx = -dzdx;
                    float ny = -dzdy;
                    float nz = 1.0f;

                    float nLen = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (nLen > 0.0001f)
                    {
                        nx /= nLen; ny /= nLen; nz /= nLen;
                    }

                    // 1. Diffuse Term
                    float nDotL = Math.Max(0.0f, nx * lx + ny * ly + nz * lz);
                    float nDotL2 = Math.Max(0.0f, nx * l2x + ny * l2y + nz * l2z);

                    // 2. Specular Term (Forged steel gloss & sharp crest glint)
                    float nDotH = Math.Max(0.0f, nx * hx + ny * hy + nz * hz);
                    float spec = (float)Math.Pow(nDotH, 14.0);
                    float sharpGlint = (float)Math.Pow(nDotH, 42.0) * 1.5f;

                    // 3. Forged Iron Light Intensity
                    float ambient = 0.24f;
                    float lightIntensity = ambient + 0.68f * nDotL + 0.20f * nDotL2;

                    // 4. Smooth Edge Chiseled Dark Border
                    float edgeDist = distInside[x, y];
                    float edgeFactor = Math.Min(1.0f, edgeDist / 1.6f);

                    // Forged Iron & Steel Color Palette:
                    // Dark Charcoal/Graphite Shadow: (24, 26, 30)
                    // Cold Gunmetal/Iron Midtone:   (85, 95, 108)
                    // Polished Steel Crest:         (185, 195, 210)
                    // Specular Glint Highlight:     (240, 245, 252)

                    float r, g, b;

                    if (lightIntensity < 0.50f)
                    {
                        float t = lightIntensity / 0.50f;
                        r = Lerp(24f, 85f, t);
                        g = Lerp(26f, 95f, t);
                        b = Lerp(30f, 108f, t);
                    }
                    else
                    {
                        float t = (lightIntensity - 0.50f) / 0.50f;
                        r = Lerp(85f, 190f, t);
                        g = Lerp(95f, 200f, t);
                        b = Lerp(108f, 215f, t);
                    }

                    // Apply Specular Reflection (Burnished Steel Flash)
                    r += (245f - r) * (spec * 0.75f + sharpGlint);
                    g += (250f - g) * (spec * 0.75f + sharpGlint);
                    b += (255f - b) * (spec * 0.80f + sharpGlint);

                    // Outer Chiseled Rim Darkening
                    r = Lerp(12f, r, edgeFactor);
                    g = Lerp(14f, g, edgeFactor);
                    b = Lerp(16f, b, edgeFactor);

                    // Smooth edge coverage blending (Hermite smoothstep for anti-aliasing)
                    float smoothAlpha = coverage * coverage * (3.0f - 2.0f * coverage);

                    byte finalR = (byte)Math.Clamp((int)r, 0, 255);
                    byte finalG = (byte)Math.Clamp((int)g, 0, 255);
                    byte finalB = (byte)Math.Clamp((int)b, 0, 255);
                    byte finalA = (byte)Math.Clamp((int)(smoothAlpha * 255f), 0, 255);

                    // Alpha blend over existing shadow
                    Rgba32 under = output[x, y];
                    if (under.A > 0)
                    {
                        float srcA = finalA / 255.0f;
                        float dstA = (under.A / 255.0f) * (1.0f - srcA);
                        float outA = srcA + dstA;

                        if (outA > 0.001f)
                        {
                            byte blendR = (byte)Math.Clamp((int)((finalR * srcA + under.R * dstA) / outA), 0, 255);
                            byte blendG = (byte)Math.Clamp((int)((finalG * srcA + under.G * dstA) / outA), 0, 255);
                            byte blendB = (byte)Math.Clamp((int)((finalB * srcA + under.B * dstA) / outA), 0, 255);
                            byte blendA = (byte)Math.Clamp((int)(outA * 255f), 0, 255);
                            output[x, y] = new Rgba32(blendR, blendG, blendB, blendA);
                        }
                    }
                    else
                    {
                        output[x, y] = new Rgba32(finalR, finalG, finalB, finalA);
                    }
                }
            }

            return output;
        }

        private static float[,] ComputeSubpixelDistanceTransform(float[,] alpha, int width, int height)
        {
            float[,] dist = new float[width, height];
            const float INF = 1e6f;

            // Initialize with subpixel boundary estimates
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float a = alpha[x, y];
                    if (a <= 0.001f)
                    {
                        dist[x, y] = 0.0f;
                    }
                    else if (a < 0.999f)
                    {
                        // Subpixel boundary transition distance
                        dist[x, y] = a * 0.75f;
                    }
                    else
                    {
                        dist[x, y] = INF;
                    }
                }
            }

            // Forward Pass
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (dist[x, y] == 0.0f)
                        continue;

                    float d = dist[x, y];
                    if (x > 0) d = Math.Min(d, dist[x - 1, y] + 1.0f);
                    if (y > 0) d = Math.Min(d, dist[x, y - 1] + 1.0f);
                    if (x > 0 && y > 0) d = Math.Min(d, dist[x - 1, y - 1] + 1.4142f);
                    if (x < width - 1 && y > 0) d = Math.Min(d, dist[x + 1, y - 1] + 1.4142f);
                    dist[x, y] = d;
                }
            }

            // Backward Pass
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    if (dist[x, y] == 0.0f)
                        continue;

                    float d = dist[x, y];
                    if (x < width - 1) d = Math.Min(d, dist[x + 1, y] + 1.0f);
                    if (y < height - 1) d = Math.Min(d, dist[x, y + 1] + 1.0f);
                    if (x < width - 1 && y < height - 1) d = Math.Min(d, dist[x + 1, y + 1] + 1.4142f);
                    if (x > 0 && y < height - 1) d = Math.Min(d, dist[x - 1, y + 1] + 1.4142f);
                    dist[x, y] = d;
                }
            }

            return dist;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0.0f, 1.0f);
        }
    }
}
