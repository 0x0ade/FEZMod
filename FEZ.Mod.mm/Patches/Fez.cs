#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoMod;
using Microsoft.Xna.Framework;
using FezGame.Mod;
using System.Reflection;
using Common;
using FezEngine.Tools;
using FezGame.Components;
using FezEngine.Components;
using FezEngine.Mod;

namespace FezGame {
    class patch_Fez : Fez {

        public static FezMod Mod { get; internal set; }

        private PropertyInfo p_GameTime_ElapsedGameTime;
        private PropertyInfo p_GameTime_TotalGameTime;

        private GameTime _MulGameTime(ref GameTime gameTime) {
            double scale = Mod.GameTimeScale;
            if (scale == 1d) {
                return gameTime;
            }

            if (p_GameTime_ElapsedGameTime == null)
                p_GameTime_ElapsedGameTime = gameTime.GetType().GetProperty("ElapsedGameTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (p_GameTime_TotalGameTime == null)
                p_GameTime_TotalGameTime = gameTime.GetType().GetProperty("TotalGameTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            TimeSpan egt = gameTime.ElapsedGameTime;
            TimeSpan tgt = gameTime.TotalGameTime;
            tgt -= egt;
            egt = TimeSpan.FromTicks((long) (egt.Ticks * scale));
            tgt += egt;

            ReflectionHelper.SetValue(p_GameTime_ElapsedGameTime, gameTime, egt);
            ReflectionHelper.SetValue(p_GameTime_TotalGameTime, gameTime, tgt);

            return gameTime;
        }

        public extern void orig_Update(GameTime gameTime);
        protected override void Update(GameTime gameTime) {
            _MulGameTime(ref gameTime);
            Mod.GameTimeUpdate = gameTime;
            orig_Update(gameTime);
        }

        public extern void orig_Draw(GameTime gameTime);
        protected override void Draw(GameTime gameTime) {
            _MulGameTime(ref gameTime);
            Mod.GameTimeDraw = gameTime;
            orig_Draw(gameTime);
        }

        public extern void orig_Initialize();
        protected override void Initialize() {
            Mod.LoadComponentReplacements();
            orig_Initialize();
            Mod.Initialize();
        }

        [MonoModLinkTo("Microsoft.Xna.Framework.Game", "System.Void LoadContent()")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void base_LoadContent();
        protected override void LoadContent() {
            base_LoadContent();
            foreach (ModBase mod in Mod.Modules)
                mod.LoadContent(!ModContent.GameLoadedContent);
            ModContent.GameLoadedContent = true;
        }

        public static extern void orig_LoadComponents(Fez game);
        public static void LoadComponents(Fez game) {
            if (ServiceHelper.FirstLoadDone)
                return;
            orig_LoadComponents(game);
            Mod.LoadComponents();
            ServiceHelper.FirstLoadDone = true;
        }

    }
}
