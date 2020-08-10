using Common;
using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FezEngine.Mod {
    public abstract class ModAssetSource : IDisposable {

        public virtual string DefaultID { get; }
        private string _ID;
        public string ID {
            get => !string.IsNullOrEmpty(_ID) ? _ID : DefaultID;
            set => _ID = value;
        }

        public ModMetadata Mod;

        public readonly List<ModAsset> List = new List<ModAsset>();
        public readonly Dictionary<string, ModAsset> Map = new Dictionary<string, ModAsset>();

        protected abstract void Crawl(bool overwrite);
        internal void _Crawl(bool overwrite) => Crawl(overwrite);

        protected virtual void Add(string path, ModAsset asset, bool overwrite) {
            ModContent.Add(path, asset, overwrite);
            List.Add(asset);
            Map[asset.PathVirtual] = asset;
        }

        protected virtual void Update(string path, ModAsset next) {
            if (next == null) {
                Update(ModContent.Get<AssetTypeDirectory>(path), null);
                return;
            }

            next.PathVirtual = path;
            Update((ModAsset)null, next);
        }

        protected virtual void Update(ModAsset prev, ModAsset next) {
            if (prev != null) {
                int index = List.IndexOf(prev);

                if (next == null) {
                    Map.Remove(prev.PathVirtual);
                    if (index != -1)
                        List.RemoveAt(index);

                    ModContent.Update(prev, null);
                    foreach (ModAsset child in prev.Children.ToArray())
                        if (child.Source == this)
                            Update(child, null);

                } else {
                    Map[prev.PathVirtual] = next;
                    if (index != -1)
                        List[index] = next;
                    else
                        List.Add(next);

                    next.PathVirtual = prev.PathVirtual;
                    next.Type = prev.Type;
                    next.Format = prev.Format;

                    ModContent.Update(prev, next);
                    foreach (ModAsset child in prev.Children.ToArray())
                        if (child.Source == this)
                            Update(child, null);
                    foreach (ModAsset child in next.Children.ToArray())
                        if (child.Source == this)
                            Update((ModAsset)null, child);
                }

            } else if (next != null) {
                Map[next.PathVirtual] = next;
                List.Add(next);
                ModContent.Update(null, next);
                foreach (ModAsset child in next.Children.ToArray())
                    if (child.Source == this)
                        Update((ModAsset)null, child);
            }
        }

        private bool disposed = false;

        ~ModAssetSource() {
            if (disposed)
                return;
            disposed = true;

            Dispose(false);
        }

        public void Dispose() {
            if (disposed)
                return;
            disposed = true;

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
        }

    }

    public class FileSystemModAssetSource : ModAssetSource {
        public override string DefaultID => System.IO.Path.GetFileName(Path);

        public readonly string Path;

        private readonly Dictionary<string, FileSystemModAsset> FileSystemMap = new Dictionary<string, FileSystemModAsset>();

        private readonly FileSystemWatcher watcher;

        public FileSystemModAssetSource(string path) {
            Path = path;

            watcher = new FileSystemWatcher {
                Path = path,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true
            };

            watcher.Changed += FileUpdated;
            watcher.Created += FileUpdated;
            watcher.Deleted += FileUpdated;
            watcher.Renamed += FileRenamed;

            watcher.EnableRaisingEvents = true;
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            watcher.Dispose();
        }

        protected override void Crawl(bool overwrite) => Crawl(overwrite, null, Path, false);

        protected virtual void Crawl(bool overwrite, string dir, string root, bool update) {
            if (dir == null)
                dir = Path;
            if (root == null)
                root = Path;

            int lastIndexOfSlash = dir.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
            // Ignore hidden files and directories.
            if (lastIndexOfSlash != -1 &&
                lastIndexOfSlash >= root.Length && // Make sure to not skip crawling in hidden mods.
                dir.Length > lastIndexOfSlash + 1 &&
                dir[lastIndexOfSlash + 1] == '.') {
                // Logger.Log(LogLevel.Verbose, "content", $"Skipped crawling hidden file or directory {dir.Substring(root.Length + 1)}");
                return;
            }

            if (File.Exists(dir)) {
                string path = dir.Substring(root.Length + 1);
                ModAsset asset = new FileSystemModAsset(this, dir);

                if (update)
                    Update(path, asset);
                else
                    Add(path, asset, overwrite);
                return;
            }

            string[] files = Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                Crawl(overwrite, file, root, update);
            }

            files = Directory.GetDirectories(dir);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                Crawl(overwrite, file, root, update);
            }
        }

        protected override void Add(string path, ModAsset asset, bool overwrite) {
            FileSystemModAsset fsma = (FileSystemModAsset)asset;
            FileSystemMap[fsma.Path] = fsma;
            base.Add(path, asset, overwrite);
        }

        protected override void Update(string path, ModAsset next) {
            if (next is FileSystemModAsset fsma) {
                FileSystemMap[fsma.Path] = fsma;
            }
            base.Update(path, next);
        }

        protected override void Update(ModAsset prev, ModAsset next) {
            if (prev is FileSystemModAsset fsma) {
                FileSystemMap[fsma.Path] = null;
            }

            if ((fsma = next as FileSystemModAsset) != null) {
                FileSystemMap[fsma.Path] = fsma;

                // Make sure to wait until the file is readable.
                Stopwatch timer = Stopwatch.StartNew();
                while (File.Exists(fsma.Path)) {
                    try {
                        new FileStream(fsma.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete).Dispose();
                        break;
                    } catch (ThreadAbortException) {
                        throw;
                    } catch (ThreadInterruptedException) {
                        throw;
                    } catch {
                        // Retry, but not infinitely.
                        if (timer.Elapsed.TotalSeconds >= 2D)
                            throw;
                    }
                }
                timer.Stop();
            }

            base.Update(prev, next);
        }

        private void FileUpdated(object source, FileSystemEventArgs e) {
            // Directories will be "changed" as soon as their children change.
            if (e.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(e.FullPath))
                return;

            Logger.Log("FEZMod.Content", $"File updated: {e.FullPath} - {e.ChangeType}");
            QueuedTaskHelper.Do(e.FullPath, () => Update(e.FullPath, e.FullPath));
        }

        private void FileRenamed(object source, RenamedEventArgs e) {
            Logger.Log("FEZMod.Content", $"File renamed: {e.OldFullPath} - {e.FullPath}");
            QueuedTaskHelper.Do(Tuple.Create(e.OldFullPath, e.FullPath), () => Update(e.OldFullPath, e.FullPath));
        }

        private void Update(string pathPrev, string pathNext) {
            ModAsset prev;
            if (FileSystemMap.TryGetValue(pathPrev, out FileSystemModAsset prevFS))
                prev = prevFS;
            else
                prev = ModContent.Get<AssetTypeDirectory>(pathPrev.Substring(Path.Length + 1));

            if (File.Exists(pathNext)) {
                if (prev != null)
                    Update(prev, new FileSystemModAsset(this, pathNext));
                else
                    Update(pathNext.Substring(Path.Length + 1), new FileSystemModAsset(this, pathNext));

            } else if (Directory.Exists(pathNext)) {
                Update(prev, null);
                Crawl(true, pathNext, Path, true);

            } else if (prev != null) {
                Update(prev, null);

            } else {
                Update(pathPrev, (ModAsset)null);
            }
        }
    }

    public class AssemblyModAssetSource : ModAssetSource {
        public override string DefaultID => Assembly.GetName().Name;

        /// <summary>
        /// The assembly containing the mod content as resources.
        /// </summary>
        public readonly Assembly Assembly;

        public AssemblyModAssetSource(Assembly asm) {
            Assembly = asm;
        }

        protected override void Crawl(bool overwrite) {
            string[] resourceNames = Assembly.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; i++) {
                string name = resourceNames[i];
                int indexOfContent = name.IndexOf("Content");
                if (indexOfContent < 0)
                    continue;
                name = name.Substring(indexOfContent + 8);
                Add(name, new AssemblyModAsset(this, resourceNames[i]), overwrite);
            }
        }
    }

    public class ZipModAssetSource : ModAssetSource {
        public override string DefaultID => System.IO.Path.GetFileName(Path);

        public readonly string Path;

        public readonly ZipFile Zip;

        public ZipModAssetSource(string path) {
            Path = path;
            Zip = new ZipFile(path);
        }

        protected override void Crawl(bool overwrite) {
            foreach (ZipEntry entry in Zip.Entries) {
                string entryName = entry.FileName.Replace('\\', '/');
                if (entryName.EndsWith("/"))
                    continue;
                Add(entryName, new ZipModAsset(this, entry), overwrite);
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Zip.Dispose();
        }
    }

    public class PackedAssetSource : ModAssetSource {
        public override string DefaultID => System.IO.Path.GetFileName(Path);

        public readonly string Path;

        public readonly bool Precache;

        public PackedAssetSource(string path, bool precache) {
            Path = path;
            Precache = precache;
        }

        protected override void Crawl(bool overwrite) {
            using (FileStream stream = File.OpenRead(Path))
            using (BinaryReader reader = new BinaryReader(stream)) {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++) {
                    string path = reader.ReadString().ToLowerInvariant().Replace('\\', '/');
                    int length = reader.ReadInt32();
                    // Packs don't contain the file extensions.
                    // This affects SharedContentManager.ReadAsset and anything else that manually checks the asset type,
                    // but MemoryContentManager.OpenStream keeps working as it doesn't have any asset type checks.
                    if (Precache) {
                        Add(path, new MemoryAsset(this, reader.ReadBytes(length)), overwrite);
                    } else {
                        Add(path, new PackedAsset(this, stream.Position, length), overwrite);
                        stream.Seek(length, SeekOrigin.Current);
                    }
                }
            }
        }
    }
}
