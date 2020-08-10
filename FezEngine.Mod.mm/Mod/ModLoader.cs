using Microsoft.Xna.Framework.Graphics;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Ionic.Zip;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MonoMod.Utils;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MCC = Mono.Cecil.Cil;
using MonoMod.Cil;
using Microsoft.Xna.Framework;
using System.Diagnostics;
using Common;

namespace FezEngine.Mod {
    public static class ModLoader {

        public static string PathMods { get; internal set; }
        public static string PathCache { get; internal set; }

        public static string PathBlacklist { get; internal set; }
        internal static List<string> _Blacklist = new List<string>();
        public static ReadOnlyCollection<string> Blacklist => _Blacklist?.AsReadOnly();

        public static string PathWhitelist { get; internal set; }
        internal static string NameWhitelist;
        internal static List<string> _Whitelist;
        public static ReadOnlyCollection<string> Whitelist => _Whitelist?.AsReadOnly();

        internal static List<Tuple<ModMetadata, Action>> Delayed = new List<Tuple<ModMetadata, Action>>();
        internal static int DelayedLock;

        internal static readonly Version _VersionInvalid = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
        internal static readonly Version _VersionMax = new Version(int.MaxValue, int.MaxValue);

        internal static Dictionary<string, Version> PermanentBlacklist = new Dictionary<string, Version>() {

            // Note: Most, if not all mods use Major.Minor.Build
            // Revision is thus set to -1 and < 0
            // Entries with a revision of 0 are there because there is no update / fix for those mods.

        };

        internal static HashSet<Tuple<string, Version, string, Version>> PermanentConflictlist = new HashSet<Tuple<string, Version, string, Version>>() {

            // See above versioning note.

        };

        internal static FileSystemWatcher Watcher;

        public static bool AutoLoadNewMods { get; internal set; }

