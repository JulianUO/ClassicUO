// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Runtime.CompilerServices;
using ClassicUO.Game.Managers;
using ClassicUO.Assets;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    internal sealed partial class Land : GameObject
    {
        //private static readonly QueuedPool<Land> _pool = new QueuedPool<Land>
        //(
        //    Constants.PREDICTABLE_TILE_COUNT,
        //    l =>
        //    {
        //        l.IsDestroyed = false;
        //        l.AlphaHue = 255;
        //        l.NormalTop = l.NormalRight = l.NormalLeft = l.NormalBottom = Vector3.Zero;
        //        l.YOffsets.Top = l.YOffsets.Right = l.YOffsets.Left = l.YOffsets.Bottom = 0;
        //        l.MinZ = l.AverageZ = 0;
        //    }
        //);

        public ref LandTiles TileData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Client.Game.UO.FileManager.TileData.LandData[Graphic];
        }
        public sbyte AverageZ;
        public bool IsStretched;
        public sbyte MinZ;
        public Vector3 NormalTop, NormalRight, NormalLeft, NormalBottom;
        public ushort OriginalGraphic;
        public UltimaBatcher2D.YOffsets YOffsets;
        public int MeshBlend0 = -1, MeshBlend1 = -1, MeshBlend2 = -1, MeshBlend3 = -1;
        public Vector4 MeshBlendAlpha0, MeshBlendAlpha1, MeshBlendAlpha2, MeshBlendAlpha3;

        private Land(World world) : base(world) { }

        public static Land Create(World world, ushort graphic)
        {
            Land land = new Land(world); // _pool.GetOne();
            land.AlphaHue = 0xFF;
            land.Graphic = graphic;
            land.OriginalGraphic = graphic;
            land.IsStretched = land.TileData.TexID == 0 && land.TileData.IsWet;
            land.AllowedToDraw = CheckAllowedToDraw(graphic, ref land.TileData);
            land.UpdateGraphicBySeason();

            return land;
        }

        private static bool CheckAllowedToDraw(ushort graphic, ref LandTiles tileData)
        {
            if (graphic <= 2)
                return false;

            if (!string.IsNullOrEmpty(tileData.Name) && tileData.Name.StartsWith("nodraw", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        public override void Destroy()
        {
            if (IsDestroyed)
            {
                return;
            }

            base.Destroy();
            //_pool.ReturnOne(this);
        }

        public override void UpdateGraphicBySeason()
        {
            Graphic = SeasonManager.GetLandSeasonGraphic(World.Season, OriginalGraphic);
            AllowedToDraw = CheckAllowedToDraw(Graphic, ref TileData);
        }

        public int CalculateCurrentAverageZ(int direction)
        {
            int result = GetDirectionZ(((byte) (direction >> 1) + 1) & 3);

            if ((direction & 1) != 0)
            {
                return result;
            }

            return (result + GetDirectionZ(direction >> 1)) >> 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetDirectionZ(int direction)
        {
            switch (direction)
            {
                case 1: return YOffsets.Right >> 2;
                case 2: return YOffsets.Bottom >> 2;
                case 3: return YOffsets.Left >> 2;
                default: return Z;
            }
        }

        public void ApplyStretch(Map.Map map, int x, int y, sbyte z)
        {
            if (IsStretched || Client.Game.UO.FileManager.Texmaps.File.GetValidRefEntry(TileData.TexID).Length <= 0)
            {
                IsStretched = false;
                AverageZ = z;
                MinZ = z;

                return;
            }

            /*  _____ _____
             * | top | rig |
             * |_____|_____|
             * | lef | bot |
             * |_____|_____|
             */
            sbyte zTop = z;
            sbyte zRight = map.GetTileZ(x + 1, y);
            sbyte zLeft = map.GetTileZ(x, y + 1);
            sbyte zBottom = map.GetTileZ(x + 1, y + 1);

            YOffsets.Top = zTop * 4;
            YOffsets.Right = zRight * 4;
            YOffsets.Left = zLeft * 4;
            YOffsets.Bottom = zBottom * 4;

            if (Math.Abs(zTop - zBottom) <= Math.Abs(zLeft - zRight))
            {
                AverageZ = (sbyte) ((zTop + zBottom) >> 1);
            }
            else
            {
                AverageZ = (sbyte) ((zLeft + zRight) >> 1);
            }

            MinZ = Math.Min(zTop, Math.Min(zRight, Math.Min(zLeft, zBottom)));


            /*  _____ _____ _____ _____
             * |     | t10 | t20 |     |
             * |_____|_____|_____|_____|
             * | t01 |  z  | t21 | t31 |
             * |_____|_____|_____|_____|
             * | t02 | t12 | t22 | t32 |
             * |_____|_____|_____|_____|
             * |     | t13 | t23 |     |
             * |_____|_____|_____|_____|
             */
            sbyte t10 = map.GetTileZ(x, y - 1);
            sbyte t20 = map.GetTileZ(x + 1, y - 1);
            sbyte t01 = map.GetTileZ(x - 1, y);
            sbyte t21 = zRight;
            sbyte t31 = map.GetTileZ(x + 2, y);
            sbyte t02 = map.GetTileZ(x - 1, y + 1);
            sbyte t12 = zLeft;
            sbyte t22 = zBottom;
            sbyte t32 = map.GetTileZ(x + 2, y + 1);
            sbyte t13 = map.GetTileZ(x, y + 2);
            sbyte t23 = map.GetTileZ(x + 1, y + 2);


            IsStretched |= CalculateNormal(z, t10, t21, t12, t01, out NormalTop);
            IsStretched |= CalculateNormal(t21, t20, t31, t22, z, out NormalRight);
            IsStretched |= CalculateNormal(t22, t21, t32, t23, t12, out NormalBottom);
            IsStretched |= CalculateNormal(t12, z, t22, t13, t02, out NormalLeft);
        }

        private static bool CalculateNormal(sbyte tile, sbyte top, sbyte right, sbyte bottom, sbyte left, out Vector3 normal)
        {
            if (tile == top && tile == right && tile == bottom && tile == left)
            {
                normal.X = 0;
                normal.Y = 0;
                normal.Z = 1f;

                return false;
            }

            Vector3 u = new Vector3();
            Vector3 v = new Vector3();
            Vector3 ret = new Vector3();


            // ==========================
            u.X = -22;
            u.Y = -22;
            u.Z = (left - tile) * 4;

            v.X = -22;
            v.Y = 22;
            v.Z = (bottom - tile) * 4;

            Vector3.Cross(ref v, ref u, out ret);
            // ==========================


            // ==========================
            u.X = -22;
            u.Y = 22;
            u.Z = (bottom - tile) * 4;

            v.X = 22;
            v.Y = 22;
            v.Z = (right - tile) * 4;

            Vector3.Cross(ref v, ref u, out normal);
            Vector3.Add(ref ret, ref normal, out ret);
            // ==========================


            // ==========================
            u.X = 22;
            u.Y = 22;
            u.Z = (right - tile) * 4;

            v.X = 22;
            v.Y = -22;
            v.Z = (top - tile) * 4;

            Vector3.Cross(ref v, ref u, out normal);
            Vector3.Add(ref ret, ref normal, out ret);
            // ==========================


            // ==========================
            u.X = 22;
            u.Y = -22;
            u.Z = (top - tile) * 4;

            v.X = -22;
            v.Y = -22;
            v.Z = (left - tile) * 4;

            Vector3.Cross(ref v, ref u, out normal);
            Vector3.Add(ref ret, ref normal, out ret);
            // ==========================


            Vector3.Normalize(ref ret, out normal);

            return true;
        }

        public bool ShouldDrawTexmap(bool smoothTerrain)
        {
            if (TileData.TexID == 0)
                return false;

            // Wet tiles keep classic art diamonds; forcing texmaps on water causes seams and ghost quads.
            if (TileData.IsWet)
                return IsStretched;

            if (IsStretched)
                return true;

            return smoothTerrain && Client.Game.UO.Texmaps.GetTexmap(TileData.TexID).Texture != null;
        }

        public void ResetMeshBlends()
        {
            MeshBlend0 = MeshBlend1 = MeshBlend2 = MeshBlend3 = -1;
        }

        public void MarkLandVisible(MeshLayer layer, MeshLayer blendLayer, byte alphaHue)
        {
            if (MeshSpriteIndex >= 0)
                layer.SetVisible(MeshSpriteIndex, alphaHue);

            MarkBlend(blendLayer, MeshBlend0, alphaHue, MeshBlendAlpha0);
            MarkBlend(blendLayer, MeshBlend1, alphaHue, MeshBlendAlpha1);
            MarkBlend(blendLayer, MeshBlend2, alphaHue, MeshBlendAlpha2);
            MarkBlend(blendLayer, MeshBlend3, alphaHue, MeshBlendAlpha3);
        }

        private static void MarkBlend(MeshLayer layer, int index, byte alphaHue, Vector4 alpha)
        {
            if (layer == null || index < 0)
                return;

            layer.SetVisibleCorners(index, alphaHue, alpha.X, alpha.Y, alpha.Z, alpha.W);
        }

        public int CollectNeighborBlends(bool smoothTerrain, LandNeighborBlend[] buffer)
        {
            // Keep blends subtle and skip water / wet tiles entirely.
            if (!smoothTerrain || buffer.Length == 0 || TileData.TexID == 0 || TileData.IsWet)
                return 0;

            ref readonly var selfTex = ref Client.Game.UO.Texmaps.GetTexmap(TileData.TexID);
            if (selfTex.Texture == null)
                return 0;

            var map = World.Map;
            if (map == null)
                return 0;

            // Edge weight ~0.22 keeps transitions soft without the wide muddy wash of 0.5.
            const float e = 0.22f;
            int count = 0;
            TryAddBlend(map, X + 1, Y, TileData.TexID, 0f, e, 0f, e, buffer, ref count);
            TryAddBlend(map, X, Y + 1, TileData.TexID, 0f, 0f, e, e, buffer, ref count);
            TryAddBlend(map, X - 1, Y, TileData.TexID, e, 0f, e, 0f, buffer, ref count);
            TryAddBlend(map, X, Y - 1, TileData.TexID, e, e, 0f, 0f, buffer, ref count);
            return count;
        }

        private static void TryAddBlend(
            Map.Map map,
            int x,
            int y,
            ushort selfTexId,
            float a0,
            float a1,
            float a2,
            float a3,
            LandNeighborBlend[] buffer,
            ref int count)
        {
            if (count >= buffer.Length)
                return;

            Land neighbor = FindLand(map, x, y);
            if (neighbor == null
                || neighbor.TileData.TexID == 0
                || neighbor.TileData.TexID == selfTexId
                || neighbor.TileData.IsWet)
            {
                return;
            }

            ref readonly var tex = ref Client.Game.UO.Texmaps.GetTexmap(neighbor.TileData.TexID);
            if (tex.Texture == null)
                return;

            buffer[count++] = new LandNeighborBlend(tex.Texture, tex.UV, a0, a1, a2, a3);
        }

        private static Land FindLand(Map.Map map, int x, int y)
        {
            GameObject obj = map.GetTile(x, y, false);
            while (obj != null)
            {
                if (obj is Land land)
                    return land;

                obj = obj.TNext;
            }

            return null;
        }
    }

    internal readonly struct LandNeighborBlend
    {
        public readonly Texture2D Texture;
        public readonly Rectangle UV;
        public readonly float A0;
        public readonly float A1;
        public readonly float A2;
        public readonly float A3;

        public LandNeighborBlend(Texture2D texture, Rectangle uv, float a0, float a1, float a2, float a3)
        {
            Texture = texture;
            UV = uv;
            A0 = a0;
            A1 = a1;
            A2 = a2;
            A3 = a3;
        }
    }
}
