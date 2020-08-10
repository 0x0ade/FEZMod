using FezEngine.Mod;
using FezEngine.Mod.Core;
using FezEngine.Tools;
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
    internal static class ContentDumper {

        private static string ContentGuessType(string file, out Type type, out string format) {
            type = typeof(object);
            format = Path.GetExtension(file) ?? "";
            if (format.Length >= 1)
                format = format.Substring(1);

            if (file.EndsWith(".png")) {
                type = typeof(Texture2D);
                return file.Substring(0, file.Length - 4);
            }

            return null;
        }

        internal static void LoadComponentReplacements(Fez game) {
            ServiceHelperHooks.ReplacementServices["FezEngine.Services.MouseStateManager"] = new ModMouseStateManager();
            ServiceHelperHooks.ReplacementServices["FezEngine.Services.KeyboardStateManager"] = new ModKeyboardStateManager();
        }

        internal static void LoadComponents(Fez game) {
            // ServiceHelper.AddComponent(new ModGUIHost(game));
        }

        internal static void DumpAllPacks() {
            string pathPakDir = ModContent.PathContentOrig;
            if (!Directory.Exists(pathPakDir))
                return;
            foreach (string pathPak in Directory.GetFiles(pathPakDir)) {
                if (!pathPak.EndsWith(".pak"))
                    continue;
                DumpPack(pathPak);
            }
        }

        internal static void DumpPack(string pathPak) {
            if (!File.Exists(pathPak))
                return;

            string pak = Path.GetFileNameWithoutExtension(pathPak);
            string pathOutRoot = ModContent.PathDUMP;

            using (FileStream packStream = File.OpenRead(pathPak))
            using (BinaryReader packReader = new BinaryReader(packStream)) {
                int count = packReader.ReadInt32();
                for (int i = 0; i < count; i++) {
                    string path = packReader.ReadString();
                    int length = packReader.ReadInt32();

                    if (pak == "Music") {
                        // The music .pak contains basic .ogg files.
                        path += ".ogg";
                            
                    } else if (path.StartsWith("effects")) {
                        // The FEZ 1.12 .paks contains the raw fxb files, which should be dumped with their original fxb extension.
                        path += ".fxb";

                    } else {
                        path += ".xnb";
                    }

                    string pathOut = Path.Combine(pathOutRoot, path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
                    string pathOutDir = Path.GetDirectoryName(pathOut);

                    if (!Directory.Exists(pathOutDir))
                        Directory.CreateDirectory(pathOutDir);

                    Console.WriteLine($"Dumping {pathOut}");
                    if (File.Exists(pathOut))
                        File.Delete(pathOut);
                    using (FileStream dumpStream = File.OpenWrite(pathOut))
                        dumpStream.Write(packReader.ReadBytes(length), 0, length);
                }
            }
        }

    }
}
