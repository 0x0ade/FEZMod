#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using FezEngine.Mod;
using FezEngine.Mod.Core;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FezEngine.Tools {
    class patch_MemoryContentManager : MemoryContentManager {

        private static List<string> assetNames;
        private static int assetNamesCachedCount;

        public static new IEnumerable<string> AssetNames {
            [MonoModReplace]
            get {
                if (assetNames != null &&
                    assetNamesCachedCount == ModContent.Map.Count)
                    return assetNames;

                assetNamesCachedCount = ModContent.Map.Count;
                assetNames = ModContent.Map.Values
                    .Where(asset => asset.HasData)
                    .Select(asset => asset.PathVirtual.Replace('/', '\\')) // Game expects \ over /
                    .Distinct()
                    .ToList();

                return assetNames;
            }
        }

        public patch_MemoryContentManager(IServiceProvider serviceProvider, string rootDirectory)
            : base(serviceProvider, rootDirectory) {
            // no-op.
        }

        [MonoModReplace]
        protected override Stream OpenStream(string assetName) {
            if (ModContent.TryGet(assetName, out ModAsset modAsset))
                return modAsset.Open();

            throw new FileNotFoundException($"Asset not found: {assetName}");
        }

        [MonoModReplace]
        public static new bool AssetExists(string assetName) {
            assetName = assetName.ToLowerInvariant().Replace('\\', '/');

            if (ModContent.Get(assetName) != null)
                return true;

            return false;
        }

        [MonoModReplace]
        public new void LoadEssentials() {
            ModContent.Crawl(new PackedAssetSource(Path.Combine(RootDirectory, "Essentials.pak"), true) {
                ID = "FEZ.Essentials"
            }, false);
        }

        [MonoModReplace]
        public new void Preload() {
            ModContent.Crawl(new PackedAssetSource(Path.Combine(RootDirectory, "Updates.pak"), true) {
                ID = "FEZ.Updates"
            }, false);

            // FEZ originally precaches Other.pak - let's just scan it instead.
            ModContent.Crawl(new PackedAssetSource(Path.Combine(RootDirectory, "Other.pak"), false) {
                ID = "FEZ.Other"
            }, false);
        }

    }
}