        internal static void LoadAuto() {
            Directory.CreateDirectory(PathMods = Path.Combine(FezModEngine.Instance.PathGame, "Mods"));
            Directory.CreateDirectory(PathCache = Path.Combine(PathMods, "Cache"));

            PathBlacklist = Path.Combine(PathMods, "blacklist.txt");
            if (File.Exists(PathBlacklist)) {
                _Blacklist = File.ReadAllLines(PathBlacklist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
            } else {
                using (StreamWriter writer = File.CreateText(PathBlacklist)) {
                    writer.WriteLine("# This is the blacklist. Lines starting with # are ignored.");
                    writer.WriteLine("ExampleFolder");
                    writer.WriteLine("SomeMod.zip");
                }
            }

            if (!string.IsNullOrEmpty(NameWhitelist)) {
                PathWhitelist = Path.Combine(PathMods, NameWhitelist);
                if (File.Exists(PathWhitelist)) {
                    _Whitelist = File.ReadAllLines(PathWhitelist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
                }
            }

            Stopwatch watch = Stopwatch.StartNew();

            string[] files = Directory.GetFiles(PathMods);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (!file.EndsWith(".zip") || _Blacklist.Contains(file))
                    continue;
                if (_Whitelist != null && !_Whitelist.Contains(file))
                    continue;
                LoadZip(file);
            }

            files = Directory.GetDirectories(PathMods);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (file == "Cache" || _Blacklist.Contains(file))
                    continue;
                if (_Whitelist != null && !_Whitelist.Contains(file))
                    continue;
                LoadDir(file);
            }

            watch.Stop();
            Logger.Log("FEZMod.Loader", $"ALL MODS LOADED IN {watch.ElapsedMilliseconds}ms");

            Watcher = new FileSystemWatcher {
                Path = PathMods,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            Watcher.Created += LoadAutoUpdated;

            Watcher.EnableRaisingEvents = true;
            AutoLoadNewMods = true;
        }

        private static void LoadAutoUpdated(object source, FileSystemEventArgs e) {
            if (!AutoLoadNewMods)
                return;

            Logger.Log("FEZMod.Loader", $"Possible new mod container: {e.FullPath}");
            QueuedTaskHelper.Do("LoadAutoUpdated:" + e.FullPath, () => MainThreadHelper.Do(() => {
                if (Directory.Exists(e.FullPath))
                    LoadDir(e.FullPath);
                else if (e.FullPath.EndsWith(".zip"))
                    LoadZip(e.FullPath);
            }));
        }

        public static void LoadZip(string archive) {
            if (!File.Exists(archive))
                archive = Path.Combine(PathMods, archive);
            if (!File.Exists(archive))
                return;

            Logger.Log("FEZMod.Loader", $"Loading mod .zip: {archive}");

            ModMetadata meta = null;
            ModMetadata[] multimetas = null;

            using (ZipFile zip = new ZipFile(archive)) {
                foreach (ZipEntry entry in zip.Entries) {
                    if (entry.FileName == "metadata.yaml") {
                        using (MemoryStream stream = entry.ExtractStream())
                        using (StreamReader reader = new StreamReader(stream)) {
                            try {
                                meta = YamlHelper.Deserializer.Deserialize<ModMetadata>(reader);
                                meta.PathArchive = archive;
                                meta.PostParse();
                            } catch (Exception e) {
                                Logger.Log("FEZMod.Loader", $"Failed parsing metadata.yaml in {archive}: {e}");
                            }
                        }
                        continue;
                    }
                    if (entry.FileName == "multimetadata.yaml" ||
                        entry.FileName == "everest.yaml" ||
                        entry.FileName == "everest.yml") {
                        using (MemoryStream stream = entry.ExtractStream())
                        using (StreamReader reader = new StreamReader(stream)) {
                            try {
                                if (!reader.EndOfStream) {
                                    multimetas = YamlHelper.Deserializer.Deserialize<ModMetadata[]>(reader);
                                    foreach (ModMetadata multimeta in multimetas) {
                                        multimeta.PathArchive = archive;
                                        multimeta.PostParse();
                                    }
                                }
                            } catch (Exception e) {
                                Logger.Log("FEZMod.Loader", $"Failed parsing multimetadata.yaml in {archive}: {e}");
                            }
                        }
                        continue;
                    }
                }
            }

            ZipModAssetSource contentMeta = new ZipModAssetSource(archive);
            ModMetadata contentMetaParent = null;

            Action contentCrawl = () => {
                if (contentMeta == null)
                    return;
                if (contentMetaParent != null) {
                    contentMeta.Mod = contentMetaParent;
                    contentMeta.ID = contentMetaParent.ID;
                }
                ModContent.Crawl(contentMeta);
                contentMeta = null;
            };

            if (multimetas != null) {
                foreach (ModMetadata multimeta in multimetas) {
                    multimeta.Multimeta = multimetas;
                    if (contentMetaParent == null)
                        contentMetaParent = multimeta;
                    LoadModDelayed(multimeta, contentCrawl);
                }
            } else {
                if (meta == null) {
                    meta = new ModMetadata() {
                        ID = "_zip_" + Path.GetFileNameWithoutExtension(archive),
                        VersionString = "0.0.0-dummy",
                        PathArchive = archive
                    };
                    meta.PostParse();
                }
                contentMetaParent = meta;
                LoadModDelayed(meta, contentCrawl);
            }
        }

        public static void LoadDir(string dir) {
            if (!Directory.Exists(dir))
                dir = Path.Combine(PathMods, dir);
            if (!Directory.Exists(dir))
                return;

            Logger.Log("FEZMod.Loader", $"Loading mod directory: {dir}");

            ModMetadata meta = null;
            ModMetadata[] multimetas = null;

            string metaPath = Path.Combine(dir, "metadata.yaml");
            if (File.Exists(metaPath))
                using (StreamReader reader = new StreamReader(metaPath)) {
                    try {
                        meta = YamlHelper.Deserializer.Deserialize<ModMetadata>(reader);
                        meta.PathDirectory = dir;
                        meta.PostParse();
                    } catch (Exception e) {
                        Logger.Log("FEZMod.Loader", $"Failed parsing metadata.yaml in {dir}: {e}");
                    }
                }

            metaPath = Path.Combine(dir, "multimetadata.yaml");
            if (!File.Exists(metaPath))
                metaPath = Path.Combine(dir, "everest.yaml");
            if (!File.Exists(metaPath))
                metaPath = Path.Combine(dir, "everest.yml");
            if (File.Exists(metaPath))
                using (StreamReader reader = new StreamReader(metaPath)) {
                    try {
                        if (!reader.EndOfStream) {
                            multimetas = YamlHelper.Deserializer.Deserialize<ModMetadata[]>(reader);
                            foreach (ModMetadata multimeta in multimetas) {
                                multimeta.PathDirectory = dir;
                                multimeta.PostParse();
                            }
                        }
                    } catch (Exception e) {
                        Logger.Log("FEZMod.Loader", $"Failed parsing everest.yaml in {dir}: {e}");
                    }
                }

            FileSystemModAssetSource contentMeta = new FileSystemModAssetSource(dir);
            ModMetadata contentMetaParent = null;

            Action contentCrawl = () => {
                if (contentMeta == null)
                    return;
                if (contentMetaParent != null) {
                    contentMeta.Mod = contentMetaParent;
                    contentMeta.ID = contentMetaParent.ID;
                }
                ModContent.Crawl(contentMeta);
                contentMeta = null;
            };

            if (multimetas != null) {
                foreach (ModMetadata multimeta in multimetas) {
                    multimeta.Multimeta = multimetas;
                    if (contentMetaParent == null)
                        contentMetaParent = multimeta;
                    LoadModDelayed(multimeta, contentCrawl);
                }
            } else {
                if (meta == null) {
                    meta = new ModMetadata() {
                        ID = "_dir_" + Path.GetFileName(dir),
                        VersionString = "0.0.0-dummy",
                        PathDirectory = dir
                    };
                    meta.PostParse();
                }
                contentMetaParent = meta;
                LoadModDelayed(meta, contentCrawl);
            }
        }

        /// <summary>
        /// Load a mod .dll given its metadata at runtime. Doesn't load the mod content.
        /// If required, loads the mod after all of its dependencies have been loaded.
        /// </summary>
        /// <param name="meta">Metadata of the mod to load.</param>
        /// <param name="callback">Callback to be executed after the mod has been loaded. Executed immediately if meta == null.</param>
        public static void LoadModDelayed(ModMetadata meta, Action callback) {
            if (meta == null) {
                callback?.Invoke();
                return;
            }

            if (DependencyLoaded(meta)) {
                Logger.Log("FEZMod.Loader", $"Mod {meta} already loaded!");
                return;
            }

            if (PermanentBlacklist.TryGetValue(meta.ID, out Version minver) && meta.Version < minver) {
                Logger.Log("FEZMod.Loader", $"Mod {meta} permanently blacklisted by Everest!");
                return;
            }

            Tuple<string, Version, string, Version> conflictRow = PermanentConflictlist.FirstOrDefault(row =>
                (meta.ID == row.Item1 && meta.Version < row.Item2 && (FezModEngine.Instance.Modules.FirstOrDefault(other => other.Metadata.ID == row.Item3)?.Metadata.Version ?? _VersionInvalid) < row.Item4) ||
                (meta.ID == row.Item3 && meta.Version < row.Item4 && (FezModEngine.Instance.Modules.FirstOrDefault(other => other.Metadata.ID == row.Item1)?.Metadata.Version ?? _VersionInvalid) < row.Item2)
            );
            if (conflictRow != null) {
                throw new Exception($"CONFLICTING MODS: {conflictRow.Item1} VS {conflictRow.Item3}");
            }


            foreach (ModMetadata dep in meta.Dependencies)
                if (!DependencyLoaded(dep)) {
                    Logger.Log("FEZMod.Loader", $"Dependency {dep} of mod {meta} not loaded! Delaying.");
                    lock (Delayed) {
                        Delayed.Add(Tuple.Create(meta, callback));
                    }
                    return;
                }

            callback?.Invoke();

            LoadMod(meta);
        }

        public static void LoadMod(ModMetadata meta) {
            if (meta == null)
                return;

            AppDomain.CurrentDomain.AssemblyResolve += GenerateModAssemblyResolver(meta);

            Assembly asm = null;
            if (!string.IsNullOrEmpty(meta.PathArchive)) {
                bool returnEarly = false;
                using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                    foreach (ZipEntry entry in zip.Entries) {
                        string entryName = entry.FileName.Replace('\\', '/');
                        if (entryName == meta.DLL) {
                            using (MemoryStream stream = entry.ExtractStream())
                                asm = ModRelinker.GetRelinkedAssembly(meta, Path.GetFileNameWithoutExtension(meta.DLL), stream);
                        }
                    }
                }

                if (returnEarly)
                    return;

            } else {
                if (!string.IsNullOrEmpty(meta.DLL) && File.Exists(meta.DLL)) {
                    using (FileStream stream = File.OpenRead(meta.DLL))
                        asm = ModRelinker.GetRelinkedAssembly(meta, Path.GetFileNameWithoutExtension(meta.DLL), stream);
                }
            }

            if (asm == null) {
                FezModEngine.Instance.Register(new NullModule(meta));
                return;
            }

            LoadModAssembly(meta, asm);
        }

