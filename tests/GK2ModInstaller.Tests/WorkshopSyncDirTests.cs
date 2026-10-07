using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class WorkshopSyncDirTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "gk2sync_" + Guid.NewGuid().ToString("N"));
        private string Src => Path.Combine(_root, "src");
        private string Dst => Path.Combine(_root, "dst");

        public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

        private static void Put(string dir, string rel, string text = "x")
        {
            var p = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, text);
        }

        [Fact]
        public void KeepsFilesCreatedByModOrPlayer()
        {
            Put(Src, @"Mod\Mod.dll");
            WorkshopSync.SyncDir(Src, Dst);
            Put(Dst, @"Mod\Localization\es.json", "player");
            Put(Src, @"Mod\Mod.dll", "v2"); // обновление мода
            WorkshopSync.SyncDir(Src, Dst);
            Assert.Equal("player", File.ReadAllText(Path.Combine(Dst, @"Mod\Localization\es.json")));
            Assert.Equal("v2", File.ReadAllText(Path.Combine(Dst, @"Mod\Mod.dll")));
        }

        [Fact]
        public void RemovesFilesThatLeftTheItem()
        {
            Put(Src, @"Mod\Old.dll");
            Put(Src, @"Mod\Localization\de.json");
            WorkshopSync.SyncDir(Src, Dst);
            File.Delete(Path.Combine(Src, @"Mod\Old.dll"));
            File.Delete(Path.Combine(Src, @"Mod\Localization\de.json"));
            Put(Src, @"Mod\New.dll");
            WorkshopSync.SyncDir(Src, Dst);
            Assert.False(File.Exists(Path.Combine(Dst, @"Mod\Old.dll")));
            Assert.False(File.Exists(Path.Combine(Dst, @"Mod\Localization\de.json")));
            Assert.True(File.Exists(Path.Combine(Dst, @"Mod\New.dll")));
        }

        [Fact]
        public void WithoutManifestRemovesOnlyForeignDlls()
        {
            Put(Dst, @"Mod\Stale.dll");
            Put(Dst, @"Mod\Localization\es.json", "player");
            Put(Src, @"Mod\Mod.dll");
            WorkshopSync.SyncDir(Src, Dst);
            Assert.False(File.Exists(Path.Combine(Dst, @"Mod\Stale.dll")));
            Assert.True(File.Exists(Path.Combine(Dst, @"Mod\Localization\es.json")));
            Assert.True(File.Exists(Path.Combine(Dst, WorkshopSync.ManifestName)));
        }
    }
}
