using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FezEngine.Mod {
    public abstract class ModAsset {

        public ModAssetSource Source;

        public Type Type = null;
        public string Format = null;

        public string PathVirtual;

        public List<ModAsset> Children = new List<ModAsset>();

        public List<object> Targets = new List<object>();

        public virtual bool HasData => true;

        public virtual byte[] Data {
            get {
                using (Stream stream = Open()) {
                    try {
                        if (stream is MemoryStream ms)
                            return ms.GetBuffer();
                    } catch {
                    }

                    using (MemoryStream ms = new MemoryStream()) {
                        byte[] buffer = new byte[2048];
                        int read;
                        while (0 < (read = stream.Read(buffer, 0, buffer.Length))) {
                            ms.Write(buffer, 0, read);
                        }
                        return ms.ToArray();
                    }
                }
            }
        }

        protected ModAsset(ModAssetSource source) {
            Source = source;
        }

        public abstract Stream Open();

        public bool TryDeserialize<T>(out T result) {
            if (Type == typeof(AssetTypeYaml)) {
                try {
                    using (StreamReader reader = new StreamReader(Open()))
                        result = YamlHelper.Deserializer.Deserialize<T>(reader);
                } catch {
                    result = default;
                    return false;
                }
                return true;
            }

            // TODO: Deserialize AssetTypeXml

            result = default;
            return false;
        }

        public T Deserialize<T>() {
            TryDeserialize(out T result);
            return result;
        }

        public bool TryGetMeta<T>(out T meta) {
            meta = default;
            if (ModContent.TryGet(PathVirtual + ".meta", out ModAsset metaAsset) &&
                metaAsset.TryDeserialize(out meta)
            )
                return true;
            return false;
        }

        public T GetMeta<T>() {
            TryGetMeta(out T meta);
            return meta;
        }

    }

    public abstract class ModAsset<T> : ModAsset where T : ModAssetSource {
        public new T Source => base.Source as T;
        protected ModAsset(T source)
            : base(source) {
        }
    }

    public sealed class ModAssetBranch : ModAsset {
        public override bool HasData => false;

        public ModAssetBranch()
            : base(null) {
        }

        public override Stream Open() {
            return null;
        }
    }

    public class FileSystemModAsset : ModAsset<FileSystemModAssetSource> {
        public readonly string Path;

        public FileSystemModAsset(FileSystemModAssetSource source, string path)
            : base(source) {
            Path = path;
        }

        public override Stream Open() {
            return File.Exists(Path) ? File.OpenRead(Path) : null;
        }
    }

    public class AssemblyModAsset : ModAsset<AssemblyModAssetSource> {
        public readonly string ResourceName;

        public AssemblyModAsset(AssemblyModAssetSource source, string resourceName)
            : base(source) {
            ResourceName = resourceName;
        }

        public override Stream Open() {
            return Source.Assembly.GetManifestResourceStream(ResourceName);
        }
    }

    public class ZipModAsset : ModAsset<ZipModAssetSource> {
        public readonly string Path;
        public readonly ZipEntry Entry;

        public ZipModAsset(ZipModAssetSource source, string path)
            : base(source) {
            Path = path = path.Replace('\\', '/');

            foreach (ZipEntry entry in source.Zip.Entries) {
                if (entry.FileName.Replace('\\', '/') == path) {
                    Entry = entry;
                    break;
                }
            }
        }

        public ZipModAsset(ZipModAssetSource source, ZipEntry entry)
            : base(source) {
            Path = entry.FileName.Replace('\\', '/');
            Entry = entry;
        }

        public override Stream Open() {
            string path = Path;

            ZipEntry found = Entry;
            if (found == null) {
                foreach (ZipEntry entry in Source.Zip.Entries) {
                    if (entry.FileName.Replace('\\', '/') == path) {
                        return entry.ExtractStream();
                    }
                }
            }

            if (found == null)
                throw new KeyNotFoundException($"{GetType().Name} {Path} not found in archive {Source.Path}");

            return Entry.ExtractStream();
        }
    }

    public class MemoryAsset : ModAsset {
        public byte[] _Data;
        public override byte[] Data => _Data;

        public MemoryAsset(ModAssetSource source, byte[] data)
            : base(source) {
            _Data = data;
        }

        public override Stream Open() {
            return new MemoryStream(_Data, 0, _Data.Length, false, true);
        }
    }

    public class PackedAsset : ModAsset<PackedAssetSource> {
        public readonly long Position;
        public readonly int Length;

        private byte[] CachedData;
        public override byte[] Data => CachedData ?? base.Data;

        public PackedAsset(PackedAssetSource source, long position, int length)
            : base(source) {
            Position = position;
            Length = length;
        }

        public void Precache() {
            if (CachedData != null)
                return;
            using (LimitedStream stream = Open() as LimitedStream)
                CachedData = stream?.GetBuffer();
        }

        public override Stream Open() {
            if (CachedData != null)
                return new MemoryStream(CachedData, 0, CachedData.Length, false, true);
            return new LimitedStream(File.OpenRead(Source.Path), Position, Length);
        }
    }
}
