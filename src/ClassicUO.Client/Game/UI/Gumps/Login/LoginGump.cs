// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using SDL3;

namespace ClassicUO.Game.UI.Gumps.Login
{
    internal class LoginGump : Gump
    {
        private readonly ushort _buttonNormal;
        private readonly ushort _buttonOver;
        private readonly Checkbox _checkboxAutologin;
        private readonly Checkbox _checkboxSaveAccount;
        private readonly Button _nextArrow0;
        private readonly PasswordStbTextBox _passwordFake;
        private readonly StbTextBox _textboxAccount;

        private float _time;

        public LoginGump(World world, LoginScene scene) : base(world, 0, 0)
        {
            CanCloseWithRightClick = false;

            AcceptKeyboardInput = false;

            Add(new GumpPic(0, 0, 0x14E, 0));

            _buttonNormal = 0x603;
            _buttonOver = 0x603;
            Add
            (
                _nextArrow0 = new Button((int) Buttons.NextArrow, 0x603, 0x604)
                {
                    X = 602,
                    Y = 445,
                    ButtonAction = ButtonAction.Activate
                }
            );

            Add
            (
                new ResizePic(0x0BB8)
                {
                    X = 215,
                    Y = 282,
                    Width = 210,
                    Height = 30
                }
            );

            Add
            (
                new ResizePic(0x0BB8)
                {
                    X = 215,
                    Y = 333,
                    Width = 210,
                    Height = 30
                }
            );

            Add
            (
                new ResizePic(0x6DB)
                {
                    X = 115,
                    Y = 410,
                    Width = 413,
                    Height = 40
                }
            );

            Add
            (
                _checkboxAutologin = new Checkbox(0x05FF, 0x0601, "Autologin", 3, 0, false)
                {
                    X = 125,
                    Y = 419
                }
            );

            Add
            (
                _checkboxSaveAccount = new Checkbox(0x05FF, 0x0601, "Save Account", 3, 0, false)
                {
                    X = _checkboxAutologin.X + _checkboxAutologin.Width + 14,
                    Y = 419
                }
            );

            Add
            (
                new Label($"UO Version {Settings.GlobalSettings.ClientVersion}.", false, 0x034E, font: 9)
                {
                    X = 250,
                    Y = 453
                }
            );

            Add
            (
                new Label($"ClassicUO Version {CUOEnviroment.Version}", false, 0x034E, font: 9)
                {
                    X = 232,
                    Y = 465
                }
            );

            Add
            (
                _textboxAccount = new StbTextBox
                (
                    5,
                    16,
                    190,
                    false,
                    hue: 0x0455
                )
                {
                    X = 220,
                    Y = 282,
                    Width = 190,
                    Height = 25
                }
            );

            _textboxAccount.SetText(Settings.GlobalSettings.Username);

            Add
            (
                _passwordFake = new PasswordStbTextBox
                (
                    5,
                    16,
                    190,
                    false,
                    hue: 0x0455
                )
                {
                    X = 220,
                    Y = 335,
                    Width = 190,
                    Height = 25
                }
            );

            _passwordFake.RealText = Crypter.Decrypt(Settings.GlobalSettings.Password);

            _checkboxSaveAccount.IsChecked = Settings.GlobalSettings.SaveAccount;
            _checkboxAutologin.IsChecked = Settings.GlobalSettings.AutoLogin;

            Checkbox loginmusic_checkbox = new Checkbox
            (
                0x05FF,
                0x0601,
                "Music",
                3,
                0,
                false
            )
            {
                X = _checkboxSaveAccount.X + _checkboxSaveAccount.Width + 14,
                Y = _checkboxSaveAccount.Y,
                IsChecked = Settings.GlobalSettings.LoginMusic
            };

            Add(loginmusic_checkbox);

            HSliderBar login_music = new HSliderBar
            (
                loginmusic_checkbox.X + loginmusic_checkbox.Width + 10,
                loginmusic_checkbox.Y + 4,
                45,
                0,
                100,
                Settings.GlobalSettings.LoginMusicVolume,
                HSliderBarStyle.MetalWidgetRecessedBar
            );

            Add(login_music);
            login_music.IsVisible = Settings.GlobalSettings.LoginMusic;

            loginmusic_checkbox.ValueChanged += (sender, e) =>
            {
                Settings.GlobalSettings.LoginMusic = loginmusic_checkbox.IsChecked;
                Client.Game.Audio.UpdateCurrentMusicVolume(true);

                login_music.IsVisible = Settings.GlobalSettings.LoginMusic;
            };

            login_music.ValueChanged += (sender, e) =>
            {
                Settings.GlobalSettings.LoginMusicVolume = login_music.Value;
                Client.Game.Audio.UpdateCurrentMusicVolume(true);
            };

            if (!string.IsNullOrEmpty(_textboxAccount.Text))
            {
                _passwordFake.SetKeyboardFocus();
            }
            else
            {
                _textboxAccount.SetKeyboardFocus();
            }
        }

        public override void OnKeyboardReturn(int textID, string text)
        {
            SaveCheckboxStatus();
            LoginScene ls = Client.Game.GetScene<LoginScene>();

            if (ls.CurrentLoginStep == LoginSteps.Main)
            {
                ls.Connect(_textboxAccount.Text, _passwordFake.RealText);
            }
        }

