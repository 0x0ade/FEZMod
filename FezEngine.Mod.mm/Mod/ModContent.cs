using Common;
using FezEngine.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectFactories;

namespace FezEngine.Mod {
    public sealed class AssetTypeDirectory { private AssetTypeDirectory() { } }
    public sealed class AssetTypeAssembly { private AssetTypeAssembly() { } }
    public sealed class AssetTypeYaml { private AssetTypeYaml() { } }
    public sealed class AssetTypeXml { private AssetTypeXml() { } }
    public sealed class AssetTypeText { private AssetTypeText() { } }
    public sealed class AssetTypeMetadataYaml { private AssetTypeMetadataYaml() { } }

    public delegate string AssetTypeGuesser(string file, out Type type, out string format);

    public static class ModContent {

        public static bool GameLoadedContent = false;

        public static string PathContentOrig;
        public static string PathDUMP;

        public static readonly List<ModAssetSource> Mods = new List<ModAssetSource>();

        public static readonly Dictionary<string, ModAsset> Map = new Dictionary<string, ModAsset>();

        public static readonly HashSet<Type> NonConflictTypes = new HashSet<Type>() {
                typeof(AssetTypeDirectory),
                typeof(AssetTypeMetadataYaml)
            };

        internal static readonly List<string> LoadedAssetPaths = new List<string>();
        internal static readonly List<string> LoadedAssetFullPaths = new List<string>();
        internal static readonly List<WeakReference> LoadedAssets = new List<WeakReference>();

        internal static readonly char[] DirSplit = { '/' };

        internal static void Initialize() {
            Directory.CreateDirectory(PathContentOrig = Path.Combine(FezModEngine.Instance.PathGame, FezModEngine.Instance.Game.Content.RootDirectory));
            Directory.CreateDirectory(PathDUMP = Path.Combine(FezModEngine.Instance.PathGame, "ModDUMP"));

            Crawl(new AssemblyModAssetSource(FezModEngine.Instance.GetType().Assembly) {
                ID = "FEZMod",
                // Mod = CoreModule.Instance.Metadata // Can't actually set Mod this early.
            });
        }

        public static bool TryGet(string path, out ModAsset metadata) {
            path = path.ToLowerInvariant().Replace('\\', '/');

            lock (Map)
                if (Map.TryGetValue(path, out metadata) && metadata != null)
                    return true;

            metadata = null;
            return false;
        }

        public static ModAsset Get(string path) {
            if (TryGet(path, out ModAsset metadata))
                return metadata;
            return null;
        }

        public static bool TryGet<T>(string path, out ModAsset metadata) {
            path = path.ToLowerInvariant().Replace('\\', '/');

            List<string> parts = new List<string>(path.Split(DirSplit, StringSplitOptions.RemoveEmptyEntries));
            for (int i = 0; i < parts.Count; i++) {
                string part = parts[i];

                if (part == "..") {
                    parts.RemoveAt(i);
                    parts.RemoveAt(i - 1);
                    i -= 2;
                    continue;
                }

                if (part == ".") {
                    parts.RemoveAt(i);
                    i -= 1;
                    continue;
                }
            }

            path = string.Join("/", parts);

            lock (Map)
                if (Map.TryGetValue(path, out metadata) && metadata != null && metadata.Type == typeof(T))
                    return true;

            metadata = null;
            return false;
        }

        public static ModAsset Get<T>(string path) {
            if (TryGet<T>(path, out ModAsset metadata))
                return metadata;
            return null;
        }

