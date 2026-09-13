// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Assets;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ClassicUO.Game.GameObjects
{
    internal sealed partial class Land
    {
        private static readonly LandNeighborBlend[] DrawBlendScratch = new LandNeighborBlend[4];

        public override bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth)
        {
            if (!AllowedToDraw || IsDestroyed)
            {
                return false;
            }

            //Engine.DebugInfo.LandsRendered++;

            ushort hue = Hue;
            bool smoothTerrain = ProfileManager.CurrentProfile?.SmoothTerrain == true;
            bool useTexmap = ShouldDrawTexmap(smoothTerrain);

            if (ProfileManager.CurrentProfile.HighlightGameObjects && SelectedObject.Object == this)
            {
                hue = Constants.HIGHLIGHT_CURRENT_OBJECT_HUE;
            }
            else if (
                ProfileManager.CurrentProfile.NoColorObjectsOutOfRange
                && Distance > World.ClientViewRange
            )
            {
                hue = Constants.OUT_RANGE_COLOR;
            }
            else if (World.Player.IsDead && ProfileManager.CurrentProfile.EnableBlackWhiteEffect)
            {
                hue = Constants.DEAD_RANGE_COLOR;
            }

            Vector3 hueVec;
            if (hue != 0)
            {
                hueVec.X = hue - 1;
                hueVec.Y = useTexmap
                    ? ShaderHueTranslator.SHADER_LAND_HUED
                    : ShaderHueTranslator.SHADER_HUED;
            }
            else
            {
                hueVec.X = 0;
                hueVec.Y = useTexmap
                    ? ShaderHueTranslator.SHADER_LAND
                    : ShaderHueTranslator.SHADER_NONE;
            }
            hueVec.Z = 1f;

            if (useTexmap)
            {
                posY += Z << 2;

                ref readonly var texmapInfo = ref Client.Game.UO.Texmaps.GetTexmap(
                    Client.Game.UO.FileManager.TileData.LandData[Graphic].TexID
                );

                if (texmapInfo.Texture != null)
                {
                    batcher.DrawStretchedLand(
                        texmapInfo.Texture,
                        new Vector2(posX, posY),
                        texmapInfo.UV,
                        ref YOffsets,
                        ref NormalTop,
                        ref NormalRight,
                        ref NormalLeft,
                        ref NormalBottom,
                        hueVec,
                        depth + 0.5f
                    );

                    int blendCount = CollectNeighborBlends(smoothTerrain, DrawBlendScratch);
                    if (blendCount > 0)
                    {
                        batcher.SetStencil(DepthStencilState.DepthRead);
                        var pos = new Vector2(posX, posY);
                        for (int i = 0; i < blendCount; i++)
                        {
                            ref readonly var blend = ref DrawBlendScratch[i];
                            batcher.DrawStretchedLand(
                                blend.Texture,
                                pos,
                                blend.UV,
                                ref YOffsets,
                                ref NormalTop,
                                ref NormalRight,
                                ref NormalLeft,
                                ref NormalBottom,
                                hueVec,
                                depth + 0.5f,
                                blend.A0,
                                blend.A1,
                                blend.A2,
                                blend.A3
                            );
                        }
                        batcher.SetStencil(DepthStencilState.Default);
                    }

                    return true;
                }

                DrawStatic(
                    batcher,
                    Graphic,
                    posX,
                    posY,
                    hueVec,
                    depth,
                    ProfileManager.CurrentProfile.AnimatedWaterEffect && TileData.IsWet
                );
                return true;
            }

            if (IsStretched)
            {
                posY += Z << 2;
                DrawStatic(
                    batcher,
                    Graphic,
                    posX,
                    posY,
                    hueVec,
                    depth,
                    ProfileManager.CurrentProfile.AnimatedWaterEffect && TileData.IsWet
                );
            }
            else
            {
                ref readonly var artInfo = ref Client.Game.UO.Arts.GetLand(Graphic);

                if (artInfo.Texture != null)
                {
                    var dest = new Rectangle(posX, posY, artInfo.DrawWidth, artInfo.DrawHeight);

                    if (ProfileManager.CurrentProfile.AnimatedWaterEffect && TileData.IsWet)
                    {
                        batcher.Draw(
                            artInfo.Texture,
                            dest,
                            artInfo.UV,
                            hueVec,
                            depth + 0.5f
                        );

                        var sin = (float)Math.Sin(Time.Ticks / 1000f);
                        var cos = (float)Math.Cos(Time.Ticks / 1000f);
                        var scale = new Vector2(1.1f + sin * 0.1f, 1.1f + cos * 0.5f * 0.1f);
                        dest.Width = (int)(artInfo.DrawWidth * scale.X);
                        dest.Height = (int)(artInfo.DrawHeight * scale.Y);
                    }

                    batcher.Draw(
                        artInfo.Texture,
                        dest,
                        artInfo.UV,
                        hueVec,
                        depth + 0.5f
                    );
                }
            }

            return true;
        }

        public override bool CheckMouseSelection()
        {
            if (IsStretched || ShouldDrawTexmap(ProfileManager.CurrentProfile?.SmoothTerrain == true))
            {
                return SelectedObject.IsPointInStretchedLand(
                    ref YOffsets,
                    RealScreenPosition.X,
                    RealScreenPosition.Y + (Z << 2)
                );
            }

            return SelectedObject.IsPointInLand(RealScreenPosition.X, RealScreenPosition.Y);
        }
    }
}
