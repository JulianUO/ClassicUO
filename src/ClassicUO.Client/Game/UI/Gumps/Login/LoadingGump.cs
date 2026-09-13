// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Assets;
using SDL3;

namespace ClassicUO.Game.UI.Gumps.Login
{
    [Flags]
    internal enum LoginButtons
    {
        None = 1,
        OK = 2,
        Cancel = 4
    }

    internal class LoadingGump : Gump
    {
        private readonly Action<int> _buttonClick;
        private readonly Label _label;

        public LoadingGump(World world, string labelText, LoginButtons showButtons, Action<int> buttonClick = null) : base(world, 0, 0)
        {
            _buttonClick = buttonClick;
            CanCloseWithRightClick = false;
            CanCloseWithEsc = false;

            bool isAsianLang = string.Compare(Settings.GlobalSettings.Language, "CHT", StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(Settings.GlobalSettings.Language, "KOR", StringComparison.InvariantCultureIgnoreCase) == 0 ||
                string.Compare(Settings.GlobalSettings.Language, "JPN", StringComparison.InvariantCultureIgnoreCase) == 0;

            bool unicode = isAsianLang;
            byte font = (byte)(isAsianLang ? 1 : 3);
            ushort hue = (ushort)(isAsianLang ? 0xFFFF : 0x0);

            _label = new Label
            (
                labelText,
                unicode,
                hue,
                326,
                font,
                align: TEXT_ALIGN_TYPE.TS_CENTER
            )
            {
                X = 173,
                Y = 178
            };

            Add
            (
                new ResizePic(0x06DB)
                {
                    X = 150, Y = 134, Width = 379, Height = 212
                }
            );

            Add
            (
                new ResizePic(0x0DAC)
                {
                    X = 170,
                    Y = 154,
                    Width = 379 - 40,
                    Height = 212 - 40
                }
            );

            Add(_label);

            if (showButtons == LoginButtons.OK)
            {
                Add
                (
                    new Button((int) LoginButtons.OK, 0x601, 0x602)
                    {
                        X = 327, Y = 285, ButtonAction = ButtonAction.Activate
                    }
                );
            }
            else if (showButtons == LoginButtons.Cancel)
            {
                Add
                (
                    new Button((int) LoginButtons.Cancel, 0x05FF, 0x0600)
                    {
                        X = 327,
                        Y = 285,
                        ButtonAction = ButtonAction.Activate
                    }
                );
            }
            else if (showButtons == (LoginButtons.OK | LoginButtons.Cancel))
            {
                Add
                (
                    new Button((int) LoginButtons.OK, 0x0601, 0x0602)
                    {
                        X = 310, Y = 285, ButtonAction = ButtonAction.Activate
                    }
                );

                Add
                (
                    new Button((int) LoginButtons.Cancel, 0x05FF, 0x0600)
                    {
                        X = 324, Y = 285, ButtonAction = ButtonAction.Activate
                    }
                );
            }
        }

        public void SetText(string text)
        {
            _label.Text = text;
        }

        protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
        {
            if (key == SDL.SDL_Keycode.SDLK_KP_ENTER || key == SDL.SDL_Keycode.SDLK_RETURN)
            {
                OnButtonClick((int) LoginButtons.OK);
            }
        }


        public override void OnButtonClick(int buttonID)
        {
            _buttonClick?.Invoke(buttonID);
            base.OnButtonClick(buttonID);
        }
    }
}