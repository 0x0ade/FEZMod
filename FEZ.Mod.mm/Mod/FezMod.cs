using Common;
using FezEngine.Mod;
using FezEngine.Mod.Core;
using FezEngine.Tools;
using FezGame.Mod.Core;
using FezGame.Mod.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FezGame.Mod {
    public sealed class FezMod : FezModEngine {

        public static new FezMod Instance { get; internal set; }

        public Fez Fez { get; private set; }

        // TODO: Move out!
        public double GameTimeScale = 1f;
        public GameTime GameTimeUpdate { get; internal set; }
        public GameTime GameTimeDraw { get; internal set; }

        internal static void Prepare(string[] args) {
            Instance = new FezMod {
                Args = new ReadOnlyCollection<string>(args)
            };
        }

        internal void Boot(Fez game) {
            Logger.Log("FezMod", LogSeverity.Information, "Booting FEZMod");
            Logger.Log("FezMod", LogSeverity.Information, $"Version: {Fez.Version}");

            Boot(game, new CoreModule());
        }

        public override void LoadComponentReplacements() {
            base.LoadComponentReplacements();
            ServiceHelperHooks.ReplacementServices["FezEngine.Services.MouseStateManager"] = new ModMouseStateManager();
            ServiceHelperHooks.ReplacementServices["FezEngine.Services.KeyboardStateManager"] = new ModKeyboardStateManager();
        }

        public override void Initialize() {
            base.Initialize();
        }

        public override void LoadComponents() {
            base.LoadComponents();
            // ServiceHelper.AddComponent(new ModGUIHost(game));
        }

    }
}
