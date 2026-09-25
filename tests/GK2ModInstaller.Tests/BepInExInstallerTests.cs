using System;
using System.IO;
using System.IO.Compression;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class BepInExInstallerTests
    {
        private static MemoryStream Zip(params (string name, string content)[] files)
        {
            var ms = new MemoryStream();
            using (var a = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var f in files)
                {
                    var e = a.CreateEntry(f.name);
                    using (var s = e.Open()) using (var w = new StreamWriter(s)) w.Write(f.content);
                }
            ms.Position = 0;
            return ms;
        }

        [Fact]
        public void Extract_writes_nested_files()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2zip_" + Guid.NewGuid().ToString("N"));
            try
            {
                ZipExtractor.ExtractTo(Zip(("a.txt", "A"), ("sub/b.txt", "B")), dir);
                Assert.Equal("A", File.ReadAllText(Path.Combine(dir, "a.txt")));
                Assert.Equal("B", File.ReadAllText(Path.Combine(dir, "sub", "b.txt")));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Verify_reports_missing_on_empty_dir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2ver_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try { Assert.NotEmpty(BepInExInstaller.Verify(dir)); }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Install_then_verify_is_clean()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2ins_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                using (var bep = Zip(("winhttp.dll", "x"), ("doorstop_config.ini", "x"), ("BepInEx/core/BepInEx.dll", "x")))
                using (var fw = Zip(("BepInEx/plugins/GK2.Framework.dll", "x")))
                    BepInExInstaller.Install(dir, bep, fw, null, null, false, null);
                Assert.Empty(BepInExInstaller.Verify(dir));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Uninstall_removes_only_bepinex_files()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2un_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "BepInEx", "core"));
            File.WriteAllText(Path.Combine(dir, "BepInEx", "core", "BepInEx.dll"), "x");
            File.WriteAllText(Path.Combine(dir, "winhttp.dll"), "x");
            File.WriteAllText(Path.Combine(dir, "doorstop_config.ini"), "x");
            File.WriteAllText(Path.Combine(dir, "GameFile.txt"), "keep");
            try
            {
                BepInExInstaller.Uninstall(dir, null);
                Assert.False(Directory.Exists(Path.Combine(dir, "BepInEx")));
                Assert.False(File.Exists(Path.Combine(dir, "winhttp.dll")));
                Assert.False(File.Exists(Path.Combine(dir, "doorstop_config.ini")));
                Assert.True(File.Exists(Path.Combine(dir, "GameFile.txt")));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Install_writes_loader_fallback_next_to_bepinex()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2inst_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var bep = Zip(("BepInEx/core/BepInEx.dll", "x"), ("winhttp.dll", "x"));
                var fw = Zip(("BepInEx/plugins/GK2.Framework/GK2.Framework.dll", "x"));
                using (var patcher = new MemoryStream(new byte[] { 1 }))
                using (var loader = new MemoryStream(new byte[] { 2, 3 }))
                    BepInExInstaller.Install(dir, bep, fw, patcher, loader, false, null);

                var loaderPath = Path.Combine(dir, "BepInEx", "GK2.WorkshopLoader.dll");
                Assert.True(File.Exists(loaderPath));
                Assert.Equal(new byte[] { 2, 3 }, File.ReadAllBytes(loaderPath));
                Assert.True(File.Exists(Path.Combine(dir, "BepInEx", "patchers", "GK2.WorkshopAutoLoader.dll")));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Verify_reports_missing_loader_fallback()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2inst_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "BepInEx", "patchers"));
                Directory.CreateDirectory(Path.Combine(dir, "BepInEx", "core", "BepInEx"));
                File.WriteAllText(Path.Combine(dir, "BepInEx", "patchers", "GK2.WorkshopAutoLoader.dll"), "x");
                File.WriteAllText(Path.Combine(dir, "winhttp.dll"), "x");
                File.WriteAllText(Path.Combine(dir, "BepInEx", "core", "BepInEx.dll"), "x");
                var problems = BepInExInstaller.Verify(dir, expectPatcher: true);
                Assert.Contains(problems, p => p.Contains("GK2.WorkshopLoader.dll"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Uninstall_removes_loader_fallback()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2inst_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "BepInEx", "patchers"));
                File.WriteAllText(Path.Combine(dir, "BepInEx", "patchers", "GK2.WorkshopAutoLoader.dll"), "x");
                File.WriteAllText(Path.Combine(dir, "BepInEx", "GK2.WorkshopLoader.dll"), "x");
                BepInExInstaller.Uninstall(dir, null);
                Assert.False(File.Exists(Path.Combine(dir, "BepInEx", "GK2.WorkshopLoader.dll")));
                Assert.False(File.Exists(Path.Combine(dir, "BepInEx", "patchers", "GK2.WorkshopAutoLoader.dll")));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void ApplyPendingGameActions_deletes_and_restores_and_clears()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2pg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var config = Path.Combine(dir, "BepInEx", "config");
                Directory.CreateDirectory(config);

                var legacy = Path.Combine(dir, "GraveyardKeeper2_Data", "Managed", "Old.dll");
                Directory.CreateDirectory(Path.GetDirectoryName(legacy));
                File.WriteAllText(legacy, "LEGACY");

                var backupFile = Path.Combine(config, "GK2_WorkshopLoader.backup", "9", "files", "Languages", "l", "language.json");
                Directory.CreateDirectory(Path.GetDirectoryName(backupFile));
                File.WriteAllText(backupFile, "ORIG");
                var gameLang = Path.Combine(dir, "Languages", "l", "language.json");
                Directory.CreateDirectory(Path.GetDirectoryName(gameLang));
                File.WriteAllText(gameLang, "NEW");

                var pending = Path.Combine(config, PendingGameActions.FileName);
                PendingGameActions.Write(pending, new[]
                {
                    new PendingGameAction { Kind = PendingGameAction.DeleteKind, Rel = @"GraveyardKeeper2_Data\Managed\Old.dll" },
                    new PendingGameAction { Kind = PendingGameAction.RestoreKind, Rel = @"Languages\l\language.json", Id = "9" }
                }, null);

                int n = BepInExInstaller.ApplyPendingGameActions(dir, null);

                Assert.Equal(2, n);
                Assert.False(File.Exists(legacy));
                Assert.Equal("ORIG", File.ReadAllText(gameLang));
                Assert.False(File.Exists(pending));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void ApplyPendingGameActions_keeps_an_action_that_cannot_be_applied()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2pg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var config = Path.Combine(dir, "BepInEx", "config");
                Directory.CreateDirectory(config);
                // Цель — каталог: File.Delete на Windows детерминированно падает, имитируя
                // заблокированный запущенной игрой файл.
                Directory.CreateDirectory(Path.Combine(dir, "GraveyardKeeper2_Data", "Managed", "Locked.dll"));

                var pending = Path.Combine(config, PendingGameActions.FileName);
                PendingGameActions.Write(pending, new[]
                {
                    new PendingGameAction { Kind = PendingGameAction.DeleteKind, Rel = @"GraveyardKeeper2_Data\Managed\Locked.dll" }
                }, null);

                int n = BepInExInstaller.ApplyPendingGameActions(dir, null);

                Assert.Equal(0, n);
                Assert.True(Directory.Exists(Path.Combine(dir, "GraveyardKeeper2_Data", "Managed", "Locked.dll")));
                Assert.True(File.Exists(pending));
                Assert.Contains("Locked.dll", File.ReadAllText(pending));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