        public static void LoadModAssembly(ModMetadata meta, Assembly asm) {
            if (string.IsNullOrEmpty(meta.PathArchive) && File.Exists(meta.DLL) && meta.SupportsCodeReload && FezModEngine.Instance.Settings.CodeReload) {
                FileSystemWatcher watcher = meta.DevWatcher = new FileSystemWatcher {
                    Path = Path.GetDirectoryName(meta.DLL),
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                };

                watcher.Changed += (s, e) => {
                    if (e.FullPath != meta.DLL)
                        return;
                    ReloadModAssembly(s, e);
                };

                watcher.EnableRaisingEvents = true;
            }

            ApplyModHackfixes(meta, asm);

            ModContent.Crawl(new AssemblyModAssetSource(asm) {
                Mod = meta,
                ID = meta.ID
            });

            Type[] types;
            try {
                types = asm.GetTypesSafe();
            } catch (Exception e) {
                Logger.Log("FEZMod.Loader", $"Failed reading assembly: {e}");
                e.LogDetailed();
                return;
            }

            for (int i = 0; i < types.Length; i++) {
                Type type = types[i];

                if (typeof(ModBase).IsAssignableFrom(type) && !type.IsAbstract && !typeof(NullModule).IsAssignableFrom(type)) {
                    ModBase mod = (ModBase) Activator.CreateInstance(type);
                    mod.Metadata = meta;
                    FezModEngine.Instance.Register(mod);
                }
            }
        }

