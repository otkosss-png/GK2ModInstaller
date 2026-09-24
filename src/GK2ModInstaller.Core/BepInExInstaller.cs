using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GK2ModInstaller.Core
{
    public static class BepInExInstaller
    {
        public const string BepInExDirName = "BepInEx";
        public static readonly string[] BepInExRootFiles =
        {
            "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt"
        };

        public static bool IsGameRunning()
        {
            try { return Process.GetProcessesByName("GraveyardKeeper2").Length > 0; }
            catch { return false; }
        }

        public static IReadOnlyList<string> Verify(string gameDir)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            {
                problems.Add("Папка игры не найдена.");
                return problems;
            }
            if (!File.Exists(Path.Combine(gameDir, "winhttp.dll"))) problems.Add("нет winhttp.dll");
            if (!File.Exists(Path.Combine(gameDir, "doorstop_config.ini"))) problems.Add("нет doorstop_config.ini");
            var core = Path.Combine(gameDir, BepInExDirName, "core");
            if (!Directory.Exists(core) || Directory.GetFiles(core).Length == 0) problems.Add("пусто BepInEx/core");
            if (!File.Exists(Path.Combine(gameDir, BepInExDirName, "plugins", "GK2.Framework.dll")))
                problems.Add("нет GK2.Framework.dll");
            return problems;
        }

        public static void Install(string gameDir, Stream bepinexZip, Stream frameworkZip, bool backupExisting, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
                throw new DirectoryNotFoundException(gameDir);

            if (backupExisting)
            {
                var bep = Path.Combine(gameDir, BepInExDirName);
                if (Directory.Exists(bep))
                {
                    var bak = Path.Combine(gameDir, BepInExDirName + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    log?.Invoke("Бэкап BepInEx -> " + Path.GetFileName(bak));
                    CopyDir(bep, bak);
                }
            }

            if (bepinexZip != null)
            {
                log?.Invoke("Распаковка BepInEx…");
                ZipExtractor.ExtractTo(bepinexZip, gameDir);
            }
            if (frameworkZip != null)
            {
                log?.Invoke("Распаковка GK2 Mod Framework…");
                ZipExtractor.ExtractTo(frameworkZip, gameDir);
            }
        }

        public static void Uninstall(string gameDir, Action<string> log)
        {
            var bep = Path.Combine(gameDir, BepInExDirName);
            if (Directory.Exists(bep)) { Directory.Delete(bep, true); log?.Invoke("Удалено: BepInEx\\"); }
            foreach (var f in BepInExRootFiles)
            {
                var p = Path.Combine(gameDir, f);
                if (File.Exists(p)) { File.Delete(p); log?.Invoke("Удалено: " + f); }
            }
        }

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, dir.Substring(src.Length).TrimStart('\\')));
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(dst, file.Substring(src.Length).TrimStart('\\')), true);
        }
    }
}
