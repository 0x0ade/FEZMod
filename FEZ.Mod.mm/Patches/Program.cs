#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Common;
using FezGame.Mod;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FezGame {
    class patch_Program {

        private static Fez fez;

        private static extern void orig_Main(string[] args);
        private static void Main(string[] args) {
            FezMod.Prepare(args);

            orig_Main(args);
        }

        private static extern void orig_MainInternal();
        private static void MainInternal() {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            try {
                fez = new Fez();
                patch_Fez.Mod = FezMod.Instance;
                if (!fez.IsDisposed) {
                    FezMod.Instance.Boot(fez);
                    fez.Run();
                }
            } catch (Exception e) {
                Logger.Log("FEZMod", "Fatal error!");
                e.LogDetailed();
                throw;
            }
        }

    }
}
