using System;
using System.IO;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class WorkshopItemsTests
    {
        private static string Tmp() => Path.Combine(Path.GetTempPath(), "gk2items_" + Guid.NewGuid().ToString("N"));

        private static void MakeItem(string workshopRoot, string id, string dllName, string content, string cfgName)
        {
            var p = Path.Combine(workshopRoot, id, "BepInEx", "plugins");
            Directory.CreateDirectory(p);
            File.WriteAllBytes(Path.Combine(p, dllName), System.Text.Encoding.ASCII.GetBytes(content));
            if (cfgName != null)
            {
                var c = Path.Combine(workshopRoot, id, "BepInEx", "config");
                Directory.CreateDirectory(c);
                File.WriteAllText(Path.Combine(c, cfgName), "cfg");
            }
        }

        [Fact]
        public void Item_without_plugin_dll_is_skipped_silently()
        {
            string root = Tmp();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "100", "BepInEx", "Languages"));
                File.WriteAllText(Path.Combine(root, "100", "BepInEx", "Languages", "l.json"), "{}");
                var logs = new System.Collections.Generic.List<string>();
                var items = WorkshopItemsScanner.Scan(root, logs.Add);
                Assert.Empty(items);
                Assert.DoesNotContain(logs, l => l.Contains("100"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Broken_dll_is_tolerated_and_title_falls_back_to_file_name()
        {
            string root = Tmp();
            try
            {
                MakeItem(root, "200", "MyMod.dll", "не сборка", null);
                var logs = new System.Collections.Generic.List<string>();
                var items = WorkshopItemsScanner.Scan(root, logs.Add);
                var item = Assert.Single(items);
                Assert.Equal("200", item.Id);
                Assert.Equal("MyMod", item.Title);
                Assert.Equal("", item.Version);
                Assert.Equal(new[] { "MyMod" }, item.AssemblyNames.ToArray());
                Assert.Single(item.DllFiles);
                Assert.Contains(logs, l => l.Contains("метаданные"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Real_assembly_metadata_is_read()
        {
            string dll = typeof(GK2ModInstaller.Core.TrustStore).Assembly.Location;
            var info = AssemblyInfoReader.TryRead(dll, null);
            Assert.Equal("GK2ModInstaller.Core", info.AssemblyName);
            Assert.False(string.IsNullOrEmpty(info.Version));
        }

        [Fact]
        public void Items_are_sorted_by_id_and_configs_are_not_part_of_dll_list()
        {
            string root = Tmp();
            try
            {
                MakeItem(root, "300", "A.dll", "x", "mod.cfg");
                MakeItem(root, "150", "B.dll", "y", null);
                var items = WorkshopItemsScanner.Scan(root, null);
                Assert.Equal(new[] { "150", "300" }, items.Select(i => i.Id).ToArray());
                Assert.All(items, i => Assert.All(i.DllFiles, f => Assert.EndsWith(".dll", f)));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Missing_root_gives_empty_list()
        {
            Assert.Empty(WorkshopItemsScanner.Scan(Path.Combine(Path.GetTempPath(), "gk2none_" + Guid.NewGuid().ToString("N")), null));
            Assert.Empty(WorkshopItemsScanner.Scan(null, null));
        }

        [Fact]
        public void CopyToGameFolder_layout_is_detected_as_game_folder_item()
        {
            string root = Tmp();
            try
            {
                var itemDir = Path.Combine(root, "3807460866");
                var gfx = Path.Combine(itemDir, "CopyToGameFolder");
                var managed = Path.Combine(gfx, "GraveyardKeeper2_Data", "Managed");
                Directory.CreateDirectory(managed);
                File.WriteAllBytes(Path.Combine(managed, "GK2RecipePin.dll"), System.Text.Encoding.ASCII.GetBytes("не сборка"));
                File.WriteAllBytes(Path.Combine(managed, "A_Harmony.dll"), System.Text.Encoding.ASCII.GetBytes("не сборка"));
                var lang = Path.Combine(gfx, "Languages", "gk2recipepin");
                Directory.CreateDirectory(lang);
                File.WriteAllText(Path.Combine(lang, "language.json"), "{}");
                File.WriteAllText(Path.Combine(itemDir, "README.txt"), "readme");

                var items = WorkshopItemsScanner.Scan(root, null);

                var item = Assert.Single(items);
                Assert.Equal("3807460866", item.Id);
                Assert.Equal(WorkshopItemKind.GameFolder, item.Kind);
                Assert.Equal(gfx, item.SourceDir);
                Assert.Null(item.PluginsDir);
                Assert.Equal(new[] { "A_Harmony", "GK2RecipePin" }, item.AssemblyNames.ToArray());
                Assert.Equal(2, item.DllFiles.Count);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Root_game_folder_layout_is_detected_as_game_folder_item()
        {
            string root = Tmp();
            try
            {
                var itemDir = Path.Combine(root, "3807460867");
                var managed = Path.Combine(itemDir, "GraveyardKeeper2_Data", "Managed");
                Directory.CreateDirectory(managed);
                File.WriteAllText(Path.Combine(managed, "Mod.dll"), "x");

                var items = WorkshopItemsScanner.Scan(root, null);

                var item = Assert.Single(items);
                Assert.Equal(WorkshopItemKind.GameFolder, item.Kind);
                Assert.Equal(itemDir, item.SourceDir);
                Assert.Null(item.PluginsDir);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Item_with_no_recognised_layout_is_skipped_silently()
        {
            string root = Tmp();
            try
            {
                var itemDir = Path.Combine(root, "500");
                Directory.CreateDirectory(itemDir);
                File.WriteAllText(Path.Combine(itemDir, "notes.txt"), "hi");
                File.WriteAllText(Path.Combine(itemDir, "mod.zip"), "zip");
                var logs = new System.Collections.Generic.List<string>();

                var items = WorkshopItemsScanner.Scan(root, logs.Add);

                Assert.Empty(items);
                Assert.DoesNotContain(logs, l => l.Contains("500"));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
