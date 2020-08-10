using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using MonoMod.Utils;
using Microsoft.Xna.Framework.Input;
using System.Threading;

namespace FezEngine.Mod {
    internal class NullModule : ModBase {

        public NullModule(ModMetadata metadata) {
            Metadata = metadata;
        }

    }
}
