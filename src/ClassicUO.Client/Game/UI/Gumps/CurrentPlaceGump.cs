// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Assets;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class CurrentPlaceGump : Gump
    {
        private readonly Label _label;
        private double _createdTime;
        private const double DISPLAY_DURATION = 5000.0; // 5 seconds total
        private const double FADE_DURATION = 1000.0;   // last 1 second fade out

        public CurrentPlaceGump(World world, string placeName) : base(world, 0, 0)
        {
            CanMove = false;
            CanCloseWithRightClick = false;
            AcceptMouseInput = false;
            AcceptKeyboardInput = false;
            LayerOrder = UILayer.Over;

            _createdTime = Time.Ticks;

            _label = new Label
            (
                placeName,
                false,
                0xFFFF,
                0,
                3,
                FontStyle.BlackBorder,
                TEXT_ALIGN_TYPE.TS_CENTER
            );

            Width = _label.Width + 20;
            Height = _label.Height + 10;

            _label.X = (Width - _label.Width) / 2;
            _label.Y = (Height - _label.Height) / 2;

            Add(_label);

            UpdatePosition();
        }

        public string PlaceName
        {
            get => _label.Text;
            set
            {
                _label.Text = value;
                Width = _label.Width + 20;
                Height = _label.Height + 10;
                _label.X = (Width - _label.Width) / 2;
                _label.Y = (Height - _label.Height) / 2;
                _createdTime = Time.Ticks;
                Alpha = 1.0f;
                UpdatePosition();
            }
        }

        public void ResetTimer()
        {
            _createdTime = Time.Ticks;
            Alpha = 1.0f;
        }

        private void UpdatePosition()
        {
            var scene = Client.Game.GetScene<GameScene>();
            int screenWidth = scene != null ? scene.Camera.Bounds.Width : Client.Game.ClientBounds.Width;
            X = (screenWidth - Width) / 2;
            Y = 25;
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

            if (elapsed > (DISPLAY_DURATION - FADE_DURATION))
            {
                double fadeProgress = (elapsed - (DISPLAY_DURATION - FADE_DURATION)) / FADE_DURATION;
                Alpha = MathHelper.Clamp(1.0f - (float)fadeProgress, 0.0f, 1.0f);
            }
            else
            {
                Alpha = 1.0f;
            }
        }
    }
}
