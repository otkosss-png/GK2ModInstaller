using System;
using System.IO;

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
