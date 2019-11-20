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
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.IO;
using ClassicUO.Renderer;
using ClassicUO.Utility;

namespace ClassicUO.Game.UI.Gumps.Login
{
    internal class LoginGump : Gump
    {
        private readonly Checkbox _checkboxAutologin;
        private readonly Checkbox _checkboxSaveAccount;
        private readonly Button _nextArrow0;
        private readonly TextBox _textboxAccount;
        private readonly TextBox _textboxPassword;

        public LoginGump() : base(0, 0)
        {
            CanCloseWithRightClick = false;

            AcceptKeyboardInput = false;

            Add(new GumpPic(0, 0, 0x14E, 0));

            // Arrow Button
            Add(_nextArrow0 = new Button((int) Buttons.NextArrow, 0x0603, 0x604)
            {
                X = 602,
                Y = 445,
                ButtonAction = ButtonAction.Activate
            });

            // Account Text Input Background
            Add(new ResizePic(0x0BB8)
            {
                X = 215,
                Y = 282,
                Width = 210,
                Height = 30
            });

            // Password Text Input Background
            Add(new ResizePic(0x0BB8)
            {
                X = 215,
                Y = 333,
                Width = 210,
                Height = 30
            });


            Add(new ResizePic(0x6DB)
            {
                X = 190,
                Y = 410,
                Width = 260,
                Height = 40
            });
            

            _checkboxSaveAccount.IsChecked = Settings.GlobalSettings.SaveAccount;
            _checkboxAutologin.IsChecked = Settings.GlobalSettings.AutoLogin;
			Add(_checkboxAutologin = new Checkbox(0x05FF, 0x0601, "Autologin", 3, 0, false)
            {
                X = 200,
                Y = 419
            });

            Add(_checkboxSaveAccount = new Checkbox(0x05FF, 0x0601, "Save Account", 3, 0, false)
            {
                X = _checkboxAutologin.X + _checkboxAutologin.Width + 14,
                Y = 419
            });

            _checkboxSaveAccount.IsChecked = Engine.GlobalSettings.SaveAccount;
            _checkboxAutologin.IsChecked = Engine.GlobalSettings.AutoLogin;
            
            Add(new Label($"UO Version {Engine.GlobalSettings.ClientVersion}.", false, 0x034E, font: 9)
            {
                X = 250,
                Y = 453
            });

            Add(new Label($"ClassicUO Version {CUOEnviroment.Version}", false, 0x034E, font: 9)
            {
                X = 232,
                Y = 465
            });

            /*
            int htmlX = 130;
            int htmlY = 442;

            Add(new HtmlControl(htmlX, htmlY, 300, 100,
                                false, false,
                                false, 
                                text: "<body link=\"#ad9413\" vlink=\"#00FF00\" ><a href=\"https://www.paypal.me/muskara\">> Instant donation",
                                0x32, true, isunicode: true, style: FontStyle.BlackBorder));
            Add(new HtmlControl(htmlX, htmlY + 20, 300, 100,
                                false, false,
                                false,
                                text: "<body link=\"#ad9413\" vlink=\"#00FF00\" ><a href=\"https://www.patreon.com/user?u=21694183\">> Become a Patreon!",
                                0x32, true, isunicode: true, style: FontStyle.BlackBorder));
            
            */
            // Text Inputs
            Add(_textboxAccount = new TextBox(5, 16, 190, 190, false)
            {
                X = 220,
                Y = 282,
                Width = 190,
                Height = 25,
                Hue = 0x0455,
                SafeCharactersOnly = true
            });

            Add(_textboxPassword = new TextBox(5, 16, 190, 190, false)
            {
                X = 220,
                Y = 335,
                Width = 190,
                Height = 25,
                Hue = 0x0455,
                IsPassword = true,
                SafeCharactersOnly = true
            });
            _textboxAccount.SetText(Settings.GlobalSettings.Username);
            _textboxPassword.SetText(Crypter.Decrypt(Settings.GlobalSettings.Password));
        }

        public override void OnKeyboardReturn(int textID, string text)
        {
            SaveCheckboxStatus();
            LoginScene ls = CUOEnviroment.Client.GetScene<LoginScene>();

            if (ls.CurrentLoginStep == LoginScene.LoginStep.Main)
                ls.Connect(_textboxAccount.Text, _textboxPassword.Text);
        }

        private void SaveCheckboxStatus()
        {
            Settings.GlobalSettings.SaveAccount = _checkboxSaveAccount.IsChecked;
            Settings.GlobalSettings.AutoLogin = _checkboxAutologin.IsChecked;
        }

        public override void Update(double totalMS, double frameMS)
        {
            if (IsDisposed)
                return;

            base.Update(totalMS, frameMS);
            
            if (_textboxPassword.HasKeyboardFocus)
            {
                if (_textboxPassword.Hue != 0x0021)
                    _textboxPassword.Hue = 0x0021;
            }
            else if (_textboxPassword.Hue != 0)
                _textboxPassword.Hue = 0;

            if (_textboxAccount.HasKeyboardFocus)
            {
                if (_textboxAccount.Hue != 0x0021)
                    _textboxAccount.Hue = 0x0021;
            }
            else if (_textboxAccount.Hue != 0)
                _textboxAccount.Hue = 0;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch ((Buttons) buttonID)
            {
                case Buttons.NextArrow:
                    SaveCheckboxStatus();
                    if (!_textboxAccount.IsDisposed)
                        CUOEnviroment.Client.GetScene<LoginScene>().Connect(_textboxAccount.Text, _textboxPassword.Text);

                    break;

                case Buttons.Quit:
                    CUOEnviroment.Client.Exit();

                    break;
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            if (!string.IsNullOrEmpty(_textboxAccount.Text))
                _textboxPassword.SetKeyboardFocus();
        }

        private enum Buttons
        {
            NextArrow,
            Quit
        }
    }
}