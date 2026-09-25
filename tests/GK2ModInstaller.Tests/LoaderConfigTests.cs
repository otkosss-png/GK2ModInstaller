using System;
using System.Collections.Generic;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class LoaderConfigTests
    {
        private static string TmpFile() => Path.Combine(Path.GetTempPath(), "gk2cfg_" + Guid.NewGuid().ToString("N") + ".txt");

        [Fact]
        public void Missing_file_is_created_with_auto_and_uses_system_language()
        {
            string path = TmpFile();
            try
            {
                var result = LoaderConfig.Resolve(path, systemIsRussian: true, null);
                Assert.Equal(LoaderLanguage.Ru, result);
                Assert.True(File.Exists(path));
                Assert.Contains("language = auto", File.ReadAllText(path));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Missing_file_uses_english_for_non_russian_system()
        {
            string path = TmpFile();
            try
            {
                var result = LoaderConfig.Resolve(path, systemIsRussian: false, null);
                Assert.Equal(LoaderLanguage.En, result);
                Assert.True(File.Exists(path));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Language_ru_forces_russian()
        {
            string path = TmpFile();
            try
            {
                File.WriteAllText(path, "language = ru\n");
                Assert.Equal(LoaderLanguage.Ru, LoaderConfig.Resolve(path, systemIsRussian: false, log: null));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Language_EN_is_case_insensitive_and_forces_english()
        {
            string path = TmpFile();
            try
            {
                File.WriteAllText(path, "language = EN\n");
                Assert.Equal(LoaderLanguage.En, LoaderConfig.Resolve(path, systemIsRussian: true, log: null));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Unknown_value_falls_back_to_auto_and_logs()
        {
            string path = TmpFile();
            try
            {
                File.WriteAllText(path, "language = klingon\n");
                var logs = new List<string>();
                Assert.Equal(LoaderLanguage.Ru, LoaderConfig.Resolve(path, systemIsRussian: true, log: logs.Add));
                Assert.Contains(logs, l => l.Contains("klingon"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Garbage_file_never_throws_and_falls_back_to_auto()
        {
            string path = TmpFile();
            try
            {
                File.WriteAllBytes(path, new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0x80, 0x90 });
                Assert.Equal(LoaderLanguage.Ru, LoaderConfig.Resolve(path, systemIsRussian: true, log: null));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
