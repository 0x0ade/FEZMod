using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using MonoMod;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Ionic.Zip;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Pdb;
using Common;

namespace FezEngine.Mod {
    public static class ModRelinker {

        public static readonly List<Assembly> RelinkedAssemblies = new List<Assembly>();

        public static readonly HashAlgorithm ChecksumHasher = MD5.Create();

        public static string GameChecksum { get; internal set; }

        internal static readonly Dictionary<string, ModuleDefinition> StaticRelinkModuleCache = new Dictionary<string, ModuleDefinition>() {
            { "MonoMod", ModuleDefinition.ReadModule(typeof(MonoModder).Assembly.Location, new ReaderParameters(ReadingMode.Immediate)) },
            { FezModEngine.Instance.GetType().Assembly.GetName().Name, ModuleDefinition.ReadModule(FezModEngine.Instance.GetType().Assembly.Location, new ReaderParameters(ReadingMode.Immediate)) },
            { "FezEngine", ModuleDefinition.ReadModule(typeof(FezModEngine).Assembly.Location, new ReaderParameters(ReadingMode.Immediate)) },
            { "Common", ModuleDefinition.ReadModule(typeof(Logger).Assembly.Location, new ReaderParameters(ReadingMode.Immediate)) },
        };
        internal static bool RuntimeRulesParsed = false;

        private static Dictionary<string, ModuleDefinition> _SharedRelinkModuleMap;
        public static Dictionary<string, ModuleDefinition> SharedRelinkModuleMap {
            get {
                if (_SharedRelinkModuleMap != null)
                    return _SharedRelinkModuleMap;

                _SharedRelinkModuleMap = new Dictionary<string, ModuleDefinition>();
                string[] entries = Directory.GetFiles(FezModEngine.Instance.PathGame);
                for (int i = 0; i < entries.Length; i++) {
                    string path = entries[i];
                    string name = Path.GetFileName(path);
                    string nameNeutral = name.Substring(0, Math.Max(0, name.Length - 4));
                    if (name.EndsWith(".mm.dll")) {
                        if (name.StartsWith("FEZ."))
                            _SharedRelinkModuleMap[nameNeutral] = StaticRelinkModuleCache["FEZ"];
                        else if (name.StartsWith("FezEngine."))
                            _SharedRelinkModuleMap[nameNeutral] = StaticRelinkModuleCache["FezEngine"];
                        else if (name.StartsWith("Common."))
                            _SharedRelinkModuleMap[nameNeutral] = StaticRelinkModuleCache["Common"];
                        else {
                            Logger.Log("FEZMod.Relinker", $"Found unknown {name}");
                            int dot = name.IndexOf('.');
                            if (dot < 0)
                                continue;
                            string nameRelinkedNeutral = name.Substring(0, dot);
                            string nameRelinked = nameRelinkedNeutral + ".dll";
                            string pathRelinked = Path.Combine(Path.GetDirectoryName(path), nameRelinked);
                            if (!File.Exists(pathRelinked))
                                continue;
                            if (!StaticRelinkModuleCache.TryGetValue(nameRelinkedNeutral, out ModuleDefinition relinked)) {
                                relinked = ModuleDefinition.ReadModule(pathRelinked, new ReaderParameters(ReadingMode.Immediate));
                                StaticRelinkModuleCache[nameRelinkedNeutral] = relinked;
                            }
                            Logger.Log("FEZMod.Relinker", $"Remapped to {nameRelinked}");
                            _SharedRelinkModuleMap[nameNeutral] = relinked;
                        }
                    }
                }
                return _SharedRelinkModuleMap;
            }
        }

