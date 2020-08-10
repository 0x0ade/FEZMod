using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace FezEngine.Mod.Core {
    public class CoreEngineModule<TSettings> : ModBase<TSettings> where TSettings : CoreEngineModuleSettings, new() {

        public override void Load() {
        }

        public override void Unload() {
        }

    }

    public class CoreEngineModuleSettings : ModSettings {

        public bool CodeReload_WIP { get; set; } = false;

        // TODO: Once CodeReload is no longer WIP, remove this and rename ^ to non-WIP.
        [YamlIgnore]
        public bool CodeReload {
            get => CodeReload_WIP;
            set => CodeReload_WIP = value;
        }

    }
}
