using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class GameFolderInstallerTests
    {
        private static string NewDir(string prefix)
        {
            var d = Path.Combine(Path.GetTempPath(), prefix + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        private static void Del(string d)
        {
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }

        [Fact]
        public void Install_copies_nested_data_files_and_skips_dlls()
        {
            string src = NewDir("gk2gfsrc");
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                Directory.CreateDirectory(Path.Combine(src, "GraveyardKeeper2_Data", "Managed"));
                File.WriteAllText(Path.Combine(src, "GraveyardKeeper2_Data", "Managed", "Mod.dll"), "DLL");
                File.WriteAllText(Path.Combine(src, "GraveyardKeeper2_Data", "Managed", "data.bin"), "DATA");
                Directory.CreateDirectory(Path.Combine(src, "Languages", "l"));
                File.WriteAllText(Path.Combine(src, "Languages", "l", "language.json"), "{}");

                int n = GameFolderInstaller.Install(src, game, backup, null);

                Assert.Equal(2, n);
                Assert.False(File.Exists(Path.Combine(game, "GraveyardKeeper2_Data", "Managed", "Mod.dll")));
                Assert.Equal("DATA", File.ReadAllText(Path.Combine(game, "GraveyardKeeper2_Data", "Managed", "data.bin")));
                Assert.Equal("{}", File.ReadAllText(Path.Combine(game, "Languages", "l", "language.json")));
            }
            finally { Del(src); Del(game); Del(backupParent); }
        }

        [Fact]
        public void Install_does_not_copy_dlls_from_any_folder()
        {
            string src = NewDir("gk2gfsrc");
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                File.WriteAllText(Path.Combine(src, "a.dll"), "A");
                Directory.CreateDirectory(Path.Combine(src, "sub"));
                File.WriteAllText(Path.Combine(src, "sub", "b.dll"), "B");
                File.WriteAllText(Path.Combine(src, "keep.txt"), "KEEP");

                int n = GameFolderInstaller.Install(src, game, backup, null);

                Assert.Equal(1, n);
                Assert.False(File.Exists(Path.Combine(game, "a.dll")));
                Assert.False(File.Exists(Path.Combine(game, "sub", "b.dll")));
                Assert.True(File.Exists(Path.Combine(game, "keep.txt")));
            }
            finally { Del(src); Del(game); Del(backupParent); }
        }

        [Fact]
        public void Existing_target_is_backed_up_and_manifest_records_1()
        {
            string src = NewDir("gk2gfsrc");
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                File.WriteAllText(Path.Combine(src, "file.txt"), "NEW");
                File.WriteAllText(Path.Combine(game, "file.txt"), "ORIG");

                int n = GameFolderInstaller.Install(src, game, backup, null);

                Assert.Equal(1, n);
                Assert.Equal("ORIG", File.ReadAllText(Path.Combine(backup, "files", "file.txt")));
                Assert.Contains("file.txt|1", File.ReadAllText(Path.Combine(backup, "manifest.txt")));
                Assert.Equal("NEW", File.ReadAllText(Path.Combine(game, "file.txt")));
            }
            finally { Del(src); Del(game); Del(backupParent); }
        }

        [Fact]
        public void New_file_records_0_and_has_no_backup_copy()
        {
            string src = NewDir("gk2gfsrc");
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                File.WriteAllText(Path.Combine(src, "new.txt"), "NEW");

                int n = GameFolderInstaller.Install(src, game, backup, null);

                Assert.Equal(1, n);
                Assert.Contains("new.txt|0", File.ReadAllText(Path.Combine(backup, "manifest.txt")));
                Assert.False(File.Exists(Path.Combine(backup, "files", "new.txt")));
                Assert.Equal("NEW", File.ReadAllText(Path.Combine(game, "new.txt")));
            }
            finally { Del(src); Del(game); Del(backupParent); }
        }

        [Fact]
        public void Root_readme_is_not_copied_but_nested_readme_is()
        {
            string src = NewDir("gk2gfsrc");
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                File.WriteAllText(Path.Combine(src, "README.txt"), "readme");
                File.WriteAllText(Path.Combine(src, "mod.txt"), "m");
                Directory.CreateDirectory(Path.Combine(src, "Sub"));
                File.WriteAllText(Path.Combine(src, "Sub", "README.txt"), "nested");

                int n = GameFolderInstaller.Install(src, game, backup, null);

                Assert.Equal(2, n);
                Assert.False(File.Exists(Path.Combine(game, "README.txt")));
                Assert.True(File.Exists(Path.Combine(game, "mod.txt")));
                Assert.True(File.Exists(Path.Combine(game, "Sub", "README.txt")));
            }
            finally { Del(src); Del(game); Del(backupParent); }
        }

        [Fact]
        public void Restore_deletes_created_files_restores_overwritten_removes_empty_dirs_and_backup()
        {
            string src = NewDir("gk2gfsrc");
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                Directory.CreateDirectory(Path.Combine(src, "newdir"));
                File.WriteAllText(Path.Combine(src, "newdir", "created.txt"), "NEW");
                File.WriteAllText(Path.Combine(src, "existing.txt"), "NEW");
                File.WriteAllText(Path.Combine(game, "existing.txt"), "ORIG");
                Directory.CreateDirectory(Path.Combine(game, "newdir"));
                File.WriteAllText(Path.Combine(game, "newdir", "keep.txt"), "KEEP");

                GameFolderInstaller.Install(src, game, backup, null);
                Assert.Equal("NEW", File.ReadAllText(Path.Combine(game, "newdir", "created.txt")));

                int n = GameFolderInstaller.Restore(game, backup, null);

                Assert.Equal(2, n);
                Assert.Equal("ORIG", File.ReadAllText(Path.Combine(game, "existing.txt")));
                Assert.False(File.Exists(Path.Combine(game, "newdir", "created.txt")));
                Assert.True(File.Exists(Path.Combine(game, "newdir", "keep.txt")));
                Assert.True(Directory.Exists(Path.Combine(game, "newdir")));
                Assert.False(Directory.Exists(backup));
            }
            finally { Del(src); Del(game); Del(backupParent); }
        }

        [Fact]
        public void Install_after_restore_keeps_original_file_in_backup()
        {
            string src1 = NewDir("gk2gfsrc1");
            string src2 = NewDir("gk2gfsrc2");
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                File.WriteAllText(Path.Combine(src1, "existing.txt"), "V1");
                File.WriteAllText(Path.Combine(src2, "existing.txt"), "V2");
                File.WriteAllText(Path.Combine(game, "existing.txt"), "ORIG");

                GameFolderInstaller.Install(src1, game, backup, null);
                Assert.Equal("V1", File.ReadAllText(Path.Combine(game, "existing.txt")));
                Assert.Equal("ORIG", File.ReadAllText(Path.Combine(backup, "files", "existing.txt")));

                GameFolderInstaller.Restore(game, backup, null);
                Assert.Equal("ORIG", File.ReadAllText(Path.Combine(game, "existing.txt")));

                GameFolderInstaller.Install(src2, game, backup, null);

                Assert.Equal("V2", File.ReadAllText(Path.Combine(game, "existing.txt")));
                Assert.Equal("ORIG", File.ReadAllText(Path.Combine(backup, "files", "existing.txt")));
            }
            finally { Del(src1); Del(src2); Del(game); Del(backupParent); }
        }

        [Fact]
        public void Install_reports_failed_count_when_a_file_cannot_be_copied()
        {
            string src = NewDir("gk2gfsrc");
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                File.WriteAllText(Path.Combine(src, "data.bin"), "DATA");
                // Цель — каталог: File.Copy на Windows детерминированно падает.
                Directory.CreateDirectory(Path.Combine(game, "data.bin"));

                int n = GameFolderInstaller.Install(src, game, backup, null, out int failed);

                Assert.Equal(0, n);
                Assert.Equal(1, failed);
            }
            finally { Del(src); Del(game); Del(backupParent); }
        }

        [Fact]
        public void Install_reports_failure_for_missing_source()
        {
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            try
            {
                int n = GameFolderInstaller.Install(Path.Combine(game, "nope"), game, Path.Combine(backupParent, "item"), null, out int failed);
                Assert.Equal(0, n);
                Assert.Equal(1, failed);
            }
            finally { Del(game); Del(backupParent); }
        }

        [Fact]
        public void Restore_reports_failed_count_and_zero_after_retry()
        {
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                Directory.CreateDirectory(Path.Combine(backup, "files"));
                File.WriteAllText(Path.Combine(backup, "files", "blocked.txt"), "ORIG");
                File.WriteAllText(Path.Combine(backup, "manifest.txt"), "blocked.txt|1\n");
                Directory.CreateDirectory(Path.Combine(game, "blocked.txt"));

                int first = GameFolderInstaller.Restore(game, backup, null, out int failedFirst);
                Assert.Equal(0, first);
                Assert.Equal(1, failedFirst);

                Directory.Delete(Path.Combine(game, "blocked.txt"));
                int second = GameFolderInstaller.Restore(game, backup, null, out int failedSecond);
                Assert.Equal(1, second);
                Assert.Equal(0, failedSecond);
            }
            finally { Del(game); Del(backupParent); }
        }

        [Fact]
        public void Restore_on_missing_backup_is_a_noop_and_never_throws()
        {
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            try
            {
                int n = GameFolderInstaller.Restore(game, Path.Combine(backupParent, "missing"), null);
                Assert.Equal(0, n);
            }
            finally { Del(game); Del(backupParent); }
        }

        [Fact]
        public void Restore_keeps_backup_when_a_file_cannot_be_restored()
        {
            string game = NewDir("gk2gfgame");
            string backupParent = NewDir("gk2gfbak");
            string backup = Path.Combine(backupParent, "item");
            try
            {
                Directory.CreateDirectory(Path.Combine(backup, "files"));
                File.WriteAllText(Path.Combine(backup, "files", "blocked.txt"), "ORIG");
                File.WriteAllText(Path.Combine(backup, "manifest.txt"), "blocked.txt|1\n");
                // Цель — каталог: File.Copy(..., true) на Windows детерминированно падает,
                // имитируя заблокированный запущенной игрой файл.
                Directory.CreateDirectory(Path.Combine(game, "blocked.txt"));

                var logs = new System.Collections.Generic.List<string>();
                int first = GameFolderInstaller.Restore(game, backup, logs.Add);

                Assert.Equal(0, first);
                Assert.Contains(logs, l => l.Contains("не удалось откатить"));
                Assert.True(Directory.Exists(backup));
                Assert.True(File.Exists(Path.Combine(backup, "manifest.txt")));
                Assert.True(File.Exists(Path.Combine(backup, "files", "blocked.txt")));

                // Убираем помеху — повторный откат должен пройти и удалить бэкап.
                Directory.Delete(Path.Combine(game, "blocked.txt"));
                int second = GameFolderInstaller.Restore(game, backup, logs.Add);

                Assert.Equal(1, second);
                Assert.False(Directory.Exists(backup));
                Assert.Equal("ORIG", File.ReadAllText(Path.Combine(game, "blocked.txt")));
            }
            finally { Del(game); Del(backupParent); }
        }
    }
}
