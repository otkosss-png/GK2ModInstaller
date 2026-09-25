using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class GameFolderDllLoaderTests
    {
        public GameFolderDllLoaderTests()
        {
            LoaderText.Language = LoaderLanguage.En;
        }

        private static string NewDir()
        {
            var d = Path.Combine(Path.GetTempPath(), "gk2dll_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        // Ищем настоящую .NET-сборку на диске, которая ещё не загружена в тест-процесс.
        // Так мы проверяем именно ветку Assembly.Load, не выдумывая сборку.
        private static string FindUnloadedAssemblyFile()
        {
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { loaded.Add(a.GetName().Name); } catch { }
            }
            var dir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (string.IsNullOrEmpty(dir)) return null;
            foreach (var f in Directory.GetFiles(dir, "*.dll").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var name = AssemblyName.GetAssemblyName(f).Name;
                    if (!loaded.Contains(name)) return f;
                }
                catch { }
            }
            return null;
        }

        [Fact]
        public void Load_loads_a_not_yet_loaded_assembly_from_bytes()
        {
            string path = FindUnloadedAssemblyFile();
            if (path == null) return; // нечего грузить — не среда с полным фреймворком

            var logs = new List<string>();
            int n = GameFolderDllLoader.Load(new[] { path }, logs.Add);

            Assert.Equal(1, n);
            Assert.Contains(logs, l => l.Contains("loaded"));
        }

        [Fact]
        public void Load_skips_an_already_loaded_assembly()
        {
            var logs = new List<string>();
            int n = GameFolderDllLoader.Load(new[] { typeof(GameFolderDllLoaderTests).Assembly.Location }, logs.Add);

            Assert.Equal(0, n);
            Assert.Contains(logs, l => l.Contains("already"));
        }

        [Fact]
        public void Load_skips_missing_invalid_and_empty_paths_without_throwing()
        {
            string dir = NewDir();
            try
            {
                string invalid = Path.Combine(dir, "bad.dll");
                File.WriteAllText(invalid, "not an assembly");
                string missing = Path.Combine(dir, "missing.dll");

                var logs = new List<string>();
                int n = GameFolderDllLoader.Load(new[] { missing, invalid, null, "" }, logs.Add);

                Assert.Equal(0, n);
                Assert.Contains(logs, l => l.Contains("not loaded") && l.Contains("bad.dll"));
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public void Load_tolerates_null_and_empty_input()
        {
            Assert.Equal(0, GameFolderDllLoader.Load(null, null));
            Assert.Equal(0, GameFolderDllLoader.Load(new string[0], null));
        }
    }
}
