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

using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Utility.Logging;

using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class TopBarGump : Gump
    {
        private TopBarGump() : base(0, 0)
        {
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = false;

            // maximized view
            Add(new ResizePic(1755)
            {
                X = 0, Y = 0, Width = 710 + 133, Height = 39
            }, 1);

            Add(new Button(0, 1531, 1532)
            {
                ButtonAction = ButtonAction.SwitchPage, ToPage = 2, X = 8, Y = 9
            }, 1);

            Add(new Button((int) Buttons.Map, 1592, 1593, 0, "Map", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 32, Y = 7, FontCenter = true
            }, 1);

            Add(new Button((int) Buttons.Paperdoll, 1588, 1589, 0, "Paperdoll", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 85, Y = 7, FontCenter = true
            }, 1);

            Add(new Button((int) Buttons.Inventory, 1588, 1589, 0, "Inventario", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 200, Y = 7, FontCenter = true
            }, 1);
            
            Add(new Button((int) Buttons.Journal, 1588, 1589, 0, "Journal", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 315, Y = 7, FontCenter = true
            }, 1); // 108
            
            Add(new Button((int) Buttons.Achievements, 1588, 1589, 0, "Achievements", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 430, Y = 7, FontCenter = true
            }, 1);
            
            Add(new Button((int) Buttons.Chat, 1592, 1593, 0, "Chat", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 545, Y = 7, FontCenter = true
            }, 1);
            
            Add(new Button((int) Buttons.Help, 1592, 1593, 0, "Help", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 600, Y = 7, FontCenter = true
            }, 1);
            
            Add(new Button((int) Buttons.Debug, 1592, 1593, 0, "Debug", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 655, Y = 7, FontCenter = true
            }, 1);
            
            Add(new Button((int)Buttons.WorldMap, 1588, 1589, 0, "World Map", 1, true, 0x0386, 0)
            {
                ButtonAction = ButtonAction.Activate, X = 710, Y = 7, FontCenter = true
            }, 1);
            
            //minimized view
            Add(new ResizePic(1755)
            {
                X = 0,
                Y = 0,
                Width = 40,
                Height = 39
            }, 2);

            Add(new Button(0, 1531, 1532)
            {
                ButtonAction = ButtonAction.SwitchPage,
                ToPage = 1,
                X = 8,
                Y = 9
            }, 2);

            //layer
            ControlInfo.Layer = UILayer.Over;
        }

        public bool IsMinimized { get; private set; }

        //private static TopBarGump _gump;

        public static void Create()
        {
            TopBarGump gump = UIManager.GetGump<TopBarGump>();

            if (gump == null)
            {
                if (ProfileManager.Current.TopbarGumpPosition.X < 0 || ProfileManager.Current.TopbarGumpPosition.Y < 0)
                    ProfileManager.Current.TopbarGumpPosition = Point.Zero;

                UIManager.Add(gump = new TopBarGump
                {
                    X = ProfileManager.Current.TopbarGumpPosition.X,
                    Y = ProfileManager.Current.TopbarGumpPosition.Y
                });

                if (ProfileManager.Current.TopbarGumpIsMinimized)
                    gump.ChangePage(2);
            }
            else
                Log.Error( "TopBarGump already exists!!");
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Right && (X != 0 || Y != 0))
            {
                X = 0;
                Y = 0;

                ProfileManager.Current.TopbarGumpPosition = Location;
            }
        }

        public override void OnPageChanged()
        {
            ProfileManager.Current.TopbarGumpIsMinimized = IsMinimized = ActivePage == 2;
            WantUpdateSize = true;
        }

        protected override void OnDragEnd(int x, int y)
        {
            base.OnDragEnd(x, y);
            ProfileManager.Current.TopbarGumpPosition = Location;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch ((Buttons) buttonID)
            {
                case Buttons.Map:
                    MiniMapGump miniMapGump = UIManager.GetGump<MiniMapGump>();

                    if (miniMapGump == null)
                        UIManager.Add(new MiniMapGump());
                    else
                    {
                        miniMapGump.SetInScreen();
                        miniMapGump.BringOnTop();
                    }

                    break;

                case Buttons.Paperdoll:
                    PaperDollGump paperdollGump = UIManager.GetGump<PaperDollGump>(World.Player);

                    if (paperdollGump == null)
                        GameActions.OpenPaperdoll(World.Player);
                    else
                    {
                        paperdollGump.SetInScreen();
                        paperdollGump.BringOnTop();
                    }

                    break;

                case Buttons.Inventory:
                    Item backpack = World.Player.Equipment[(int) Layer.Backpack];

                    ContainerGump backpackGump = UIManager.GetGump<ContainerGump>(backpack);

                    if (backpackGump == null)
                        GameActions.DoubleClick(backpack);
                    else
                    {
                        backpackGump.SetInScreen();
                        backpackGump.BringOnTop();
                    }

                    break;

                case Buttons.Journal:
                    JournalGump journalGump = UIManager.GetGump<JournalGump>();

                    if (journalGump == null)
                    {
                        UIManager.Add(new JournalGump
                                          {X = 64, Y = 64});
                    }
                    else
                    {
                        journalGump.SetInScreen();
                        journalGump.BringOnTop();
                    }

                    break;

                case Buttons.Chat:
                    Log.Warn( "Chat button pushed! Not implemented yet!");

                    break;

                case Buttons.Help:
                    GameActions.RequestHelp();

                    break;

                case Buttons.Achievements:
                    GameActions.RequestStore();
                    break;

                case Buttons.Debug:

                    DebugGump debugGump = UIManager.GetGump<DebugGump>();

                    if (debugGump == null)
                    {
                        debugGump = new DebugGump
                        {
                            X = ProfileManager.Current.DebugGumpPosition.X,
                            Y = ProfileManager.Current.DebugGumpPosition.Y
                        };

                        UIManager.Add(debugGump);
                    }
                    else
                    {
                        debugGump.IsVisible = !debugGump.IsVisible;
                        debugGump.SetInScreen();
                    }

                    //Engine.DropFpsMinMaxValues();

                    break;
                case Buttons.WorldMap:

                    WorldMapGump worldMap = UIManager.GetGump<WorldMapGump>();

                    if (worldMap == null || worldMap.IsDisposed)
                    {
                        worldMap = new WorldMapGump();
                        UIManager.Add(worldMap);
                    }
                    else
                    {
                        worldMap.BringOnTop();
                        worldMap.SetInScreen();
                    }
                    break;
            }
        }

        private enum Buttons
        {
            Map,
            Paperdoll,
            Inventory,
            Journal,
            Achievements,
            Chat,
            Help,
            Debug,
            WorldMap,
        }
    }
}