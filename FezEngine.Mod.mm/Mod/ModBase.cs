using Microsoft.Xna.Framework.Graphics;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using System.Configuration;
using Microsoft.Xna.Framework.Input;
using Common;

namespace FezEngine.Mod {
    public abstract class ModBase {

        public virtual ModMetadata Metadata { get; set; }

        public virtual ModSettings GetSettings() => null;
        public virtual ModSettings GetSaveData() => null;

        public virtual void LoadSettings() {
        }

        public virtual void SaveSettings() {
        }

        public virtual void LoadSaveData(int slot) {
        }

        public virtual void SaveSaveData(int slot) {
        }

        public virtual void DeleteSaveData(int slot) {
        }

        public virtual void Load() {
            LoadSettings();
        }

        public virtual void Initialize() {
        }

        public virtual void LoadContent(bool firstLoad) {
        }

        public virtual void Unload() {
        }

        public virtual bool ParseArg(string arg, Queue<string> args) {
            return false;
        }

    }

    public abstract class ModBase<TSettings> : ModBase where TSettings : ModSettings, new() {

        public TSettings Settings = new TSettings();
        public override ModSettings GetSettings() => Settings;

        public override void LoadSettings() {
            (Settings ??= new TSettings()).Load(Path.Combine(Util.LocalConfigFolder, $"Settings-{Metadata.ID}.yaml"));
        }

        public override void SaveSettings() {
            (Settings ??= new TSettings()).Save(Path.Combine(Util.LocalConfigFolder, $"Settings-{Metadata.ID}.yaml"));
        }

    }

    public abstract class ModBase<TSettings, TSaveData> : ModBase<TSettings> where TSettings : ModSettings, new() where TSaveData : ModSettings, new() {

        public TSaveData SaveData = new TSaveData();
        public override ModSettings GetSaveData() => SaveData;

        public override void LoadSaveData(int slot) {
            (SaveData ??= new TSaveData()).Load(Path.Combine(Util.LocalConfigFolder, $"SaveSlot{slot}-{Metadata.ID}.yaml"));
        }

        public override void SaveSaveData(int slot) {
            (SaveData ??= new TSaveData()).Save(Path.Combine(Util.LocalConfigFolder, $"SaveSlot{slot}-{Metadata.ID}.yaml"));
        }

        public override void DeleteSaveData(int slot) {
            string path = Path.Combine(Util.LocalConfigFolder, $"SaveSlot{slot}-{Metadata.ID}.yaml");
            if (File.Exists(path))
                File.Delete(path);
        }

    }

    public abstract class ModSettings {

        [YamlIgnore]
        public string FilePath = "";

        public virtual void Load(string path = "") {
            FilePath = path = Path.GetFullPath(path.Nullify() ?? FilePath);

            if (!File.Exists(path)) {
                Save(path);
                return;
            }

            Logger.Log("FEZMod.Settings", $"Loading {GetType().Name} from {path}");

            using (Stream stream = File.OpenRead(path))
            using (StreamReader reader = new StreamReader(stream))
                Load(reader);
        }

        public virtual void Load(TextReader reader) {
            YamlHelper.DeserializerUsing(this).Deserialize(reader, GetType());
        }

        public virtual void Save(string path = "") {
            path = Path.GetFullPath(path.Nullify() ?? FilePath);

            Logger.Log("FEZMod.Settings", $"Saving {GetType().Name} to {path}");

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (Stream stream = File.OpenWrite(path + ".tmp"))
            using (StreamWriter writer = new StreamWriter(stream))
                Save(writer);

            if (File.Exists(path))
                File.Delete(path);
            File.Move(path + ".tmp", path);
        }

        public virtual void Save(TextWriter writer) {
            YamlHelper.Serializer.Serialize(writer, this, GetType());
        }

    }
}