        private static Dictionary<string, object> _SharedRelinkMap;
        public static Dictionary<string, object> SharedRelinkMap {
            get {
                if (_SharedRelinkMap != null)
                    return _SharedRelinkMap;

                _SharedRelinkMap = new Dictionary<string, object>();

                // Find our current XNA flavour and relink all types to it.
                // This relinks mods from XNA to FNA and from FNA to XNA.

                AssemblyName[] asmRefs = typeof(FezModEngine).Assembly.GetReferencedAssemblies();
                for (int ari = 0; ari < asmRefs.Length; ari++) {
                    AssemblyName asmRef = asmRefs[ari];
                    // Ugly hardcoded supported framework list.
                    if (!asmRef.FullName.ToLowerInvariant().Contains("xna") &&
                        !asmRef.FullName.ToLowerInvariant().Contains("fna") &&
                        !asmRef.FullName.ToLowerInvariant().Contains("monogame") // Contains many differences - we should print a warning.
                    )
                        continue;
                    Assembly asm = Assembly.Load(asmRef);
                    ModuleDefinition module = ModuleDefinition.ReadModule(asm.Location, new ReaderParameters(ReadingMode.Immediate));
                    SharedRelinkModuleMap[asmRef.FullName] = SharedRelinkModuleMap[asmRef.Name] = module;
                    Type[] types = asm.GetExportedTypes();
                    for (int i = 0; i < types.Length; i++) {
                        Type type = types[i];
                        TypeDefinition typeDef = module.GetType(type.FullName) ?? module.GetType(type.FullName.Replace('+', '/'));
                        if (typeDef == null)
                            continue;
                        SharedRelinkMap[typeDef.FullName] = typeDef;
                    }
                }

                return _SharedRelinkMap;
            }
        }

        internal static bool SharedModder = true;
        private static MonoModder _Modder;
        public static MonoModder Modder {
            get {
                if (_Modder != null)
                    return _Modder;

                _Modder = new MonoModder() {
                    CleanupEnabled = false,
                    RelinkModuleMap = SharedRelinkModuleMap,
                    RelinkMap = SharedRelinkMap,
                    DependencyDirs = {
                        FezModEngine.Instance.PathGame
                    },
                    ReaderParameters = {
                        SymbolReaderProvider = new RelinkerSymbolReaderProvider()
                    }
                };

                ((DefaultAssemblyResolver) _Modder.AssemblyResolver).ResolveFailure += OnRelinkerResolveFailure;

                return _Modder;
            }
            set {
                _Modder = value;
            }
        }

        private static AssemblyDefinition OnRelinkerResolveFailure(object sender, AssemblyNameReference reference) {
            if (reference.FullName.ToLowerInvariant().Contains("fna") || reference.FullName.ToLowerInvariant().Contains("xna")) {
                AssemblyName[] asmRefs = typeof(FezModEngine).Assembly.GetReferencedAssemblies();
                for (int ari = 0; ari < asmRefs.Length; ari++) {
                    AssemblyName asmRef = asmRefs[ari];
                    if (!asmRef.FullName.ToLowerInvariant().Contains("xna") &&
                        !asmRef.FullName.ToLowerInvariant().Contains("fna") &&
                        !asmRef.FullName.ToLowerInvariant().Contains("monogame")
                    )
                        continue;
                    return ((DefaultAssemblyResolver) _Modder.AssemblyResolver).Resolve(AssemblyNameReference.Parse(asmRef.FullName));
                }
            }

            return null;
        }

