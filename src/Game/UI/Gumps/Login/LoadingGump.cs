﻿#region license
// Copyright (C) 2020 ClassicUO Development Community on Github
// 
// This project is an alternative client for the game Ultima Online.
// The goal of this is to develop a lightweight client considering
// new technologies.
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.
#endregion

using System;

using ClassicUO.Game.UI.Controls;
using ClassicUO.IO.Resources;

using SDL2;

namespace ClassicUO.Game.UI.Gumps.Login
{
    [Flags]
    enum LoginButtons
    {
        None = 1,
        OK = 2,
        Cancel = 4
    }

    class LoadingGump : Gump
    {  

        private readonly Action<int> _buttonClick;
        private readonly Label _label;

        public LoadingGump(string labelText, LoginButtons showButtons, Action<int> buttonClick = null) : base(0, 0)
        {
            _buttonClick = buttonClick;
            CanCloseWithRightClick = false;
            CanCloseWithEsc = false;

            _label = new Label(labelText, false, 0, 326, 3, align: TEXT_ALIGN_TYPE.TS_CENTER)
            {
                X = 173,
                Y = 178
            };

            Add(new ResizePic(0x06DB)
            {
                X = 150, Y = 134, Width = 379, Height = 212
            });

            Add(new ResizePic(0x0DAC)
            {
                X = 170,
                Y = 154,
                Width = 379 - 40,
                Height = 212 - 40
            });

            Add(_label);

            if (showButtons == LoginButtons.OK)
            {
                Add(new Button((int) LoginButtons.OK, 0x0601, 0x0602)
                {
                    X = 327, Y = 285, ButtonAction = ButtonAction.Activate
                });
            }
            else if (showButtons == (LoginButtons.OK | LoginButtons.Cancel))
            {
                Add(new Button((int) LoginButtons.OK, 0x0601, 0x0602)
                {
                    X = 310, Y = 285, ButtonAction = ButtonAction.Activate
                });

                Add(new Button((int) LoginButtons.Cancel, 0x05FF, 0x0600)
                {
                    X = 324, Y = 285, ButtonAction = ButtonAction.Activate
                });
            }
        }

        public void SetText(string text)
        {
            _label.Text = text;
        }
        protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
        {
            if (key == SDL.SDL_Keycode.SDLK_KP_ENTER || key == SDL.SDL_Keycode.SDLK_RETURN)
                OnButtonClick((int) LoginButtons.OK);
        }


        public override void OnButtonClick(int buttonID)
        {
            _buttonClick?.Invoke(buttonID);
            base.OnButtonClick(buttonID);
        }
    }
}