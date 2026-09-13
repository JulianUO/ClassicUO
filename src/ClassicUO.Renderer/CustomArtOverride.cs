using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ClassicUO.Renderer
{
    /// <summary>
    /// Loads optional higher-resolution PNG/BMP replacements for art and texmaps.
    /// Files live under CustomArt/art/land, CustomArt/art/static, and CustomArt/texmaps.
    /// 8-bit alpha is preserved; 24-bit BMP uses black and magenta as transparency.
    /// </summary>
    public static class CustomArtOverride
    {
        private static GraphicsDevice _device;
        private static string[] _searchRoots = Array.Empty<string>();
        private static bool _indexed;
        private static readonly Dictionary<int, string> _landFiles = new();
        private static readonly Dictionary<int, string> _staticFiles = new();
        private static readonly Dictionary<int, string> _texmapFiles = new();

        public static void Initialize(GraphicsDevice device, params string[] searchRoots)
        {
            _device = device;
            _searchRoots = searchRoots ?? Array.Empty<string>();
            _indexed = false;
            _landFiles.Clear();
            _staticFiles.Clear();
            _texmapFiles.Clear();
        }

        public static bool TryLoadLand(uint idx, int originalWidth, int originalHeight, out uint[] pixels, out int width, out int height)
            => TryLoad(_landFiles, (int)idx, originalWidth, originalHeight, out pixels, out width, out height);

        public static bool TryLoadStatic(uint idx, int originalWidth, int originalHeight, out uint[] pixels, out int width, out int height)
            => TryLoad(_staticFiles, (int)idx, originalWidth, originalHeight, out pixels, out width, out height);

        public static bool TryLoadTexmap(uint idx, int originalWidth, int originalHeight, out uint[] pixels, out int width, out int height)
            => TryLoad(_texmapFiles, (int)idx, originalWidth, originalHeight, out pixels, out width, out height);

        private static bool TryLoad(
            Dictionary<int, string> files,
            int idx,
            int originalWidth,
            int originalHeight,
            out uint[] pixels,
            out int width,
            out int height)
        {
            pixels = null;
            width = 0;
            height = 0;

            if (_device == null)
                return false;

            EnsureIndex();

            if (!files.TryGetValue(idx, out string path) || !File.Exists(path))
                return false;

            if (!TryReadImage(path, out pixels, out width, out height))
                return false;

            bool missingOriginal = originalWidth <= 0 || originalHeight <= 0;
            bool larger =
                width >= originalWidth
                && height >= originalHeight
                && (width > originalWidth || height > originalHeight);

            if (!missingOriginal && !larger)
            {
                pixels = null;
                width = 0;
                height = 0;
                return false;
            }

            return true;
        }

        private static void EnsureIndex()
        {
            if (_indexed)
                return;

            _indexed = true;

            foreach (string root in _searchRoots)
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                IndexDirectory(Path.Combine(root, "art", "land"), _landFiles);
                IndexDirectory(Path.Combine(root, "art", "static"), _staticFiles);
                IndexDirectory(Path.Combine(root, "texmaps"), _texmapFiles);
            }

            if (_landFiles.Count + _staticFiles.Count + _texmapFiles.Count > 0)
            {
                Log.Trace(
                    $"CustomArt: {_landFiles.Count} land, {_staticFiles.Count} static, {_texmapFiles.Count} texmap overrides"
                );
            }
        }

        private static void IndexDirectory(string directory, Dictionary<int, string> map)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (string file in Directory.EnumerateFiles(directory))
            {
                string ext = Path.GetExtension(file);
                if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    && !ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryParseId(Path.GetFileNameWithoutExtension(file), out int id))
                    continue;

                if (!map.TryGetValue(id, out string existing))
                {
                    map[id] = file;
                    continue;
                }

                bool existingBmp = existing.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase);
                bool incomingPng = ext.Equals(".png", StringComparison.OrdinalIgnoreCase);
                if (existingBmp && incomingPng)
                    map[id] = file;
            }
        }

        private static bool TryParseId(string name, out int id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("&h", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(name.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id)
                    && id >= 0;
            }

            return int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id >= 0;
        }

        private static bool TryReadImage(string path, out uint[] pixels, out int width, out int height)
        {
            pixels = null;
            width = 0;
            height = 0;

            try
            {
                using FileStream stream = File.OpenRead(path);
                using Texture2D texture = Texture2D.FromStream(_device, stream);

                width = texture.Width;
                height = texture.Height;
                pixels = new uint[width * height];
                texture.GetData(pixels);
            }
            catch (Exception ex)
            {
                Log.Warn($"CustomArt: failed to load {path}: {ex.Message}");
                pixels = null;
                width = 0;
                height = 0;
                return false;
            }

            bool isBmp = path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase);
            bool anyTransparent = false;
            bool anyPartialAlpha = false;
            bool allOpaque = true;

            for (int i = 0; i < pixels.Length; i++)
            {
                byte a = (byte)(pixels[i] >> 24);
                if (a == 0)
                {
                    pixels[i] = 0;
                    anyTransparent = true;
                    allOpaque = false;
                }
                else if (a != 255)
                {
                    anyPartialAlpha = true;
                    allOpaque = false;
                }
            }

            if (isBmp && allOpaque && !anyPartialAlpha && !anyTransparent)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    uint p = pixels[i];
                    // PackedValue is 0xAABBGGRR. Magenta RGB(255,0,255) = 0xFFFF00FF; black = 0xFF000000.
                    if (p == 0xFF_00_00_00 || p == 0xFF_FF_00_FF)
                        pixels[i] = 0;
                }
            }

            return true;
        }
    }
}
