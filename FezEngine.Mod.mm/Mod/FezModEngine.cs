using Common;
using FezEngine.Mod.Core;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FezEngine.Mod {
    public class FezModEngine {

        public static FezModEngine Instance { get; internal set; }

        public Game Game { get; protected set; }

        public ReadOnlyCollection<string> Args { get; protected set; }

        public string PathGame { get; private set; }

        public ModBase CoreModule { get; private set; }

        public CoreEngineModuleSettings Settings => CoreModule.GetSettings() as CoreEngineModuleSettings;

        public readonly List<ModBase> Modules = new List<ModBase>();

        private DetourModManager _DetourModManager;
        private readonly HashSet<Assembly> _DetourOwners = new HashSet<Assembly>();
        internal readonly List<string> _DetourLog = new List<string>();

        private bool _Initialized;

        public FezModEngine() {
            Instance = this;
        }

        public virtual void Boot(Game game, ModBase coreModule) {
            Game = game;
            CoreModule = coreModule;

            if (Type.GetType("Mono.Runtime") != null) {
                // Mono hates HTTPS.
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
                    return true;
                };
            }

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            PathGame = Path.GetDirectoryName(typeof(FezModEngine).Assembly.Location);

            // .NET hates it when strong-named dependencies get updated.
            AppDomain.CurrentDomain.AssemblyResolve += (asmSender, asmArgs) => {
                AssemblyName asmName = new AssemblyName(asmArgs.Name);
                if (!asmName.Name.StartsWith("Mono.Cecil") &&
                    !asmName.Name.StartsWith("YamlDotNet"))
                    return null;

                Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(other => other.GetName().Name == asmName.Name);
                if (asm != null)
                    return asm;

                return Assembly.LoadFrom(Path.Combine(PathGame, asmName.Name + ".dll"));
            };

            // .NET hates to acknowledge manually loaded assemblies.
            AppDomain.CurrentDomain.AssemblyResolve += (asmSender, asmArgs) => {
                AssemblyName asmName = new AssemblyName(asmArgs.Name);
                foreach (Assembly asm in ModRelinker.RelinkedAssemblies) {
                    if (asm.GetName().Name == asmName.Name)
                        return asm;
                }

                return null;
            };

            _DetourModManager = new DetourModManager();
            _DetourModManager.OnILHook += (owner, from, to) => {
                _DetourOwners.Add(owner);
                object target = to.Target;
                _DetourLog.Add($"new ILHook by {owner.GetName().Name}: {from.GetID()} -> {to.Method?.GetID() ?? "???"}" + (target == null ? "" : $" (target: {target})"));
            };
            _DetourModManager.OnHook += (owner, from, to, target) => {
                _DetourOwners.Add(owner);
                _DetourLog.Add($"new Hook by {owner.GetName().Name}: {from.GetID()} -> {to.GetID()}" + (target == null ? "" : $" (target: {target})"));
            };
            _DetourModManager.OnDetour += (owner, from, to) => {
                _DetourOwners.Add(owner);
                _DetourLog.Add($"new Detour by {owner.GetName().Name}: {from.GetID()} -> {to.GetID()}");
            };
            _DetourModManager.OnNativeDetour += (owner, fromMethod, from, to) => {
                _DetourOwners.Add(owner);
                _DetourLog.Add($"new NativeDetour by {owner.GetName().Name}: {fromMethod?.ToString() ?? from.ToString("16X")} -> {to.ToString("16X")}");
            };
            HookEndpointManager.OnAdd += (from, to) => {
                Assembly owner = HookEndpointManager.GetOwner(to) as Assembly ?? typeof(FezModEngine).Assembly;
                _DetourOwners.Add(owner);
                object target = to.Target;
                _DetourLog.Add($"new On.+= by {owner.GetName().Name}: {from.GetID()} -> {to.Method?.GetID() ?? "???"}" + (target == null ? "" : $" (target: {target})"));
                return true;
            };
            HookEndpointManager.OnModify += (from, to) => {
                Assembly owner = HookEndpointManager.GetOwner(to) as Assembly ?? typeof(FezModEngine).Assembly;
                _DetourOwners.Add(owner);
                object target = to.Target;
                _DetourLog.Add($"new IL.+= by {owner.GetName().Name}: {from.GetID()} -> {to.Method?.GetID() ?? "???"}" + (target == null ? "" : $" (target: {target})"));
                return true;
            };

            MainThreadHelper.Instance = new MainThreadHelper(Game);

            ModContent.Initialize();

            Register(coreModule);
            ModLoader.LoadAuto();

            Queue<string> args = new Queue<string>(Args);
            while (args.Count > 0) {
                string arg = args.Dequeue();
                foreach (ModBase mod in Modules) {
                    if (mod.ParseArg(arg, args))
                        break;
                }
            }
        }

        public virtual void LoadComponentReplacements() {
        }

        public virtual void Initialize() {
            _Initialized = true;
            foreach (ModBase mod in Modules)
                mod.Initialize();
        }

        public virtual void LoadComponents() {
            Game.Components.Add(MainThreadHelper.Instance);
        }

        public void Register(ModBase module) {
            lock (Modules) {
                Modules.Add(module);
            }

            module.LoadSettings();
            module.Load();
            if (ModContent.GameLoadedContent)
                module.LoadContent(true);

            if (_Initialized) {
                module.Initialize();

                // FIXME: Port from Everest!
                /*
                if (SaveData.Instance != null) {
                    // we are in a save. we are expecting the save data to already be loaded at this point
                    Logger.Log("core", $"Loading save data slot {SaveData.Instance.FileSlot} for {module.Metadata}");
                    module.LoadSaveData(SaveData.Instance.FileSlot);

                    if (SaveData.Instance.CurrentSession?.InArea ?? false) {
                        // we are in a level. we are expecting the session to already be loaded at this point
                        Logger.Log("core", $"Loading session slot {SaveData.Instance.FileSlot} for {module.Metadata}");
                        module.LoadSession(SaveData.Instance.FileSlot, false);
                    }
                }
                */
            }

            ModMetadata meta = module.Metadata;
            meta.Hash = ModRelinker.GetChecksum(meta);

            Logger.Log("FEZMod", $"Module {module.Metadata} registered.");

            // Attempt to load mods after their dependencies have been loaded.
            // Only load and lock the delayed list if we're not already loading delayed mods.
            if (Interlocked.CompareExchange(ref ModLoader.DelayedLock, 1, 0) == 0) {
                try {
                    lock (ModLoader.Delayed) {
                        for (int i = 0; i < ModLoader.Delayed.Count; i++) {
                            Tuple<ModMetadata, Action> entry = ModLoader.Delayed[i];
                            if (!ModLoader.DependenciesLoaded(entry.Item1))
                                continue; // dependencies are still missing!

                            Logger.Log("FEZMod", $"Dependencies of mod {entry.Item1} are now satisfied: loading");

                            if (Modules.Any(mod => mod.Metadata.ID == entry.Item1.ID)) {
                                // a duplicate of the mod was loaded while it was sitting in the delayed list.
                                Logger.Log("FEZMod", $"Mod {entry.Item1.ID} already loaded!");
                            } else {
                                entry.Item2?.Invoke();
                                ModLoader.LoadMod(entry.Item1);
                            }
                            ModLoader.Delayed.RemoveAt(i);

                            // we now loaded an extra mod, consider all delayed mods again to deal with transitive dependencies.
                            i = -1;
                        }
                    }
                } finally {
                    Interlocked.Decrement(ref ModLoader.DelayedLock);
                }
            }
        }

        internal void Unregister(ModBase module) {
            module.Unload();

            Assembly asm = module.GetType().Assembly;
            MainThreadHelper.Do(() => _DetourModManager.Unload(asm));
            ModRelinker.RelinkedAssemblies.Remove(asm);

            // TODO: Undo event listeners
            // TODO: Make sure modules depending on this are unloaded as well.
            // TODO: Unload content, textures, audio, maps, AAAAAAAAAAAAAAAAAAAAAAA

            lock (Modules)
                Modules.Remove(module);

            Logger.Log("FEZMod", $"Module {module.Metadata} unregistered.");
        }

    }
}