        public static void Add(string path, ModAsset metadata, bool overwrite = true) {
            path = path.ToLowerInvariant().Replace('\\', '/');

            if (metadata != null) {
                if (metadata.Type == null)
                    path = GuessType(path, out metadata.Type, out metadata.Format);
                metadata.PathVirtual = path;
            }
            string prefix = metadata?.Source?.ID;

            if (metadata != null && metadata.Type == typeof(AssetTypeDirectory) && !(metadata is ModAssetBranch))
                return;

            ModAsset metadataPrev;

            lock (Map) {
                // We want our new mapping to replace the previous one, but need to replace the previous one in the shadow structure.
                if (!Map.TryGetValue(path, out metadataPrev))
                    metadataPrev = null;

                if (!overwrite && metadataPrev != null)
                    return;

                if (metadata == null && metadataPrev != null && metadataPrev.Type == typeof(AssetTypeDirectory))
                    return;

                if (metadata == null) {
                    Map.Remove(path);
                    if (prefix != null)
                        Map.Remove($"{prefix}:/{path}");

                } else {
                    if (Map.TryGetValue(path, out ModAsset existing) && existing != null &&
                        existing.Source != metadata.Source && !NonConflictTypes.Contains(existing.Type)) {
                        Logger.Log("FEZMod.Content", $"CONFLICT for asset path {path} ({existing?.Source?.ID ?? "???"} vs {metadata?.Source?.ID ?? "???"})");
                    }

                    Map[path] = metadata;
                    if (prefix != null)
                        Map[$"{prefix}:/{path}"] = metadata;
                }

                // If we're not already the highest level shadow "node"...
                if (path != "") {
                    // Add directories automatically.
                    string pathDir = Path.GetDirectoryName(path).Replace('\\', '/');
                    if (!Map.TryGetValue(pathDir, out ModAsset metadataDir)) {
                        metadataDir = new ModAssetBranch {
                            PathVirtual = pathDir,
                            Type = typeof(AssetTypeDirectory)
                        };
                        Add(pathDir, metadataDir);
                    }
                    // If a previous mapping exists, replace it in the shadow structure.
                    lock (metadataDir.Children) {
                        int metadataPrevIndex = metadataDir.Children.IndexOf(metadataPrev);
                        if (metadataPrevIndex != -1) {
                            if (metadata == null) {
                                metadataDir.Children.RemoveAt(metadataPrevIndex);
                            } else {
                                metadataDir.Children[metadataPrevIndex] = metadata;
                            }
                        } else {
                            metadataDir.Children.Add(metadata);
                        }
                    }
                }
            }

            if (GameLoadedContent) {
                // We're late-loading this mod asset and thus need to manually ingest new assets.
                // FIXME: Endless recursion on game startup.
                // Logger.Log("FEZMod.Content", $"Late ingest via update for {prefix}:/{path}");
                // Update(metadataPrev, metadata);
            }
        }

        public static event AssetTypeGuesser OnGuessType;

        public static string GuessType(string file, out Type type, out string format) {
            type = typeof(object);
            format = Path.GetExtension(file) ?? "";
            if (format.Length >= 1)
                format = format.Substring(1);

            if (file.EndsWith(".dll")) {
                type = typeof(AssetTypeAssembly);

            } else if (file.EndsWith(".png")) {
                type = typeof(Texture2D);
                file = file.Substring(0, file.Length - 4);

            } else if (
                file == "fezmod.yaml" ||
                file == "fezmod.yml"
            ) {
                type = typeof(AssetTypeMetadataYaml);
                file = file.Substring(0, file.Length - (file.EndsWith(".yaml") ? 5 : 4));
                format = ".yml";

            } else if (file.EndsWith(".yaml")) {
                type = typeof(AssetTypeYaml);
                file = file.Substring(0, file.Length - 5);
                format = ".yml";
            } else if (file.EndsWith(".yml")) {
                type = typeof(AssetTypeYaml);
                file = file.Substring(0, file.Length - 4);

            } else if (file.EndsWith(".xml")) {
                type = typeof(AssetTypeXml);
                file = file.Substring(0, file.Length - 4);

            } else if (file.EndsWith(".txt")) {
                type = typeof(AssetTypeText);
                file = file.Substring(0, file.Length - 4);

            } else if (OnGuessType != null) {
                // Allow mods to parse custom types.
                Delegate[] ds = OnGuessType.GetInvocationList();
                for (int i = 0; i < ds.Length; i++) {
                    string fileMod = ((AssetTypeGuesser) ds[i])(file, out Type typeMod, out string formatMod);
                    if (fileMod == null || typeMod == null || formatMod == null)
                        continue;
                    file = fileMod;
                    type = typeMod;
                    format = formatMod;
                    break;
                }
            }

            return file;
        }

