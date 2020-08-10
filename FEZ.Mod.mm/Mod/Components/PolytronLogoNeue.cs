using System;
using System.Linq;
using System.Runtime.CompilerServices;
using FezEngine.Effects;
using FezEngine.Services;
using FezEngine.Structure;
using FezEngine.Structure.Geometry;
using FezEngine.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;

namespace FezGame.Components {
    [MonoModLinkFrom("FezGame.Components.PolytronLogo")]
    internal class PolytronLogoNeue : DrawableGameComponent {

        [ServiceDependency] public ITargetRenderingManager TargetRenderer { get; set; }
        [ServiceDependency] public IContentManagerProvider CMProvider { get; set; }

        private static readonly Color[] StripColors = new Color[] {
            new Color(0, 174, 250),
            new Color(255, 242, 0),
            new Color(255, 111, 1)
        };

        private Mesh LogoMesh;
        private Texture2D PolytronText;
        private SpriteBatch spriteBatch;

        private SoundEffect sPolytron;
        private SoundEmitter iPolytron;

        private float SinceStarted;

        public PolytronLogoNeue(Game game) : base(game) {
            Visible = false;
            Enabled = false;
        }

        public float Opacity { get; set; }

        public override void Initialize() {
            base.Initialize();

            LogoMesh = new Mesh {
                AlwaysOnTop = true
            };

            for (int i = 0; i < StripColors.Length; i++) {
                FezVertexPositionColor[] vertices = new FezVertexPositionColor[202];
                for (int j = 0; j < vertices.Length; j++) {
                    vertices[j] = new FezVertexPositionColor(Vector3.Zero, StripColors[i]);
                }
                LogoMesh.AddGroup().Geometry = new IndexedUserPrimitives<FezVertexPositionColor>(vertices, Enumerable.Range(0, vertices.Length).ToArray(), PrimitiveType.TriangleStrip);
            }

            float viewScale = GraphicsDevice.GetViewScale();
            float xScale = GraphicsDevice.Viewport.Width / (1280f * viewScale);
            float yScale = GraphicsDevice.Viewport.Height / (720f * viewScale);
            int width = GraphicsDevice.Viewport.Width;
            int height = GraphicsDevice.Viewport.Height;

            LogoMesh.Position = new Vector3(-0.1975f / xScale, -0.25f / yScale, 0f);
            LogoMesh.Scale = new Vector3(new Vector2(500f) * viewScale / new Vector2(width, height), 1f);
            sPolytron = CMProvider.Get(CM.Intro).Load<SoundEffect>("Sounds/Intro/PolytronJingle");

            DrawActionScheduler.Schedule(delegate {
                PolytronText = CMProvider.Get(CM.Intro).Load<Texture2D>("Other Textures/splash/polytron_neue" + (viewScale >= 1.5f ? "_1440" : ""));
                spriteBatch = new SpriteBatch(GraphicsDevice);
                LogoMesh.Effect = new DefaultEffect.VertexColored {
                    ForcedProjectionMatrix = Matrix.CreateOrthographic(320f / 224f, 320f / 224f, 0.1f, 100f),
                    ForcedViewMatrix = Matrix.CreateLookAt(Vector3.UnitZ, -Vector3.UnitZ, Vector3.Up)
                };
            });
        }

        protected override void Dispose(bool disposing) {
            LogoMesh.Dispose();
            spriteBatch.Dispose();
        }

        private void UpdateStripe(int stripe, float step) {
            // Brought back from FEZ.

            FezVertexPositionColor[] vertices = (LogoMesh.Groups[stripe].Geometry as IndexedUserPrimitives<FezVertexPositionColor>).Vertices;
            Vector3 posI = Vector3.Zero; // Inner
            Vector3 posO = Vector3.Zero; // Outter

            float thickness = 0.364f / StripColors.Length;
            const float halfPI = 1.5707963267948966f;

            for (int i = 0; i <= 100; i++) {
                int iA = i * 2;
                int iB = i * 2 + 1;

                float f;
                if (i < 20) {
                    // Bottom left |
                    f = i / 20f * FezMath.Saturate(step / 0.2f);
                    posI = new Vector3((stripe + 1) * thickness, f * 0.5f, 0f);
                    posO = new Vector3(stripe * thickness, f * 0.5f, 0f);

                } else if (i > 80 && step > 0.8f) {
                    // Bottom -
                    f = (i - 80f) / 20f * FezMath.Saturate((step - 0.8f) / 0.2f / 0.272f);
                    posI = new Vector3(0.5f - f * 0.136f, (stripe + 1) * thickness, 0f);
                    posO = new Vector3(0.5f - f * 0.136f, stripe * thickness, 0f);

                } else if (i >= 20 && i <= 80 && step > 0.2f) {
                    // Arc
                    f = (i - 20f) / 60f * FezMath.Saturate((step - 0.2f) / 0.6f) * halfPI * 3f - halfPI;
                    posI = new Vector3((float) Math.Sin(f) * (0.5f - (stripe + 1) * thickness) + 0.5f, (float) Math.Cos(f) * (0.5f - (stripe + 1) * thickness) + 0.5f, 0f);
                    posO = new Vector3((float) Math.Sin(f) * (0.5f - stripe * thickness) + 0.5f, (float) Math.Cos(f) * (0.5f - stripe * thickness) + 0.5f, 0f);

                }

                vertices[iA].Position = posI;
                vertices[iB].Position = posO;
            }
        }

        public void End() {
            if (!iPolytron.Dead)
                iPolytron.FadeOutAndDie(0.1f);
            iPolytron = null;
        }

        public override void Update(GameTime gameTime) {
            if (SinceStarted == 0f && gameTime.ElapsedGameTime.Ticks != 0L && sPolytron != null) {
                iPolytron = sPolytron.Emit();
                sPolytron = null;
            }

            SinceStarted += (float) gameTime.ElapsedGameTime.TotalSeconds;

            for (int i = StripColors.Length - 1; i > -1; --i) {
                // float ease = FezMath.Saturate(SinceStarted / 1.5f);
                // UpdateStripe(i, Easing.EaseOut(Easing.EaseIn(ease, EasingType.Quadratic + (StripColors.Length - 1) - i), EasingType.Quartic) * 0.86f);

                float ease = FezMath.Saturate((SinceStarted - 0.125f * ((StripColors.Length - 1) - i)) / 1.5f);
                UpdateStripe(i, Easing.EaseOut(Easing.EaseIn(ease, EasingType.Quadratic), EasingType.Quartic) * 0.86f);
            }
        }

        public override void Draw(GameTime gameTime) {
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

            Vector2 center = (new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height) / 2f).Round();
            float viewScale = GraphicsDevice.GetViewScale();
            float ease = Easing.EaseOut(FezMath.Saturate((SinceStarted - 1.5f) / 0.25f), EasingType.Quadratic);

            LogoMesh.Material.Opacity = Opacity;
            LogoMesh.Draw();

            spriteBatch.Begin();
            spriteBatch.Draw(PolytronText, center + new Vector2((-PolytronText.Width) / 2f, (128f + 120f / StripColors.Length) * viewScale).Round(), new Color(1f, 1f, 1f, Opacity * ease));
            spriteBatch.End();
        }

    }
}
