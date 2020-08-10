using FezEngine.Mod;
using FezEngine.Mod.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FezGame.Mod.Core {
    public class CoreModule : CoreEngineModule<CoreModuleSettings> {

        public static CoreModule Instance;

        public CoreModule() {
            Instance = this;

            Metadata = new ModMetadata {
                ID = "FEZMod",
                VersionString = "0.0.0-fuckno"
            };
            Fez.Version = $"{Fez.Version} | FEZMod {Metadata.VersionString}";
        }

        public override void Load() {
        }

        public override void Unload() {
        }

        public override bool ParseArg(string arg, Queue<string> args) {
            if (arg == "--dump-all") {
                ContentDumper.DumpAllPacks();
                return true;
            }

            return false;
        }

    }

    public class CoreModuleSettings : CoreEngineModuleSettings {

    }
}
