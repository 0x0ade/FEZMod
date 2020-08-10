#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using FezEngine.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FezGame.Components {
    class patch_GlitchyRespawner {

        public extern void orig_Initialize();
        public void Initialize() {
            // orig_Initialize creates Effects, which fails when loading a level with it.
            DrawActionScheduler.Schedule(orig_Initialize);
        }

    }
}
