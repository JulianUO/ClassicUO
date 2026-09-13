// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Globalization;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal class DiscoveredPlaceGump : Gump
    {
        private readonly string _formattedPlaceName;
        private readonly double _createdTime;
        private const double DISPLAY_DURATION = 5500.0; // 5.5 seconds total lifespan
        private const double FADE_IN_DURATION = 500.0;   // 0.5s fade in
        private const double FADE_OUT_DURATION = 950.0;  // 0.95s fade out

        public DiscoveredPlaceGump(World world, string placeName) : base(world, 0, 0)
        {
            CanMove = false;
            CanCloseWithRightClick = false;
            AcceptMouseInput = false;
            AcceptKeyboardInput = false;
            LayerOrder = UILayer.Over;

            _createdTime = Time.Ticks;

            if (string.IsNullOrWhiteSpace(placeName))
            {
                _formattedPlaceName = "Britannia";
            }
            else
            {
                TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
                _formattedPlaceName = textInfo.ToTitleCase(placeName.Trim().ToLowerInvariant());
            }

            Texture2D texture = DiscoveredPlaceRenderer.GetOrCreateBannerTexture(Client.Game.GraphicsDevice, _formattedPlaceName);
            if (texture != null)
            {
                Width = texture.Width;
                Height = texture.Height;
            }
            else
            {
                Width = 450;
                Height = 130;
            }

            Alpha = 0.0f;
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var scene = Client.Game.GetScene<GameScene>();
            int screenWidth = scene != null ? scene.Camera.Bounds.Width : Client.Game.ClientBounds.Width;
            X = (screenWidth - Width) / 2;
            Y = 50;
        }

        public override void Update()
        {
            base.Update();

            if (IsDisposed)
                return;

            UpdatePosition();

            double elapsed = Time.Ticks - _createdTime;
            if (elapsed >= DISPLAY_DURATION)
            {
                Dispose();
                return;
            }

            if (elapsed < FADE_IN_DURATION)
            {
                Alpha = MathHelper.Clamp((float)(elapsed / FADE_IN_DURATION), 0.0f, 1.0f);
            }
            else if (elapsed > (DISPLAY_DURATION - FADE_OUT_DURATION))
            {
                double fadeOutElapsed = elapsed - (DISPLAY_DURATION - FADE_OUT_DURATION);
                Alpha = MathHelper.Clamp(1.0f - (float)(fadeOutElapsed / FADE_OUT_DURATION), 0.0f, 1.0f);
            }
            else
            {
                Alpha = 1.0f;
            }
        }

        public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
        {
            if (IsDisposed)
                return false;

            float layerDepth = layerDepthRef;
            float currentAlpha = Alpha;

            if (currentAlpha <= 0.001f)
                return base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);

            Texture2D texture = DiscoveredPlaceRenderer.GetOrCreateBannerTexture(Client.Game.GraphicsDevice, _formattedPlaceName);
            if (texture == null || texture.IsDisposed)
                return base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);

            Vector3 hueVector = new Vector3(0, 0, currentAlpha);

            renderLists.AddGumpNoAtlas(batcher =>
            {
                batcher.Draw(
                    texture,
                    new Rectangle(x, y, Width, Height),
                    hueVector,
                    layerDepth
                );
                return true;
            });

            return base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);
        }
    }
}

