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
                    BepInExInstaller.Install(dir, bep, fw, false, null);
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
    }
}
