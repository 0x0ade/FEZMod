using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace FezEngine.Mod {
    public sealed class ModMetadata {

        public ModMetadata[] Multimeta { get; set; }

        [YamlIgnore]
        public string PathArchive { get; set; }

        [YamlIgnore]
        public string PathDirectory { get; set; }

        public string ID { get; set; }

        [YamlIgnore]
        public Version Version { get; set; } = new Version(1, 0);
        private string _VersionString;
        [YamlMember(Alias = "Version")]
        public string VersionString {
            get {
                return _VersionString;
            }
            set {
                _VersionString = value;
                int versionSplitIndex = value.IndexOf('-');
                if (versionSplitIndex == -1)
                    Version = new Version(value);
                else
                    Version = new Version(value.Substring(0, versionSplitIndex));
            }
        }

        public string DLL { get; set; }

        public List<ModMetadata> Dependencies { get; set; } = new List<ModMetadata>();

        public string Hash { get; set; }

        public bool SupportsCodeReload { get; set; } = true;

        internal FileSystemWatcher DevWatcher;

        public override string ToString() {
            return ID + " " + Version;
        }

        public void PostParse() {
            if (!string.IsNullOrEmpty(DLL) && !string.IsNullOrEmpty(PathDirectory) && !File.Exists(DLL))
                DLL = Path.Combine(PathDirectory, DLL.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));

            // Add dependency to API 1.0 if missing.
            bool dependsOnAPI = false;
            foreach (ModMetadata dep in Dependencies) {
                if (dep.ID == "API")
                    dep.ID = FezModEngine.Instance.CoreModule.Metadata.ID;
                if (dep.ID == FezModEngine.Instance.CoreModule.Metadata.ID) {
                    dependsOnAPI = true;
                    break;
                }
            }
            if (!dependsOnAPI)
                Dependencies.Insert(0, FezModEngine.Instance.CoreModule.Metadata);
        }

    }
}
