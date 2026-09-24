using System;
using System.Collections.Generic;
using System.IO;

namespace GK2ModInstaller.Core
{
    public static class WorkshopSync
    {
        public const string WorkshopPluginsDirName = "_Workshop";
        private static bool _ran;

        public static void RunOnce(string workshopRoot, string bepInExRoot, Action<string> log)
        {
            if (_ran) return;
            _ran = true;
            Sync(workshopRoot, bepInExRoot, log);
        }

        public static void Sync(string workshopRoot, string bepInExRoot, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(workshopRoot) || !Directory.Exists(workshopRoot))
            { log?.Invoke("Workshop root не найден: " + workshopRoot); return; }
            if (string.IsNullOrWhiteSpace(bepInExRoot) || !Directory.Exists(bepInExRoot))
            { log?.Invoke("BepInEx root не найден: " + bepInExRoot); return; }

            var pluginsDir = Path.Combine(bepInExRoot, "plugins");
            var configDir = Path.Combine(bepInExRoot, "config");
            var stagingRoot = Path.Combine(pluginsDir, WorkshopPluginsDirName);
            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(configDir);

            var present = new HashSet<string>();
            int items = 0, copied = 0;
            foreach (var itemDir in Directory.GetDirectories(workshopRoot))
            {
                var id = Path.GetFileName(itemDir);
                var srcPlugins = Path.Combine(itemDir, "BepInEx", "plugins");
                if (!Directory.Exists(srcPlugins)) continue;
                if (Directory.GetFiles(srcPlugins, "*.dll", SearchOption.AllDirectories).Length == 0) continue;

                present.Add(id);
                items++;
                copied += CopyDir(srcPlugins, Path.Combine(stagingRoot, id));

                var srcConfig = Path.Combine(itemDir, "BepInEx", "config");
                if (Directory.Exists(srcConfig))
                    foreach (var cfg in Directory.GetFiles(srcConfig, "*.cfg", SearchOption.AllDirectories))
                    {
                        var target = Path.Combine(configDir, Path.GetFileName(cfg));
                        if (!File.Exists(target)) { File.Copy(cfg, target); log?.Invoke("config: " + Path.GetFileName(cfg)); }
                    }
            }

            foreach (var staged in Directory.GetDirectories(stagingRoot))
            {
                var id = Path.GetFileName(staged);
                if (!present.Contains(id)) { Directory.Delete(staged, true); log?.Invoke("Удалён отписанный мод: " + id); }
            }

            log?.Invoke($"Автозагрузка Workshop: айтемов {items}, файлов {copied}.");
        }

        private static int CopyDir(string src, string dst)
        {
            int n = 0;
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, dir.Substring(src.Length).TrimStart('\\')));
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(dst, file.Substring(src.Length).TrimStart('\\')), true);
                n++;
            }
            return n;
        }
    }
}
