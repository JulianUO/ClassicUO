// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Assets;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Map;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal class TopBarRadarGump : Gump
    {
        private struct ColorInfo
        {
            public ushort Color;
            public sbyte Z;
            public bool IsLand;
        }

        private Texture2D _radarTexture;
        private uint[] _pixelBuffer;
        private bool _drawDots;
        private long _blinkTimer;
        private ushort _lastX, _lastY;
        private int _lastMap = -1;
        private readonly RenderedText _coordsText;
        private readonly ResizePic _frame;

        public const int RADAR_WIDTH = 250;
        public const int EXPANDED_HEIGHT = 220;

        public TopBarRadarGump(World world) : base(world, 0, 0)
        {
            CanMove = false;
            AcceptMouseInput = true;
            CanCloseWithRightClick = false;
            LayerOrder = UILayer.Over;
            WantUpdateSize = false;

            Width = RADAR_WIDTH;
            Height = EXPANDED_HEIGHT;

            int screenWidth = Client.Game.ClientBounds.Width;
            if (screenWidth <= 0)
                screenWidth = 800;

            X = screenWidth - Width;
            Y = 0;

            _frame = new ResizePic(1755)
            {
                Width = Width,
                Height = Height
            };
            Add(_frame);

            _coordsText = RenderedText.Create(string.Empty, 0xFFFF, 1, false, FontStyle.BlackBorder);

            InitializeTexture();
        }

        private void InitializeTexture()
        {
            _radarTexture?.Dispose();
            _radarTexture = new Texture2D(Client.Game.GraphicsDevice, Width - 8, Height - 8, false, SurfaceFormat.Color);
            _pixelBuffer = new uint[(Width - 8) * (Height - 8)];
            ForceUpdateMap();
        }

        public void ForceUpdateMap()
        {
            _lastX = 0xFFFF;
            _lastY = 0xFFFF;
        }

        public override void Update()
        {
            base.Update();

            if (IsDisposed || !World.InGame || World.Player == null)
                return;

            int screenWidth = Client.Game.ClientBounds.Width;
            if (screenWidth <= 0)
                screenWidth = 800;

            int targetX = screenWidth - Width;
            if (X != targetX || Y != 0)
            {
                X = targetX;
                Y = 0;
            }

            if (_lastMap != World.MapIndex)
            {
                _lastMap = World.MapIndex;
                ForceUpdateMap();
            }

            if (_blinkTimer < Time.Ticks)
            {
                _drawDots = !_drawDots;
                _blinkTimer = (long)Time.Ticks + 500;
            }

            ushort curX = World.Player.X;
            ushort curY = World.Player.Y;
            sbyte curZ = World.Player.Z;

            if (_lastX != curX || _lastY != curY)
            {
                UpdateRadarPixels(curX, curY);
                _coordsText.Text = $"{curX}, {curY}, {curZ}";
                _lastX = curX;
                _lastY = curY;
            }
        }

        private unsafe void UpdateRadarPixels(ushort playerX, ushort playerY)
        {
            if (World.Map == null || _radarTexture == null || _pixelBuffer == null)
                return;

            int w = Width - 8;
            int h = Height - 8;
            if (w <= 0 || h <= 0)
                return;

            int gumpCenterX = w >> 1;
            int gumpCenterY = h >> 1;

            // In isometric projection:
            // gx = (tileX - playerX) - (tileY - playerY) + gumpCenterX
            // gy = (tileX - playerX) + (tileY - playerY) + gumpCenterY
            // To completely cover the rectangle [0, w) x [0, h),
            // the maximum offset in tile space (X and Y) needed is ((w + h) / 4) + safety buffer.
            int tileOffset = ((w + h) >> 2) + 16;

            int minBlockX = ((playerX - tileOffset) >> 3) - 1;
            int minBlockY = ((playerY - tileOffset) >> 3) - 1;
            int maxBlockX = ((playerX + tileOffset) >> 3) + 1;
            int maxBlockY = ((playerY + tileOffset) >> 3) + 1;

            if (minBlockX < 0) minBlockX = 0;
            if (minBlockY < 0) minBlockY = 0;

            int maxBlockIndex = World.Map.BlocksCount;
            int mapBlockWidth = Client.Game.UO.FileManager.Maps.MapBlocksSize[World.MapIndex, 0];
            int mapBlockHeight = Client.Game.UO.FileManager.Maps.MapBlocksSize[World.MapIndex, 1];

            if (maxBlockX >= mapBlockWidth) maxBlockX = mapBlockWidth - 1;
            if (maxBlockY >= mapBlockHeight) maxBlockY = mapBlockHeight - 1;

            Array.Clear(_pixelBuffer, 0, _pixelBuffer.Length);

            Span<Point> table = stackalloc Point[2];
            table[0].X = 0; table[0].Y = 0;
            table[1].X = 0; table[1].Y = 1;

            Span<ColorInfo> staticsZ = stackalloc ColorInfo[64];
            var emptyColorInfo = new ColorInfo { Z = sbyte.MinValue };

            for (int i = minBlockX; i <= maxBlockX; i++)
            {
                int blockIndexOffset = i * mapBlockHeight;

                for (int j = minBlockY; j <= maxBlockY; j++)
                {
                    int blockIndex = blockIndexOffset + j;
                    if (blockIndex >= maxBlockIndex)
                        continue;

                    if ((Configuration.ProfileManager.CurrentProfile?.EnableFogOfWar == true || UltimaLive.UltimaLiveActive)
                        && !Managers.FogOfWarManager.Instance.IsBlockDiscovered(World.MapIndex, blockIndex))
                    {
                        continue;
                    }

                    ref var indexMap = ref World.Map.GetIndex(i, j);
                    if (!indexMap.IsValid())
                        continue;

                    staticsZ.Fill(emptyColorInfo);
                    indexMap.StaticFile.Seek((long)indexMap.StaticAddress, System.IO.SeekOrigin.Begin);
                    indexMap.MapFile.Seek((long)indexMap.MapAddress, System.IO.SeekOrigin.Begin);
                    var cells = indexMap.MapFile.Read<MapBlock>().Cells;

                    Chunk block = World.Map.GetChunk(blockIndex);
                    int realBlockX = i << 3;
                    int realBlockY = j << 3;

                    for (int c = 0; c < indexMap.StaticCount; ++c)
                    {
                        var stblock = indexMap.StaticFile.Read<StaticsBlock>();
                        if (stblock.Color > 0 && stblock.Color != 0xFFFF && GameObject.CanBeDrawn(World, stblock.Color))
                        {
                            ref var st = ref staticsZ[stblock.Y * 8 + stblock.X];
                            if (st.Z < stblock.Z)
                            {
                                st.Color = stblock.Hue > 0 ? (ushort)(stblock.Hue + 0x4000) : stblock.Color;
                                st.Z = stblock.Z;
                                st.IsLand = stblock.Hue > 0;
                            }
                        }
                    }

                    for (int x = 0; x < 8; x++)
                    {
                        int px = realBlockX + x - playerX + gumpCenterX;

                        for (int y = 0; y < 8; y++)
                        {
                            ref readonly var cell = ref cells[(y << 3) + x];
                            int color = cell.TileID;
                            bool isLand = true;
                            int z = cell.Z;

                            ref var stZ = ref staticsZ[y * 8 + x];
                            if (stZ.Z >= z)
                            {
                                z = stZ.Z;
                                color = stZ.Color;
                                isLand = stZ.IsLand;
                            }

                            if (block != null)
                            {
                                GameObject obj = block.Tiles[x, y];
                                while (obj?.TNext != null) obj = obj.TNext;
                                for (; obj != null; obj = obj.TPrevious)
                                {
                                    if (obj is Multi)
                                    {
                                        color = obj.Hue == 0 ? obj.Graphic : (obj.Hue + 0x4000);
                                        if (obj.Hue == 0) isLand = false;
                                        break;
                                    }
                                }
                            }

                            if (!isLand) color += 0x4000;

                            if (isLand && color > 0x4000)
                            {
                                color = Client.Game.UO.FileManager.Hues.GetColor16(16384, (ushort)(color - 0x4000));
                            }
                            else
                            {
                                color = Client.Game.UO.FileManager.Hues.GetRadarColorData(color);
                            }

                            int py = realBlockY + y - playerY;
                            int gx = px - py;
                            int gy = px + py - (gumpCenterX - gumpCenterY);

                            for (int ti = 0; ti < 2; ti++)
                            {
                                int rx = gx + table[ti].X;
                                int ry = gy + table[ti].Y;

                                if (rx >= 0 && rx < w && ry >= 0 && ry < h)
                                {
                                    int blockIdx = ry * w + rx;
                                    _pixelBuffer[blockIdx] = HuesHelper.Color16To32((ushort)(0x8000 | color)) | 0xFF_00_00_00;
                                }
                            }
                        }
                    }
                }
            }

            fixed (uint* ptr = _pixelBuffer)
            {
                _radarTexture.SetDataPointerEXT(0, new Rectangle(0, 0, w, h), (IntPtr)ptr, _pixelBuffer.Length * sizeof(uint));
            }
        }

        public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
        {
            if (IsDisposed || _radarTexture == null || !IsVisible)
                return false;

            base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);

            float layerDepth = layerDepthRef;
            Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);

            renderLists.AddGumpNoAtlas(batcher =>
            {
                int innerX = x + 4;
                int innerY = y + 4;
                int innerW = Width - 8;
                int innerH = Height - 8;

                Texture2D darkBg = SolidColorTextureCache.GetTexture(new Color(15, 12, 10, 240));
                Texture2D innerBorder = SolidColorTextureCache.GetTexture(new Color(80, 60, 35, 255));

                batcher.Draw(darkBg, new Rectangle(innerX, innerY, innerW, innerH), hueVector, layerDepth);
                batcher.Draw(_radarTexture, new Vector2(innerX, innerY), new Rectangle(0, 0, innerW, innerH), hueVector, layerDepth);
                batcher.DrawRectangle(innerBorder, innerX, innerY, innerW, innerH, hueVector, layerDepth);

                if (_drawDots && World.Player != null)
                {
                    int cx = innerX + (innerW >> 1);
                    int cy = innerY + (innerH >> 1);

                    Texture2D dotTexture = SolidColorTextureCache.GetTexture(Color.White);

                    foreach (Mobile mob in World.Mobiles.Values)
                    {
                        if (mob == World.Player)
                            continue;

                        int dx = mob.X - World.Player.X;
                        int dy = mob.Y - World.Player.Y;
                        int gx = dx - dy;
                        int gy = dx + dy;

                        int mobScreenX = cx + gx;
                        int mobScreenY = cy + gy;

                        if (mobScreenX >= innerX + 2 && mobScreenX <= innerX + innerW - 3 &&
                            mobScreenY >= innerY + 2 && mobScreenY <= innerY + innerH - 3)
                        {
                            Vector3 mobHue = ShaderHueTranslator.GetHueVector(Notoriety.GetHue(mob.NotorietyFlag));
                            batcher.Draw(dotTexture, new Rectangle(mobScreenX - 1, mobScreenY - 1, 3, 3), mobHue, layerDepth);
                        }
                    }

                    // Draw Player Dot
                    batcher.Draw(dotTexture, new Rectangle(cx - 1, cy - 1, 3, 3), hueVector, layerDepth);
                }

                if (!string.IsNullOrEmpty(_coordsText.Text))
                {
                    _coordsText.Draw(batcher, innerX + 4, innerY + innerH - _coordsText.Height - 2, layerDepth);
                }

                return true;
            });

            return true;
        }

        protected override bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
        {
            return false;
        }

        public override void Dispose()
        {
            _radarTexture?.Dispose();
            _radarTexture = null;
            base.Dispose();
        }
    }
}