        public static Assembly GetRelinkedAssembly(ModMetadata meta, string asmname, Stream stream,
            MissingDependencyResolver depResolver = null, string[] checksumsExtra = null, Action<MonoModder> prePatch = null) {

            string cachedPath = GetCachedPath(meta, asmname);
            string cachedChecksumPath = cachedPath.Substring(0, cachedPath.Length - 4) + ".sum";

            string[] checksums = new string[2 + (checksumsExtra?.Length ?? 0)];
            if (GameChecksum == null)
                GameChecksum = GetChecksum(Assembly.GetAssembly(typeof(ModRelinker)).Location);
            checksums[0] = GameChecksum;

            checksums[1] = GetChecksum(ref stream).ToHexadecimalString();

            if (checksumsExtra != null)
                for (int i = 0; i < checksumsExtra.Length; i++) {
                    checksums[i + 2] = checksumsExtra[i];
                }

            if (File.Exists(cachedPath) && File.Exists(cachedChecksumPath) &&
                ChecksumsEqual(checksums, File.ReadAllLines(cachedChecksumPath))) {
                Logger.Log("FEZMod.Relinker", $"Loading cached assembly for {meta} - {asmname}");
                try {
                    Assembly asm = Assembly.LoadFrom(cachedPath);
                    RelinkedAssemblies.Add(asm);
                    return asm;
                } catch (Exception e) {
                    Logger.Log("FEZMod.Relinker", $"Failed loading {meta} - {asmname}");
                    e.LogDetailed();
                    return null;
                }
            }

            if (depResolver == null)
                depResolver = GenerateModDependencyResolver(meta);

            bool temporaryASM = false;

            try {
                MonoModder modder = Modder;

                modder.Input = stream;
                modder.OutputPath = cachedPath;
                modder.MissingDependencyResolver = depResolver;

                modder.ReaderParameters.SymbolStream = OpenStream(meta, out string symbolPath, meta.DLL.Substring(0, meta.DLL.Length - 4) + ".pdb", meta.DLL + ".mdb");
                modder.ReaderParameters.ReadSymbols = modder.ReaderParameters.SymbolStream != null;
                if (modder.ReaderParameters.SymbolReaderProvider != null &&
                    modder.ReaderParameters.SymbolReaderProvider is RelinkerSymbolReaderProvider) {
                    ((RelinkerSymbolReaderProvider) modder.ReaderParameters.SymbolReaderProvider).Format =
                        string.IsNullOrEmpty(symbolPath) ? DebugSymbolFormat.Auto :
                        symbolPath.EndsWith(".mdb") ? DebugSymbolFormat.MDB :
                        symbolPath.EndsWith(".pdb") ? DebugSymbolFormat.PDB :
                        DebugSymbolFormat.Auto;
                }

                try {
                    modder.ReaderParameters.ReadSymbols = true;
                    modder.Read();
                } catch {
                    modder.ReaderParameters.SymbolStream?.Dispose();
                    modder.ReaderParameters.SymbolStream = null;
                    modder.ReaderParameters.ReadSymbols = false;
                    stream.Seek(0, SeekOrigin.Begin);
                    modder.Read();
                }

                if (modder.ReaderParameters.SymbolReaderProvider != null &&
                    modder.ReaderParameters.SymbolReaderProvider is RelinkerSymbolReaderProvider) {
                    ((RelinkerSymbolReaderProvider) modder.ReaderParameters.SymbolReaderProvider).Format = DebugSymbolFormat.Auto;
                }

                modder.MapDependencies();

                if (!RuntimeRulesParsed) {
                    RuntimeRulesParsed = true;

                    InitMMSharedData();

                    string rulesPath = Path.Combine(
                        Path.GetDirectoryName(typeof(FezModEngine).Assembly.Location),
                        "FEZ.Mod.mm.dll"
                    );
                    if (File.Exists(rulesPath)) {
                        ModuleDefinition rules = ModuleDefinition.ReadModule(rulesPath, new ReaderParameters(ReadingMode.Immediate));
                        modder.ParseRules(rules);
                        rules.Dispose(); // Is this safe?
                    }

                    rulesPath = Path.Combine(
                        Path.GetDirectoryName(typeof(FezModEngine).Assembly.Location),
                        "FezEngine.Mod.mm.dll"
                    );
                    if (File.Exists(rulesPath)) {
                        ModuleDefinition rules = ModuleDefinition.ReadModule(rulesPath, new ReaderParameters(ReadingMode.Immediate));
                        modder.ParseRules(rules);
                        rules.Dispose(); // Is this safe?
                    }
                }

                prePatch?.Invoke(modder);

                modder.ParseRules(modder.Module);

                modder.AutoPatch();

                RetryWrite:
                try {
                    modder.WriterParameters.WriteSymbols = true;
                    modder.Write();
                } catch {
                    try {
                        modder.WriterParameters.WriteSymbols = false;
                        modder.Write();
                    } catch when (!temporaryASM) {
                        temporaryASM = true;
                        long stamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        cachedPath = Path.Combine(Path.GetTempPath(), $"Everest.Relinked.{Path.GetFileNameWithoutExtension(cachedPath)}.{stamp}.dll");
                        modder.Module.Name += "." + stamp;
                        modder.Module.Assembly.Name.Name += "." + stamp;
                        modder.OutputPath = cachedPath;
                        modder.WriterParameters.WriteSymbols = true;
                        goto RetryWrite;
                    }
                }
            } catch (Exception e) {
                Logger.Log("FEZMod.Relinker", $"Failed relinking {meta} - {asmname}");
                e.LogDetailed();
                return null;
            } finally {
                Modder.ReaderParameters.SymbolStream?.Dispose();
                if (SharedModder) {
                    Modder.ClearCaches(moduleSpecific: true);
                    Modder.Module.Dispose();
                    Modder.Module = null;

                } else {
                    Modder.Dispose();
                    Modder = null;
                }
            }

            if (File.Exists(cachedChecksumPath)) {
                File.Delete(cachedChecksumPath);
            }
            if (!temporaryASM) {
                File.WriteAllLines(cachedChecksumPath, checksums);
            }

            Logger.Log("FEZMod.Relinker", $"Loading assembly for {meta} - {asmname}");
            try {
                Assembly asm = Assembly.LoadFrom(cachedPath);
                RelinkedAssemblies.Add(asm);
                return asm;
            } catch (Exception e) {
                Logger.Log("FEZMod.Relinker", $"Failed loading {meta} - {asmname}");
                e.LogDetailed();
                return null;
            }
        }

