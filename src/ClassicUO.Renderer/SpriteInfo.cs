using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    public struct SpriteInfo
    {
        public Texture2D Texture;
        public Rectangle UV;
        public Point Center;
        /// <summary>
        /// On-screen size in classic UO pixels. When an HD override is packed into
        /// <see cref="UV"/>, this stays at the original MUL dimensions so layout is unchanged.
        /// </summary>
        public Point LogicalSize;

        public int DrawWidth => LogicalSize.X > 0 ? LogicalSize.X : UV.Width;
        public int DrawHeight => LogicalSize.Y > 0 ? LogicalSize.Y : UV.Height;

        public static readonly SpriteInfo Empty = new SpriteInfo { Texture = null };
    }
}
