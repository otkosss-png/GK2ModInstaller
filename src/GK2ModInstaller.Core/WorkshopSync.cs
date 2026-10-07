using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GK2ModInstaller.Core
{
    // Низкоуровневые файловые операции автозагрузчика. Решения «что грузить» принимает WorkshopLoader.
    public static class WorkshopSync
    {
        public static int CopyDir(string src, string dst)
        {
            int n = 0;
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, dir.Substring(src.Length).TrimStart('\\', '/')));
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(dst, file.Substring(src.Length).TrimStart('\\', '/')), true);
                n++;
            }
            return n;
        }

        // Список файлов, пришедших из айтема при прошлой синхронизации (относительные пути).
        public const string ManifestName = ".gk2loader_files.txt";

        // Синхронизация копии мода с айтемом, не стирая чужие файлы. Файлы айтема перезаписываются.
        // Удаляется только то, что раньше пришло из айтема (есть в манифесте), а в айтеме больше нет.
        // Файлы, которые создал сам мод или игрок (переводы Localization\*.json и т.п.), остаются.
        // Без манифеста (копия от старого загрузчика) убираем лишь посторонние *.dll — чтобы старая
        // версия плагина под другим именем не загрузилась вместе с новой.
        public static int SyncDir(string src, string dst, Action<string> log = null)
        {
            Directory.CreateDirectory(dst);
            var manifestPath = Path.Combine(dst, ManifestName);
            HashSet<string> previous = null;
            if (File.Exists(manifestPath))
            {
                previous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(manifestPath))
                    if (!string.IsNullOrWhiteSpace(line)) previous.Add(line.Trim());
            }

            var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int n = 0;
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, Rel(src, dir)));
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var rel = Rel(src, file);
                File.Copy(file, Path.Combine(dst, rel), true);
                current.Add(rel);
                n++;
            }

            foreach (var file in Directory.GetFiles(dst, "*", SearchOption.AllDirectories))
            {
                var rel = Rel(dst, file);
                if (string.Equals(rel, ManifestName, StringComparison.OrdinalIgnoreCase) || current.Contains(rel)) continue;
                bool stale = previous != null
                    ? previous.Contains(rel)
                    : rel.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                if (!stale) continue;
                try { File.Delete(file); log?.Invoke("sync: removed " + rel); }
                catch (Exception ex) { log?.Invoke("sync: cannot remove " + rel + " (" + ex.Message + ")"); }
            }

            File.WriteAllLines(manifestPath, current.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            return n;
        }

        private static string Rel(string root, string path) => path.Substring(root.Length).TrimStart('\\', '/');

        // Конфиги копируются только если такого файла ещё нет (не затираем правки игрока).
        public static int CopyConfigs(string srcConfigDir, string configDir, Action<string> log)
        {
            int n = 0;
            if (string.IsNullOrEmpty(srcConfigDir) || !Directory.Exists(srcConfigDir)) return 0;
            Directory.CreateDirectory(configDir);
            foreach (var cfg in Directory.GetFiles(srcConfigDir, "*.cfg", SearchOption.AllDirectories))
            {
                var target = Path.Combine(configDir, Path.GetFileName(cfg));
                if (File.Exists(target)) continue;
                File.Copy(cfg, target);
                log?.Invoke("config: " + Path.GetFileName(cfg));
                n++;
            }
            return n;
        }

        public static void DeleteDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
