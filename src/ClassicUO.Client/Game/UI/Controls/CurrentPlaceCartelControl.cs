// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Globalization;
using ClassicUO.Assets;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Controls
{
    internal class CurrentPlaceCartelControl : Control
    {
        private string _placeName = "Britannia";
        private RenderedText _placeText;
        private const ushort BUTTON_GUMP = 1588;

        public CurrentPlaceCartelControl(string initialPlace = "Britannia")
        {
            AcceptMouseInput = false;
            CanMove = false;

            _placeName = FormatPlaceName(initialPlace);

            ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(BUTTON_GUMP);
            int baseWidth = gumpInfo.Texture != null ? gumpInfo.UV.Width : 110;
            int baseHeight = gumpInfo.Texture != null ? gumpInfo.UV.Height : 26;

            _placeText = RenderedText.Create(_placeName, 0x0386, 1, true);

            Width = Math.Max(baseWidth, _placeText.Width + 16);
            Height = baseHeight;
        }

        private static string FormatPlaceName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Britannia";

            TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
            return textInfo.ToTitleCase(text.Trim().ToLowerInvariant());
        }

        public string PlaceName
        {
            get => _placeName;
            set
            {
                string formatted = FormatPlaceName(value);
                if (_placeName != formatted)
                {
                    _placeName = formatted;
                    _placeText?.Destroy();
                    _placeText = RenderedText.Create(_placeName, 0x0386, 1, true);

                    ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(BUTTON_GUMP);
                    int baseWidth = gumpInfo.Texture != null ? gumpInfo.UV.Width : 110;
                    Width = Math.Max(baseWidth, _placeText.Width + 16);
                }
            }
        }

        public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
        {
            if (IsDisposed)
                return false;

            base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);

            float layerDepth = layerDepthRef;
            ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(BUTTON_GUMP);
            Texture2D texture = gumpInfo.Texture;
            Rectangle uv = gumpInfo.UV;

            if (texture == null)
                return false;

            var hue = ShaderHueTranslator.GetHueVector(0, false, Alpha, true);

            renderLists.AddGumpWithAtlas(batcher =>
            {
                batcher.Draw(texture, new Rectangle(x, y, Width, Height), uv, hue, layerDepth);
                return true;
            });

            if (_placeText != null)
            {
                renderLists.AddGumpNoAtlas(
                    _placeText,
                    x + ((Width - _placeText.Width) >> 1),
                    y + ((Height - _placeText.Height) >> 1),
                    layerDepth
                );
            }

            return true;
        }

        public override void Dispose()
        {
            _placeText?.Destroy();
            base.Dispose();
        }
    }
}

