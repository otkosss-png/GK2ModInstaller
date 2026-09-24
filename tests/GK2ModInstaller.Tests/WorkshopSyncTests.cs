using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class WorkshopSyncTests
    {
        private static string Tmp() => Path.Combine(Path.GetTempPath(), "gk2sync_" + Guid.NewGuid().ToString("N"));

        private static void MakeItem(string workshopRoot, string id, string dllContent, string cfgName)
        {
            string p = Path.Combine(workshopRoot, id, "BepInEx", "plugins");
            Directory.CreateDirectory(p);
            File.WriteAllText(Path.Combine(p, "Mod.dll"), dllContent);
            if (cfgName != null)
            {
                string c = Path.Combine(workshopRoot, id, "BepInEx", "config");
                Directory.CreateDirectory(c);
                File.WriteAllText(Path.Combine(c, cfgName), "cfg");
            }
        }

        [Fact]
        public void Copies_plugins_into_workshop_staging()
        {
            string root = Tmp();
            string bep = Path.Combine(root, "BE");
            string ws = Path.Combine(root, "ws");
            Directory.CreateDirectory(bep); Directory.CreateDirectory(ws);
            MakeItem(ws, "111", "MOD111", "mod.cfg");
            try
            {
                WorkshopSync.Sync(ws, bep, null);
                Assert.True(File.Exists(Path.Combine(bep, "plugins", "_Workshop", "111", "Mod.dll")));
                Assert.True(File.Exists(Path.Combine(bep, "config", "mod.cfg")));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Does_not_overwrite_existing_config()
        {
            string root = Tmp();
            string bep = Path.Combine(root, "BE");
            string ws = Path.Combine(root, "ws");
            Directory.CreateDirectory(Path.Combine(bep, "config"));
            File.WriteAllText(Path.Combine(bep, "config", "mod.cfg"), "USER");
            Directory.CreateDirectory(ws);
            MakeItem(ws, "222", "x", "mod.cfg");
            try
            {
                WorkshopSync.Sync(ws, bep, null);
                Assert.Equal("USER", File.ReadAllText(Path.Combine(bep, "config", "mod.cfg")));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Removes_staging_for_unsubscribed_items()
        {
            string root = Tmp();
            string bep = Path.Combine(root, "BE");
            string ws = Path.Combine(root, "ws");
            Directory.CreateDirectory(Path.Combine(bep, "plugins", "_Workshop", "999"));
            File.WriteAllText(Path.Combine(bep, "plugins", "_Workshop", "999", "Old.dll"), "x");
            Directory.CreateDirectory(ws);
            try
            {
                WorkshopSync.Sync(ws, bep, null);
                Assert.False(Directory.Exists(Path.Combine(bep, "plugins", "_Workshop", "999")));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
