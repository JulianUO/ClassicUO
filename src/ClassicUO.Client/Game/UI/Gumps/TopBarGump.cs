// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Network;
using ClassicUO.Resources;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class TopBarGump : Gump
    {
        private readonly ResizePic _background;
        private readonly CurrentPlaceCartelControl _cartel;
        private static string _savedPlaceName = "Britannia";

        private TopBarGump(World world) : base(world, 0, 0)
        {
            CanMove = false;
            AcceptMouseInput = true;
            CanCloseWithRightClick = false;
            LayerOrder = UILayer.Over;
            WantUpdateSize = false;

            X = 0;
            Y = 0;
            Width = Math.Max(Client.Game.ClientBounds.Width, 800);
            Height = 40;

            // Little / Minimized (Page 2)
            Add
            (
                new ResizePic(1755)
                {
                    Width = 40, Height = 39
                },
                2
            );

            Add
            (
                new Button(0, 1531, 1532)
                {
                    X = 8, Y = 9, ToPage = 1
                },
                2
            );

            // Full Bar (Page 1)
            Add
            (
                _background = new ResizePic(1755)
                {
                    Width = Width,
                    Height = 39
                },
                1
            );

            Add
            (
                new Button(0, 1531, 1532)
                {
                    ButtonAction = ButtonAction.SwitchPage, ToPage = 2, X = 8, Y = 9
                },
                1
            );

            Add
            (
                new Button((int)Buttons.Map, 1592, 1593, 0, "Radar", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 32, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.Paperdoll, 1588, 1589, 0, "Paperdoll", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 85, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.Inventory, 1588, 1589, 0, "Inventario", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 200, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.Journal, 1588, 1589, 0, "Journal", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 315, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.UOStore, 1588, 1589, 0, "Achievements", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 430, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.Chat, 1592, 1593, 0, "Chat", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 545, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.Help, 1592, 1593, 0, "Help", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 600, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.Debug, 1592, 1593, 0, "Debug", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 655, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.WorldMap, 1592, 1593, 0, "Mapa", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 710, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.Info, 1592, 1593, 0, "Info", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 765, Y = 7, FontCenter = true
                },
                1
            );

            Add
            (
                new Button((int)Buttons.NetStats, 1592, 1593, 0, "Lag", 1, true, 0x0386, 0)
                {
                    ButtonAction = ButtonAction.Activate, X = 820, Y = 7, FontCenter = true
                },
                1
            );

            // Right side: Current Place Cartel
            _cartel = new CurrentPlaceCartelControl(_savedPlaceName);
            Add(_cartel, 1);

            UpdateLayout();
        }

        public bool IsMinimized { get; private set; }

        public string CurrentPlace
        {
            get => _cartel.PlaceName;
            set
            {
                _savedPlaceName = value;
                _cartel.PlaceName = value;
                UpdateLayout();
            }
        }

        public static void SetCurrentPlace(World world, string placeName)
        {
            _savedPlaceName = placeName;
            TopBarGump gump = UIManager.GetGump<TopBarGump>();
            if (gump != null && !gump.IsDisposed)
            {
                gump.CurrentPlace = placeName;
            }
        }

        public static void Create(World world)
        {
            TopBarGump gump = UIManager.GetGump<TopBarGump>();

            if (gump == null)
            {
                UIManager.Add(gump = new TopBarGump(world));

                if (ProfileManager.CurrentProfile.TopbarGumpIsMinimized)
                {
                    gump.ChangePage(2);
                }
            }

            TopBarRadarGump radar = UIManager.GetGump<TopBarRadarGump>();
            if (radar == null)
            {
                UIManager.Add(radar = new TopBarRadarGump(world));
                if (ProfileManager.CurrentProfile.TopbarGumpIsMinimized)
                {
                    radar.IsVisible = false;
                }
            }
        }

        public void ToggleRadar()
        {
            TopBarRadarGump radar = UIManager.GetGump<TopBarRadarGump>();
            if (radar == null || radar.IsDisposed)
            {
                UIManager.Add(new TopBarRadarGump(World));
            }
            else
            {
                radar.IsVisible = !radar.IsVisible;
            }
        }

        private void UpdateLayout()
        {
            int screenWidth = Client.Game.ClientBounds.Width;
            if (screenWidth <= 0)
                screenWidth = 800;

            Width = screenWidth;
            Height = 40;
            X = 0;
            Y = 0;

            if (_background != null)
            {
                _background.Width = screenWidth;
            }

            if (_cartel != null)
            {
                int radarLeft = screenWidth - TopBarRadarGump.RADAR_WIDTH;
                _cartel.X = radarLeft - _cartel.Width - 6;
                _cartel.Y = 7;
            }
        }

        public override void Update()
        {
            base.Update();

            if (IsDisposed)
                return;

            int screenWidth = Client.Game.ClientBounds.Width;
            if (Width != screenWidth || X != 0 || Y != 0)
            {
                UpdateLayout();
            }
        }

        public override void OnPageChanged()
        {
            ProfileManager.CurrentProfile.TopbarGumpIsMinimized = IsMinimized = ActivePage == 2;
            WantUpdateSize = false;

            TopBarRadarGump radar = UIManager.GetGump<TopBarRadarGump>();
            if (radar != null && !radar.IsDisposed)
            {
                radar.IsVisible = !IsMinimized;
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            switch ((Buttons)buttonID)
            {
                case Buttons.Map:
                    ToggleRadar();
                    break;

                case Buttons.Paperdoll:
                    GameActions.OpenPaperdoll(World, World.Player);
                    break;

                case Buttons.Inventory:
                    GameActions.OpenBackpack(World);
                    break;

                case Buttons.Journal:
                    GameActions.OpenJournal(World);
                    break;

                case Buttons.Chat:
                    GameActions.OpenChat(World);
                    break;

                case Buttons.GlobalChat:
                    GameActions.Print(
                        World,
                        ResGumps.GlobalChatNotImplementedYet,
                        0x23,
                        MessageType.System
                    );
                    break;

                case Buttons.UOStore:
                    if (Client.Game.UO.Version >= ClientVersion.CV_706400)
                    {
                        NetClient.Socket.Send_OpenUOStore();
                    }
                    break;

                case Buttons.Help:
                    GameActions.RequestHelp();
                    break;

                case Buttons.Debug:
                    DebugGump debugGump = UIManager.GetGump<DebugGump>();
                    if (debugGump == null)
                    {
                        debugGump = new DebugGump(World, 100, 100);
                        UIManager.Add(debugGump);
                    }
                    else
                    {
                        debugGump.IsVisible = !debugGump.IsVisible;
                        debugGump.SetInScreen();
                    }
                    break;

                case Buttons.NetStats:
                    NetworkStatsGump netstatsgump = UIManager.GetGump<NetworkStatsGump>();
                    if (netstatsgump == null)
                    {
                        netstatsgump = new NetworkStatsGump(World, 100, 100);
                        UIManager.Add(netstatsgump);
                    }
                    else
                    {
                        netstatsgump.IsVisible = !netstatsgump.IsVisible;
                        netstatsgump.SetInScreen();
                    }
                    break;

                case Buttons.WorldMap:
                    GameActions.OpenWorldMap(World);
                    break;
            }
        }

        public void RequestRadarUpdate()
        {
            UIManager.GetGump<TopBarRadarGump>()?.ForceUpdateMap();
        }

        public override void Dispose()
        {
            UIManager.GetGump<TopBarRadarGump>()?.Dispose();
            base.Dispose();
        }

        private enum Buttons
        {
            Map,
            Paperdoll,
            Inventory,
            Journal,
            Chat,
            Help,
            WorldMap,
            Info,
            Debug,
            NetStats,
            UOStore,
            GlobalChat
        }
    }
}