        private void SaveCheckboxStatus()
        {
            Settings.GlobalSettings.SaveAccount = _checkboxSaveAccount.IsChecked;
            Settings.GlobalSettings.AutoLogin = _checkboxAutologin.IsChecked;
        }

        public override void Update()
        {
            if (IsDisposed)
            {
                return;
            }

            base.Update();

            if (_time < Time.Ticks)
            {
                _time = (float)Time.Ticks + 1000;

                _nextArrow0.ButtonGraphicNormal = _nextArrow0.ButtonGraphicNormal == _buttonNormal ? _buttonOver : _buttonNormal;
            }

            if (_passwordFake.HasKeyboardFocus)
            {
                if (_passwordFake.Hue != 0x0021)
                {
                    _passwordFake.Hue = 0x0021;
                }
            }
            else if (_passwordFake.Hue != 0)
            {
                _passwordFake.Hue = 0;
            }

            if (_textboxAccount.HasKeyboardFocus)
            {
                if (_textboxAccount.Hue != 0x0021)
                {
                    _textboxAccount.Hue = 0x0021;
                }
            }
            else if (_textboxAccount.Hue != 0)
            {
                _textboxAccount.Hue = 0;
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            switch ((Buttons) buttonID)
            {
                case Buttons.NextArrow:
                    SaveCheckboxStatus();

                    if (!_textboxAccount.IsDisposed)
                    {
                        Client.Game.GetScene<LoginScene>().Connect(_textboxAccount.Text, _passwordFake.RealText);
                    }

                    break;

                case Buttons.Quit:
                    Client.Game.Exit();

                    break;

                case Buttons.Credits:
                    UIManager.Add(new CreditsGump(World));

                    break;
            }
        }

        private class PasswordStbTextBox : StbTextBox
        {
            private new Point _caretScreenPosition;
            private new readonly RenderedText _rendererCaret;

            private new readonly RenderedText _rendererText;

            public PasswordStbTextBox
            (
                byte font,
                int max_char_count = -1,
                int maxWidth = 0,
                bool isunicode = true,
                FontStyle style = FontStyle.None,
                ushort hue = 0,
                TEXT_ALIGN_TYPE align = TEXT_ALIGN_TYPE.TS_LEFT
            ) : base
            (
                font,
                max_char_count,
                maxWidth,
                isunicode,
                style,
                hue,
                align
            )
            {
                _rendererText = RenderedText.Create
                (
                    string.Empty,
                    hue,
                    font,
                    isunicode,
                    style,
                    align,
                    maxWidth
                );

                _rendererCaret = RenderedText.Create
                (
                    "_",
                    hue,
                    font,
                    isunicode,
                    (style & FontStyle.BlackBorder) != 0 ? FontStyle.BlackBorder : FontStyle.None,
                    align
                );

                NoSelection = true;
            }

            internal string RealText
            {
                get => Text;
                set => SetText(value);
            }

            public new ushort Hue
            {
                get => _rendererText.Hue;
                set
                {
                    if (_rendererText.Hue != value)
                    {
                        _rendererText.Hue = value;
                        _rendererCaret.Hue = value;

                        _rendererText.CreateTexture();
                        _rendererCaret.CreateTexture();
                    }
                }
            }

            protected override void DrawCaret(UltimaBatcher2D batcher, int x, int y, float layerDepth)
            {
                if (HasKeyboardFocus)
                {
                    _rendererCaret.Draw(batcher, x + _caretScreenPosition.X, y + _caretScreenPosition.Y, layerDepth);
                }
            }

            protected override void OnMouseDown(int x, int y, MouseButtonType button)
            {
                base.OnMouseDown(x, y, button);

                if (button == MouseButtonType.Left)
                {
                    UpdateCaretScreenPosition();
                }
            }

            protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
            {
                base.OnKeyDown(key, mod);
                UpdateCaretScreenPosition();
            }

            public override void Dispose()
            {
                _rendererText?.Destroy();
                _rendererCaret?.Destroy();

                base.Dispose();
            }

            protected override void OnTextInput(string c)
            {
                base.OnTextInput(c);
            }

            protected override void OnTextChanged(string previousText)
            {
                if (Text.Length > 0)
                {
                    _rendererText.Text = new string('*', Text.Length);
                }
                else
                {
                    _rendererText.Text = string.Empty;
                }

                base.OnTextChanged(previousText);
                UpdateCaretScreenPosition();
            }

            internal override void OnFocusEnter()
            {
                base.OnFocusEnter();
                CaretIndex = Text?.Length ?? 0;
                UpdateCaretScreenPosition();
            }

            private new void UpdateCaretScreenPosition()
            {
                _caretScreenPosition = _rendererText.GetCaretPosition(Stb.CursorIndex);
            }

            public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
            {
                float layerDepth = layerDepthRef;
                renderLists.AddGumpNoAtlas(
                    batcher =>
                    {
                        if (batcher.ClipBegin(x, y, Width, Height))
                        {
                            DrawSelection(batcher, x, y, layerDepth);

                            _rendererText.Draw(batcher, x, y, layerDepth);

                            DrawCaret(batcher, x, y, layerDepth);
                            batcher.ClipEnd();
                        }
                        return true;
                    }
                );

                return true;
            }
        }


        private enum Buttons
        {
            NextArrow,
            Quit,
            Credits
        }
    }
}