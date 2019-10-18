#region license

//  Copyright (C) 2019 ClassicUO Development Community on Github
//
//	This project is an alternative client for the game Ultima Online.
//	The goal of this is to develop a lightweight client considering 
//	new technologies.  
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

using System.Linq;

using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.IO;
using ClassicUO.IO.Resources;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using SDL2;

namespace ClassicUO.Game.UI.Gumps.Login
{
    internal class ServerSelectionGump : Gump
    {
        private const ushort SELECTED_COLOR = 0x0;
        private const ushort NORMAL_COLOR = 0x0;

        public ServerSelectionGump() : base(0, 0)
        {
            //AddChildren(new LoginBackground(true));

            Add(new Button((int) Buttons.Prev, 0x0605, 0x0606)
            {
                X = 575, Y = 445, ButtonAction = ButtonAction.Activate
            });

            Add(new Button((int) Buttons.Next, 0x603, 0x0604)
            {
                X = 602, Y = 445, ButtonAction = ButtonAction.Activate
            });
            
            Add(new ResizePic(0x06DB)
            {
                X = 150,
                Y = 90,
                Width = 393 - 14,
                Height = 300
            });

            // Server Scroll Area Bg
            Add(new ResizePic(0x0DAC)
            {
                X = 170, Y = 110, Width = 393 - 14 - 40, Height = 300 - 40
            });
            // Sever Scroll Area
            ScrollArea scrollArea = new ScrollArea(150, 130, 383, 271, true);
            LoginScene loginScene = Engine.SceneManager.GetScene<LoginScene>();

            foreach (ServerListEntry server in loginScene.Servers)
            {
                HoveredLabel label;
                scrollArea.Add(label = new HoveredLabel($"{server.Name}                         -           -", false, NORMAL_COLOR, SELECTED_COLOR, font: 3)
                {
                    X = 50,
                    //Y = 250
                });

                label.MouseUp += (sender, e) => { OnButtonClick((int) (Buttons.Server + server.Index)); };
            }

            Add(scrollArea);
            AcceptKeyboardInput = true;
            CanCloseWithRightClick = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            LoginScene loginScene = Engine.SceneManager.GetScene<LoginScene>();

            if (buttonID >= (int) Buttons.Server)
            {
                int index = buttonID - (int) Buttons.Server;
                loginScene.SelectServer((byte) index);
            }
            else
            {
                switch ((Buttons) buttonID)
                {
                    case Buttons.Next:
                    case Buttons.Earth:

                        if (loginScene.Servers.Any())
                        {
                            int index = Engine.GlobalSettings.LastServerNum;

                            if (index <= 0 || index > loginScene.Servers.Length)
                            {
                                Log.Message(LogTypes.Warning, $"Wrong server index: {index}");

                                index = 1;
                            }

                            loginScene.SelectServer((byte) loginScene.Servers[index - 1].Index);
                        }

                        break;

                    case Buttons.Prev:
                        loginScene.StepBack();

                        break;
                }
            }
        }

        protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
        {
            if (key == SDL.SDL_Keycode.SDLK_RETURN || key == SDL.SDL_Keycode.SDLK_KP_ENTER)
            {
                LoginScene loginScene = Engine.SceneManager.GetScene<LoginScene>();

                if (loginScene.Servers.Any())
                {
                    int index = Engine.GlobalSettings.LastServerNum;

                    if (index <= 0 || index > loginScene.Servers.Length)
                    {
                        Log.Message(LogTypes.Warning, $"Wrong server index: {index}");

                        index = 1;
                    }

                    loginScene.SelectServer((byte) loginScene.Servers[index - 1].Index);
                }
            }
        }

        private enum Buttons
        {
            Prev,
            Next,
            SortTimeZone,
            SortFull,
            SortConnection,
            Earth,
            Server = 99
        }

        private class ServerEntryGump : Control
        {
            private readonly int _buttonId;

            private readonly HoveredLabel _serverName;

            public ServerEntryGump(ServerListEntry entry)
            {
                _buttonId = entry.Index;

                Add(_serverName = new HoveredLabel($"{entry.Name}     -      -" , false, NORMAL_COLOR, SELECTED_COLOR, font: 5));
                _serverName.X = 50;
                _serverName.Y = 310;

                AcceptMouseInput = true;
                Width = 393;
                Height = 25;

                WantUpdateSize = false;
            }

            protected override void OnMouseOver(int x, int y)
            {
                _serverName.Hue = SELECTED_COLOR;

                base.OnMouseOver(x, y);
            }

            protected override void OnMouseExit(int x, int y)
            {
                _serverName.Hue = 0;

                base.OnMouseExit(x, y);
            }

            protected override void OnMouseUp(int x, int y, MouseButton button)
            {
                if (button == MouseButton.Left) OnButtonClick((int)Buttons.Server + _buttonId);
            }
        }
    }
}