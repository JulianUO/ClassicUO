using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;


namespace ClassicUO.Renderer
{
    public class RenderTargets
    {
        private RenderTarget2D _uiRenderTarget;
        private RenderTarget2D _lightRenderTarget;
        private RenderTarget2D _worldRenderTarget;

        private Rectangle _gameWindowOnScreen;
        private Rectangle _gameWindowAfterDPI;
        private Rectangle _gameWorldSceneOnScreen;
        private Rectangle _gameWorldSceneAfterDPI;

        private Func<Vector3> _lightsHue;
        private Func<BlendState> _lightsBlendState;

        private Texture2D _background;
        private SamplerState _defaultSamplerState;
        private XBREffect _xbrEffect;
        private CRTEffect _crtEffect;
        private int _worldFilterType;

        public RenderTarget2D UiRenderTarget { get => _uiRenderTarget; }
        public RenderTarget2D LightRenderTarget { get => _lightRenderTarget; }
        public RenderTarget2D WorldRenderTarget { get => _worldRenderTarget; }
        public float WorldRenderScale { get; private set; } = 1f;

        public void SetLightsConfiguration(Func<BlendState> lightsBlendState, Func<Vector3> lightsHue)
        {
            _lightsBlendState = lightsBlendState;
            _lightsHue = lightsHue;
        }

        public void EnsureSizes(GraphicsDevice graphicsDevice, Rectangle gameWindowOnScreen, Rectangle gameWorldSceneAfterDPI, float dpiScale, float worldRenderScale = 1f, int worldFilterType = 0)
        {
            _gameWindowOnScreen = gameWindowOnScreen;
            _gameWindowAfterDPI = ScaleRectangle(gameWindowOnScreen, dpiScale);
            _gameWorldSceneOnScreen = ScaleRectangle(gameWorldSceneAfterDPI, 1/dpiScale);
            _gameWorldSceneAfterDPI = gameWorldSceneAfterDPI;
            WorldRenderScale = worldRenderScale < 1f ? 1f : worldRenderScale;
            _worldFilterType = worldFilterType;

            EnsureSize(graphicsDevice, ref _uiRenderTarget, _gameWindowAfterDPI.Width, _gameWindowAfterDPI.Height);

            int worldWidth = Math.Max(1, (int)(_gameWorldSceneAfterDPI.Width * WorldRenderScale));
            int worldHeight = Math.Max(1, (int)(_gameWorldSceneAfterDPI.Height * WorldRenderScale));
            EnsureSize(graphicsDevice, ref _lightRenderTarget, worldWidth, worldHeight);
            EnsureSize(graphicsDevice, ref _worldRenderTarget, worldWidth, worldHeight);

            // CRT and Linear present softly; Point stays crisp; SSAA downsample uses Linear.
            if (worldFilterType == 1 || worldFilterType == 3 || WorldRenderScale > 1f)
            {
                _defaultSamplerState = SamplerState.LinearClamp;
            }
            else if (dpiScale == Math.Floor(dpiScale))
            {
                _defaultSamplerState = SamplerState.PointClamp;
            }
            else
            {
                _defaultSamplerState = SamplerState.LinearClamp;
            }
        }

        private static Rectangle ScaleRectangle(Rectangle gameWindowOnScreen, float dpiScale) => new(
                (int)(gameWindowOnScreen.X / dpiScale),
                (int)(gameWindowOnScreen.Y / dpiScale),
                (int)(gameWindowOnScreen.Width / dpiScale),
                (int)(gameWindowOnScreen.Height / dpiScale)
            );

        private static void EnsureSize(GraphicsDevice graphicsDevice, ref RenderTarget2D renderTarget, int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            if (renderTarget == null || renderTarget.IsDisposed || renderTarget.Width != width || renderTarget.Height != height)
            {
                renderTarget?.Dispose();

                PresentationParameters pp = graphicsDevice.PresentationParameters;

                renderTarget = new RenderTarget2D(
                    graphicsDevice,
                    width,
                    height,
                    false,
                    pp.BackBufferFormat,
                    pp.DepthStencilFormat,
                    pp.MultiSampleCount,
                    pp.RenderTargetUsage
                    );
            }
        }

        public void InitializeBackground(Texture2D background)
        {
            _background = background ?? throw new ArgumentNullException(nameof(background));
        }

        public void Draw(UltimaBatcher2D batcher)
        {
            Vector3 fullAlphaNoColor = Vector3.UnitZ;

            batcher.Begin();
            batcher.GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 0f, 0);

            var rect = new Rectangle(
                0,
                0,
                _gameWindowOnScreen.Width,
                _gameWindowOnScreen.Height
            );
            batcher.DrawTiled(
                _background,
                rect,
                _background.Bounds,
                new Vector3(0, 0, 0.1f),
                0f
            );
            batcher.End();

            Effect worldEffect = null;
            SamplerState worldSampler = _defaultSamplerState;

            if (WorldRenderTarget != null)
            {
                if (_worldFilterType == 2)
                {
                    _xbrEffect ??= new XBREffect(batcher.GraphicsDevice);
                    _xbrEffect.TextureSize.SetValue(new Vector2(WorldRenderTarget.Width, WorldRenderTarget.Height));
                    worldEffect = _xbrEffect;
                    worldSampler = SamplerState.PointClamp;
                }
                else if (_worldFilterType == 3)
                {
                    _crtEffect ??= new CRTEffect(batcher.GraphicsDevice);
                    _crtEffect.TextureSize.SetValue(new Vector2(WorldRenderTarget.Width, WorldRenderTarget.Height));
                    _crtEffect.ScanlineIntensity?.SetValue(0.65f);
                    _crtEffect.Softness?.SetValue(1f);
                    worldEffect = _crtEffect;
                    worldSampler = SamplerState.LinearClamp;
                }
            }

            batcher.Begin(worldEffect);
            batcher.SetSampler(worldSampler);

            if (WorldRenderTarget != null)
            {
                batcher.Draw(
                    WorldRenderTarget,
                    _gameWorldSceneOnScreen,
                    fullAlphaNoColor,
                    0f
                );
            }

            batcher.End();

            batcher.Begin();
            batcher.SetSampler(_defaultSamplerState);

            if (LightRenderTarget != null)
            {
                batcher.SetBlendState(_lightsBlendState?.Invoke());

                batcher.Draw(
                    LightRenderTarget,
                    _gameWorldSceneOnScreen,
                    _lightsHue?.Invoke() ?? Vector3.Up,
                    0f
                );

                batcher.SetBlendState(null);
            }

            batcher.Draw(
                UiRenderTarget,
                _gameWindowOnScreen,
                fullAlphaNoColor,
                0f
            );

            batcher.SetSampler(null);
            batcher.End();
        }
    }
}
