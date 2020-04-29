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

using ClassicUO.Configuration;
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

            Add(new Button((int)Buttons.Prev, 0x0605, 0x0606)
            {
                X = 575,
                Y = 445,
                ButtonAction = ButtonAction.Activate
            });

            Add(new Button((int)Buttons.Next, 0x603, 0x0604)
            {
                X = 602,
                Y = 445,
                ButtonAction = ButtonAction.Activate
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
                X = 170,
                Y = 110,
                Width = 393 - 14 - 40,
                Height = 300 - 40
            });
            // Sever Scroll Area
            ScrollArea scrollArea = new ScrollArea(150, 130, 383, 271, true);
            LoginScene loginScene = Client.Game.GetScene<LoginScene>();

            scrollArea.ScissorRectangle.Y = 16;
            scrollArea.ScissorRectangle.Height = -(scrollArea.ScissorRectangle.Y + 32);

            foreach (ServerListEntry server in loginScene.Servers)
            {
                scrollArea.Add(new ServerEntryGump(server, 5, NORMAL_COLOR, SELECTED_COLOR));
            }

            Add(scrollArea);

            if (loginScene.Servers.Length != 0)
            {
                int index = Settings.GlobalSettings.LastServerNum - 1;

                if (index < 0 || index >= loginScene.Servers.Length)
                {
                    index = 0;
                }

                Add(new Label(loginScene.Servers[index].Name, false, 0x0481, font: 9)
                {
                    X = 243,
                    Y = 420
                });
            }

            AcceptKeyboardInput = true;
            CanCloseWithRightClick = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            LoginScene loginScene = Client.Game.GetScene<LoginScene>();

            if (buttonID >= (int)Buttons.Server)
            {
                int index = buttonID - (int)Buttons.Server;
                loginScene.SelectServer((byte)index);
            }
            else
            {
                switch ((Buttons)buttonID)
                {
                    case Buttons.Next:
                    case Buttons.Earth:

                        if (loginScene.Servers.Any())
                        {
                            int index = Settings.GlobalSettings.LastServerNum;

                            if (index <= 0 || index > loginScene.Servers.Length)
                            {
                                Log.Warn($"Wrong server index: {index}");

                                index = 1;
                            }

                            loginScene.SelectServer((byte)loginScene.Servers[index - 1].Index);
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
                LoginScene loginScene = Client.Game.GetScene<LoginScene>();

                if (loginScene.Servers.Any())
                {
                    int index = Settings.GlobalSettings.LastServerNum;

                    if (index <= 0 || index > loginScene.Servers.Length)
                    {
                        Log.Warn($"Wrong server index: {index}");

                        index = 1;
                    }

                    loginScene.SelectServer((byte)loginScene.Servers[index - 1].Index);
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

        private class ServerEntryGump : ScrollAreaItem
        {
            private readonly int _buttonId;

            private readonly HoveredLabel _serverName;
            private readonly HoveredLabel _server_ping;
            private readonly HoveredLabel _server_packet_loss;
            private readonly ServerListEntry _entry;

            public ServerEntryGump(ServerListEntry entry, byte font, ushort normal_hue, ushort selected_hue)
            {
                _entry = entry;

                _buttonId = entry.Index;

                Add(_serverName = new HoveredLabel(entry.Name, false, normal_hue, selected_hue, selected_hue, font: font)
                {
                    X = 74,
                    AcceptMouseInput = false
                });
                Add(_server_ping = new HoveredLabel("-", false, normal_hue, selected_hue, selected_hue, font: font)
                {
                    X = 250,
                    AcceptMouseInput = false
                });
                Add(_server_packet_loss = new HoveredLabel("-", false, normal_hue, selected_hue, selected_hue, font: font)
                {
                    X = 320,
                    AcceptMouseInput = false
                });


                AcceptMouseInput = true;
                Width = 393;
                Height = 25;

                WantUpdateSize = false;
            }

            protected override void OnMouseEnter(int x, int y)
            {
                base.OnMouseEnter(x, y);

                _serverName.IsSelected = true;
                _server_packet_loss.IsSelected = true;
                _server_ping.IsSelected = true;
            }

            protected override void OnMouseExit(int x, int y)
            {
                base.OnMouseExit(x, y);

                _serverName.IsSelected = false;
                _server_packet_loss.IsSelected = false;
                _server_ping.IsSelected = false;
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                if (button == MouseButtonType.Left) 
                    OnButtonClick((int)Buttons.Server + _buttonId);
            }
        }
    }
}