using ClassicUO.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Texmaps
{
    public sealed class Texmap
    {
        private readonly TextureAtlas _atlas;
        private readonly SpriteInfo[] _spriteInfos;
        private readonly PixelPicker _picker = new PixelPicker();
        private readonly TexmapsLoader _texmapsLoader;

        public Texmap(TexmapsLoader texmapsLoader, GraphicsDevice device)
        {
            _texmapsLoader = texmapsLoader;
            _atlas = new TextureAtlas(device, 4096, 4096, SurfaceFormat.Color);
            _spriteInfos = new SpriteInfo[texmapsLoader.File.Entries.Length];
        }

        public ref readonly SpriteInfo GetTexmap(uint idx)
        {
            if (idx >= _spriteInfos.Length)
                return ref SpriteInfo.Empty;

            ref var spriteInfo = ref _spriteInfos[idx];

            if (spriteInfo.Texture == null)
            {
                int origW = 0, origH = 0;
                _texmapsLoader.TryGetTexmapSize(idx, out origW, out origH);

                if (CustomArtOverride.TryLoadTexmap(idx, origW, origH, out var overridePixels, out int ow, out int oh))
                {
                    spriteInfo.Texture = _atlas.AddSprite(overridePixels, ow, oh, out spriteInfo.UV);
                    spriteInfo.LogicalSize = new Point(origW > 0 ? origW : ow, origH > 0 ? origH : oh);
                    _picker.Set(idx, ow, oh, overridePixels);
                    return ref spriteInfo;
                }

                var texmapInfo = _texmapsLoader.GetTexmap(idx);
                if (!texmapInfo.Pixels.IsEmpty)
                {
                    spriteInfo.Texture = _atlas.AddSprite(
                        texmapInfo.Pixels,
                        texmapInfo.Width,
                        texmapInfo.Height,
                        out spriteInfo.UV
                    );
                    spriteInfo.LogicalSize = new Point(texmapInfo.Width, texmapInfo.Height);

                    _picker.Set(idx, texmapInfo.Width, texmapInfo.Height, texmapInfo.Pixels);
                }
            }

            return ref spriteInfo;
        }
    }
}
