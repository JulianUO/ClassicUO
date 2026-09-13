// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    public class CRTEffect : Effect
    {
        public CRTEffect(GraphicsDevice graphicsDevice) : base(graphicsDevice, Resources.GetCRTShader().ToArray())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            TextureSize = Parameters["textureSize"];
            ScanlineIntensity = Parameters["ScanlineIntensity"];
            Softness = Parameters["Softness"];
            CurrentTechnique = Techniques["CRTTechnique"];
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter TextureSize { get; }
        public EffectParameter ScanlineIntensity { get; }
        public EffectParameter Softness { get; }
    }
}
