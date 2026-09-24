using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class ModFingerprintTests
    {
        private static string Tmp() => Path.Combine(Path.GetTempPath(), "gk2fp_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void Same_content_gives_same_fingerprint_regardless_of_writes()
        {
            string dir = Tmp();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "sub"));
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAA");
                File.WriteAllText(Path.Combine(dir, "sub", "res.txt"), "R");
                string a = ModFingerprint.Compute(dir);
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAA");
                Assert.Equal(a, ModFingerprint.Compute(dir));
                Assert.Equal(64, a.Length);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Changed_dll_changes_fingerprint()
        {
            string dir = Tmp();
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAA");
                string a = ModFingerprint.Compute(dir);
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAB");
                Assert.NotEqual(a, ModFingerprint.Compute(dir));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Config_files_do_not_affect_fingerprint()
        {
            string dir = Tmp();
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAA");
                string a = ModFingerprint.Compute(dir);
                File.WriteAllText(Path.Combine(dir, "settings.cfg"), "user edited");
                Assert.Equal(a, ModFingerprint.Compute(dir));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Missing_directory_gives_empty_string()
        {
            Assert.Equal("", ModFingerprint.Compute(Path.Combine(Path.GetTempPath(), "gk2nope_" + Guid.NewGuid().ToString("N"))));
            Assert.Equal("", ModFingerprint.Compute(null));
        }
    }
}