        private static MissingDependencyResolver GenerateModDependencyResolver(ModMetadata meta) {
            if (!string.IsNullOrEmpty(meta.PathArchive)) {
                return (mod, main, name, fullName) => {
                    string path = name + ".dll";
                    if (!string.IsNullOrEmpty(meta.DLL))
                        path = Path.Combine(Path.GetDirectoryName(meta.DLL), path);
                    path = path.Replace('\\', '/');

                    using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                        foreach (ZipEntry entry in zip.Entries) {
                            if (entry.FileName != path)
                                continue;
                            using (MemoryStream stream = entry.ExtractStream())
                                return ModuleDefinition.ReadModule(stream, mod.GenReaderParameters(false));
                        }
                    }
                    return null;
                };
            }

            if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                return (mod, main, name, fullName) => {
                    string path = name + ".dll";
                    if (!string.IsNullOrEmpty(meta.DLL))
                        path = Path.Combine(Path.GetDirectoryName(meta.DLL), path);
                    if (!File.Exists(path))
                        path = Path.Combine(meta.PathDirectory, path);
                    if (!File.Exists(path))
                        return null;

                    return ModuleDefinition.ReadModule(path, mod.GenReaderParameters(false, path));
                };
            }

            return null;
        }

        private static Stream OpenStream(ModMetadata meta, out string result, params string[] names) {
            if (!string.IsNullOrEmpty(meta.PathArchive)) {
                using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                    foreach (ZipEntry entry in zip.Entries) {
                        if (!names.Contains(entry.FileName))
                            continue;
                        result = entry.FileName;
                        return entry.ExtractStream();
                    }
                }
                result = null;
                return null;
            }

            if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                foreach (string name in names) {
                    string path = name;
                    if (!File.Exists(path))
                        path = Path.Combine(meta.PathDirectory, name);
                    if (!File.Exists(path))
                        continue;
                    result = path;
                    return File.OpenRead(path);
                }
            }

            result = null;
            return null;
        }

        public static string GetCachedPath(ModMetadata meta, string asmname)
            => Path.Combine(ModLoader.PathCache, meta.ID + "." + asmname + ".dll");

        public static string GetChecksum(ModMetadata meta) {
            string path = meta.PathArchive;
            if (string.IsNullOrEmpty(path))
                path = meta.DLL;
            if (string.IsNullOrEmpty(path))
                return "";
            return GetChecksum(path);
        }

        public static string GetChecksum(string path) {
            using (FileStream fs = File.OpenRead(path))
                return ChecksumHasher.ComputeHash(fs).ToHexadecimalString();
        }

        public static byte[] GetChecksum(ref Stream stream) {
            if (!stream.CanSeek) {
                MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                stream.Dispose();
                stream = ms;
                stream.Seek(0, SeekOrigin.Begin);
            }

            long pos = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);
            byte[] hash = ChecksumHasher.ComputeHash(stream);
            stream.Seek(pos, SeekOrigin.Begin);
            return hash;
        }

        public static bool ChecksumsEqual(string[] a, string[] b) {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].Trim() != b[i].Trim())
                    return false;
            return true;
        }

        [PatchInitMMSharedData]
        private static void InitMMSharedData() {
            // This method is automatically filled via MonoModRules.
        }
        private static void SetMMSharedData(string key, bool value) {
            Modder.SharedData[key] = value;
        }

    }
}