        internal static void ReloadModAssembly(object source, FileSystemEventArgs e, bool retrying = false) {
            if (!File.Exists(e.FullPath))
                return;

            Logger.Log("FEZMod.Loader", $"Reloading mod assembly: {e.FullPath}");
            QueuedTaskHelper.Do("ReloadModAssembly:" + e.FullPath, () => {
                ModBase module = FezModEngine.Instance.Modules.FirstOrDefault(m => m.Metadata.DLL == e.FullPath);
                if (module == null)
                    return;

                Assembly asm = null;
                using (FileStream stream = File.OpenRead(e.FullPath))
                    asm = ModRelinker.GetRelinkedAssembly(module.Metadata, Path.GetFileNameWithoutExtension(e.FullPath), stream);

                if (asm == null) {
                    if (!retrying) {
                        // Retry.
                        QueuedTaskHelper.Do("ReloadModAssembly:" + e.FullPath, () => {
                            ReloadModAssembly(source, e, true);
                        });
                    }
                    return;
                }

                ((FileSystemWatcher) source).Dispose();

                // FIXME: Port from Everest!
                /*
                if (SaveData.Instance != null) {
                    Logger.Log("core", $"Saving save data slot {SaveData.Instance.FileSlot} for {module.Metadata} before reloading");
                    module.SaveSaveData(SaveData.Instance.FileSlot);

                    if (SaveData.Instance.CurrentSession?.InArea ?? false) {
                        Logger.Log("core", $"Saving session slot {SaveData.Instance.FileSlot} for {module.Metadata} before reloading");
                        module.SaveSession(SaveData.Instance.FileSlot);
                    }
                }
                */

                FezModEngine.Instance.Unregister(module);
                LoadModAssembly(module.Metadata, asm);
            });
        }

