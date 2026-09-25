using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GK2ModInstaller.Core
{
    // Установка модов, которые кладут файлы в папку игры (GraveyardKeeper2_Data\Managed и т.п.),
    // а не в BepInEx\plugins. Кладём резервные копии и манифест, чтобы можно было откатить.
    public static class GameFolderInstaller
    {
        public const string BackupDirName = "GK2_WorkshopLoader.backup";
        public const string ManifestFileName = "manifest.txt";

        private const string ManifestHeader =
            "# rel|1 — файл был до нас (бэкап в files\\), 0 — файла не было";

        // Копирует содержимое sourceDir в gameRoot. Возвращает число скопированных файлов.
        public static int Install(string sourceDir, string gameRoot, string backupRoot, Action<string> log)
        {
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
            {
                log?.Invoke("game-folder: источник не найден — " + sourceDir);
                return 0;
            }
            if (string.IsNullOrEmpty(gameRoot))
            {
                log?.Invoke("game-folder: не задана папка игры — установка невозможна");
                return 0;
            }

            var manifestPath = Path.Combine(backupRoot, ManifestFileName);
            var copied = 0;
            try
            {
                Directory.CreateDirectory(backupRoot);
                if (!File.Exists(manifestPath))
                    File.WriteAllText(manifestPath, ManifestHeader + "\n", new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                log?.Invoke("game-folder: не удалось подготовить бэкап (" + ex.Message + ")");
                return 0;
            }

            foreach (var src in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = src.Substring(sourceDir.Length).TrimStart('\\', '/').Replace('/', '\\');
                if (rel.Length == 0) continue;

                bool rootLevel = rel.IndexOf('\\') < 0;
                if (rootLevel && Path.GetFileName(rel).StartsWith("README", StringComparison.OrdinalIgnoreCase))
                    continue;

                // DLL game-folder мода в папку игры не копируем: их грузит загрузчик из айтема
                // (GameFolderDllLoader). Иначе занятые запущенной игрой файлы мешают обновлению
                // и откату. Копируем только данные (Languages, текстуры и т.п.). См. §10 спеки.
                if (rel.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke(string.Format(LoaderText.GameFolderDllSkipped, rel));
                    continue;
                }

                try
                {
                    var target = Path.Combine(gameRoot, rel);
                    var targetDir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);

                    bool existed = File.Exists(target);
                    if (existed)
                    {
                        var backupFile = Path.Combine(backupRoot, "files", rel);
                        var backupFileDir = Path.GetDirectoryName(backupFile);
                        if (!string.IsNullOrEmpty(backupFileDir)) Directory.CreateDirectory(backupFileDir);
                        if (!File.Exists(backupFile)) File.Copy(target, backupFile, true);
                    }

                    File.Copy(src, target, true);
                    File.AppendAllText(manifestPath, rel + "|" + (existed ? "1" : "0") + "\n", new UTF8Encoding(false));
                    copied++;
                }
                catch (Exception ex)
                {
                    log?.Invoke("game-folder: не удалось скопировать " + rel + " (" + ex.Message + ")");
                }
            }
            return copied;
        }

        // Возвращает папку игры в состояние до установки по манифесту, затем удаляет бэкап.
        // Возвращает число восстановленных/удалённых файлов. Никогда не бросает исключений.
        public static int Restore(string gameRoot, string backupRoot, Action<string> log)
        {
            var restored = 0;
            // Продиктовано ревью: бэкап — единственная копия оригиналов, поэтому удаляем его только
            // если ВСЕ записи манифеста обработаны успешно И папка игры была известна. Иначе —
            // сохраняем для повторного отката (например, файл держит запущенная игра).
            var failed = false;
            var gameRootValid = !string.IsNullOrEmpty(gameRoot);
            try
            {
                if (string.IsNullOrEmpty(backupRoot) || !Directory.Exists(backupRoot)) return 0;
                if (!gameRootValid)
                {
                    log?.Invoke("game-folder: не задана папка игры — откат невозможен, бэкап сохранён: " + backupRoot);
                    return 0;
                }

                var manifestPath = Path.Combine(backupRoot, ManifestFileName);
                if (File.Exists(manifestPath))
                {
                    foreach (var raw in File.ReadAllLines(manifestPath))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                        var parts = line.Split('|');
                        if (parts.Length < 2)
                        {
                            log?.Invoke("game-folder: пропущена строка манифеста: " + line);
                            continue;
                        }

                        var rel = parts[0];
                        var flag = parts[1].Trim();
                        try
                        {
                            var target = Path.Combine(gameRoot, rel);
                            if (flag == "1")
                            {
                                var backupFile = Path.Combine(backupRoot, "files", rel);
                                if (File.Exists(backupFile))
                                {
                                    var targetDir = Path.GetDirectoryName(target);
                                    if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                                    File.Copy(backupFile, target, true);
                                    restored++;
                                }
                                else
                                {
                                    failed = true;
                                    log?.Invoke("game-folder: бэкап не найден — " + rel);
                                }
                            }
                            else
                            {
                                if (File.Exists(target))
                                {
                                    File.Delete(target);
                                    restored++;
                                }
                            }
                            RemoveEmptyParents(Path.GetDirectoryName(target), gameRoot);
                        }
                        catch (Exception ex)
                        {
                            failed = true;
                            log?.Invoke("game-folder: не удалось откатить " + rel + " (" + ex.Message + ")");
                        }
                    }
                }
                else
                {
                    log?.Invoke("game-folder: манифест не найден — " + manifestPath);
                }
            }
            catch (Exception ex)
            {
                failed = true;
                log?.Invoke("game-folder: откат прерван (" + ex.Message + ")");
            }

            if (failed)
            {
                log?.Invoke("game-folder: восстановление неполное — бэкап сохранён: " + backupRoot);
            }
            else if (gameRootValid)
            {
                try { if (Directory.Exists(backupRoot)) Directory.Delete(backupRoot, true); }
                catch (Exception ex) { log?.Invoke("game-folder: бэкап не удалён (" + ex.Message + ")"); }
            }
            return restored;
        }

        // Относительные пути всех файлов из манифеста бэкапа (в порядке записи). Нужно, чтобы
        // поставить отложенный откат, когда обычный не прошёл (файлы заняты игрой). См. §10 спеки.
        public static IReadOnlyList<string> ManifestRelativePaths(string backupRoot)
        {
            var list = new List<string>();
            try
            {
                var manifestPath = Path.Combine(backupRoot, ManifestFileName);
                if (string.IsNullOrEmpty(backupRoot) || !File.Exists(manifestPath)) return list;
                foreach (var raw in File.ReadAllLines(manifestPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    var parts = line.Split('|');
                    if (parts.Length < 1 || parts[0].Length == 0) continue;
                    list.Add(parts[0]);
                }
            }
            catch { }
            return list;
        }

        // Удаляет осиротевшие пустые папки вверх до (не включая) папки игры.
        private static void RemoveEmptyParents(string dir, string gameRoot)
        {
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(gameRoot)) return;
            var root = Path.GetFullPath(gameRoot).TrimEnd('\\', '/');
            var cur = Path.GetFullPath(dir).TrimEnd('\\', '/');
            while (cur.Length > root.Length &&
                   cur.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(cur)) { }
                    else if (Directory.GetFileSystemEntries(cur).Length > 0) break;
                    else Directory.Delete(cur);
                }
                catch { break; }

                var parent = Path.GetDirectoryName(cur);
                if (string.IsNullOrEmpty(parent)) break;
                cur = parent.TrimEnd('\\', '/');
            }
        }
    }
}