        public static event Action<ModAsset, ModAsset> OnUpdate;
        public static void Update(ModAsset prev, ModAsset next) {
            if (prev != null) {
                foreach (object target in prev.Targets) {
                    if (target is Dirtyable<Texture> mtex) {
                        // FIXME: Port from Everest!
                        /*
                        AssetReloadHelper.Do($"{Dialog.Clean("ASSETRELOADHELPER_UNLOADINGTEXTURE")} {Path.GetFileName(prev.PathVirtual)}", () => {
                            mtex.UndoOverride(prev);
                        });
                        */
                    }
                }

                if (next == null || prev.PathVirtual != next.PathVirtual)
                    Add(prev.PathVirtual, null);
            }


            if (next != null) {
                Add(next.PathVirtual, next);

                // Loaded assets can be folders, which means that we need to check the updated assets' entire path.
                HashSet<WeakReference> updated = new HashSet<WeakReference>();
                for (ModAsset asset = next; asset != null && !string.IsNullOrEmpty(asset.PathVirtual); asset = Get(Path.GetDirectoryName(asset.PathVirtual).Replace('\\', '/'))) {
                    int index = LoadedAssetPaths.IndexOf(asset.PathVirtual);
                    if (index == -1)
                        continue;

                    WeakReference weakref = LoadedAssets[index];
                    if (!updated.Add(weakref))
                        continue;

                    object target = weakref.Target;
                    if (!weakref.IsAlive)
                        continue;

                    // Don't feed the entire tree into the loaded asset, just the updated asset.
                    ProcessUpdate(target, next, false);
                }
            }

            OnUpdate?.Invoke(prev, next);
        }

        public static void Crawl(ModAssetSource meta, bool overwrite = true) {
            if (!Mods.Contains(meta))
                Mods.Add(meta);

            // FIXME: Trick asset reload helper into insta-reloading like in Everest!

            meta._Crawl(overwrite);
        }

        public static event Action<object, string> OnProcessLoad;
        public static void ProcessLoad(object asset, string assetNameFull) {
            string assetName = assetNameFull;
            if (assetName.StartsWith(PathContentOrig)) {
                assetName = assetName.Substring(PathContentOrig.Length + 1);
            }
            assetName = assetName.Replace('\\', '/');

            int loadedIndex = LoadedAssetPaths.IndexOf(assetName);
            if (loadedIndex == -1) {
                LoadedAssetPaths.Add(assetName);
                LoadedAssetFullPaths.Add(assetNameFull);
                LoadedAssets.Add(new WeakReference(asset));
            } else {
                LoadedAssets[loadedIndex] = new WeakReference(asset);
            }

            OnProcessLoad?.Invoke(asset, assetName);

            ProcessUpdate(asset, Get(assetName), true);
        }

        public static event Action<object, ModAsset, bool> OnProcessUpdate;
        public static void ProcessUpdate(object asset, ModAsset mapping, bool load) {
            if (asset == null || mapping == null)
                return;

            /*
            if (asset is Atlas atlas) {
                string reloadingText = Dialog.Language == null ? "" : Dialog.Clean(mapping.Children.Count == 0 ? "ASSETRELOADHELPER_RELOADINGTEXTURE" : "ASSETRELOADHELPER_RELOADINGTEXTURES");
                AssetReloadHelper.Do(load, $"{reloadingText} {Path.GetFileName(mapping.PathVirtual)}", () => {
                    atlas.ResetCaches();
                    (atlas as patch_Atlas).Ingest(mapping);
                });
            }
            */

            OnProcessUpdate?.Invoke(asset, mapping, load);
        }

    }
}