        public static bool DependenciesLoaded(ModMetadata meta) {
            foreach (ModMetadata dep in meta.Dependencies)
                if (!DependencyLoaded(dep))
                    return false;
            return true;
        }

        public static bool DependencyLoaded(ModMetadata dep) {
            string depName = dep.ID;
            Version depVersion = dep.Version;

            lock (FezModEngine.Instance.Modules) {
                foreach (ModBase other in FezModEngine.Instance.Modules) {
                    ModMetadata meta = other.Metadata;
                    if (meta.ID != depName)
                        continue;

                    Version version = meta.Version;
                    return VersionSatisfiesDependency(depVersion, version);
                }
            }

            return false;
        }

        public static bool VersionSatisfiesDependency(Version requiredVersion, Version installedVersion) {
            // Special case: Always true if version == 0.0.*
            if (installedVersion.Major == 0 && installedVersion.Minor == 0)
                return true;

            // Major version, breaking changes, must match.
            if (installedVersion.Major != requiredVersion.Major)
                return false;
            // Minor version, non-breaking changes, installed can't be lower than what we depend on.
            if (installedVersion.Minor < requiredVersion.Minor)
                return false;

            // "Build" is "PATCH" in semver, but we'll also check for it and "Revision".
            if (installedVersion.Minor == requiredVersion.Minor && installedVersion.Build < requiredVersion.Build)
                return false;
            if (installedVersion.Minor == requiredVersion.Minor && installedVersion.Build == requiredVersion.Build && installedVersion.Revision < requiredVersion.Revision)
                return false;

            return true;
        }

        private static ResolveEventHandler GenerateModAssemblyResolver(ModMetadata meta)
            => (sender, args) => {
                AssemblyName name = args?.Name == null ? null : new AssemblyName(args.Name);
                if (string.IsNullOrEmpty(name?.Name))
                    return null;

                string path = name.Name + ".dll";
                if (!string.IsNullOrEmpty(meta.DLL))
                    path = Path.Combine(Path.GetDirectoryName(meta.DLL), path);

                if (!string.IsNullOrEmpty(meta.PathArchive)) {
                    string zipPath = path.Replace('\\', '/');
                    using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                        foreach (ZipEntry entry in zip.Entries) {
                            if (entry.FileName == zipPath)
                                using (MemoryStream stream = entry.ExtractStream())
                                    return ModRelinker.GetRelinkedAssembly(meta, Path.GetFileNameWithoutExtension(zipPath), stream);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                    string filePath = path;
                    if (!File.Exists(filePath))
                        path = Path.Combine(meta.PathDirectory, filePath);
                    if (File.Exists(filePath))
                        using (FileStream stream = File.OpenRead(filePath))
                            return ModRelinker.GetRelinkedAssembly(meta, Path.GetFileNameWithoutExtension(filePath), stream);
                }

                return null;
            };

        private static void ApplyModHackfixes(ModMetadata meta, Assembly asm) {

        }

    }
}
