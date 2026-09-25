using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GK2ModInstaller.Core
{
    public static class BepInExInstaller
    {
        public const string BepInExDirName = "BepInEx";
        public const string LoaderFileName = "GK2.WorkshopLoader.dll";
        public static readonly string[] BepInExRootFiles =
        {
            "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt"
        };

        public static bool IsGameRunning()
        {
            try { return Process.GetProcessesByName("GraveyardKeeper2").Length > 0; }
            catch { return false; }
        }

        public static IReadOnlyList<string> Verify(string gameDir, bool expectPatcher = false)
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
            if (expectPatcher && !File.Exists(Path.Combine(gameDir, BepInExDirName, "patchers", "GK2.WorkshopAutoLoader.dll")))
                problems.Add("нет GK2.WorkshopAutoLoader.dll");
            if (expectPatcher && !File.Exists(Path.Combine(gameDir, BepInExDirName, LoaderFileName)))
                problems.Add("Нет " + Path.Combine(BepInExDirName, LoaderFileName) + " (офлайн-копия загрузчика)");
            return problems;
        }

        public static void Install(string gameDir, Stream bepinexZip, Stream frameworkZip, Stream patcherDll, Stream loaderDll, bool backupExisting, Action<string> log)
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
            if (patcherDll != null)
            {
                var patchersDir = Path.Combine(gameDir, BepInExDirName, "patchers");
                Directory.CreateDirectory(patchersDir);
                var dst = Path.Combine(patchersDir, "GK2.WorkshopAutoLoader.dll");
                log?.Invoke("Патчер автозагрузки -> " + dst);
                using (var fs = File.Create(dst)) patcherDll.CopyTo(fs);
            }
            if (loaderDll != null)
            {
                var loaderPath = Path.Combine(gameDir, BepInExDirName, LoaderFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(loaderPath));
                using (var fs = File.Create(loaderPath)) loaderDll.CopyTo(fs);
                log?.Invoke("Загрузчик: " + Path.Combine(BepInExDirName, LoaderFileName));
            }

            // Инсталлятор запускается при закрытой игре — самое время применить отложенные
            // операции загрузчика с файлами в папке игры (legacy-копии DLL, неудавшиеся откаты).
            ApplyPendingGameActions(gameDir, log);
        }

        // Применяет очередь из BepInEx\config\GK2_WorkshopLoader.pendinggame.txt: удаляет legacy-копии
        // и восстанавливает файлы из бэкапов модов. Неудавшиеся (файл занят) оставляет на следующий раз.
        // Возвращает число применённых действий. Никогда не бросает исключений.
        public static int ApplyPendingGameActions(string gameDir, Action<string> log)
        {
            var applied = 0;
            if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir)) return applied;
            var pendingPath = Path.Combine(gameDir, BepInExDirName, "config", PendingGameActions.FileName);
            var actions = PendingGameActions.Read(pendingPath, log);
            if (actions.Count == 0) return applied;

            log?.Invoke(string.Format(LoaderText.PendingGameApplyStart, actions.Count));
            var remaining = new List<PendingGameAction>();
            foreach (var a in actions)
            {
                var target = Path.Combine(gameDir, a.Rel);
                try
                {
                    if (string.Equals(a.Kind, PendingGameAction.DeleteKind, StringComparison.OrdinalIgnoreCase))
                    {
                        // File.Delete — no-op для отсутствующего файла; на каталоге/занятом файле бросает,
                        // и действие остаётся до следующего запуска (инсталлятор при закрытой игре).
                        File.Delete(target);
                        applied++;
                        log?.Invoke(string.Format(LoaderText.PendingGameDeleted, a.Rel));
                    }
                    else
                    {
                        var backupFile = Path.Combine(gameDir, BepInExDirName, "config",
                            GameFolderInstaller.BackupDirName, a.Id, "files", a.Rel);
                        var targetDir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                        if (File.Exists(backupFile)) File.Copy(backupFile, target, true);
                        else File.Delete(target);
                        applied++;
                        log?.Invoke(string.Format(LoaderText.PendingGameRestored, a.Rel, a.Id));
                    }
                }
                catch (Exception ex)
                {
                    remaining.Add(a);
                    var format = string.Equals(a.Kind, PendingGameAction.DeleteKind, StringComparison.OrdinalIgnoreCase)
                        ? LoaderText.PendingGameDeleteFailed
                        : LoaderText.PendingGameRestoreFailed;
                    log?.Invoke(string.Format(format, a.Rel) + " (" + ex.Message + ")");
                }
            }
            PendingGameActions.Write(pendingPath, remaining, log);
            log?.Invoke(string.Format(LoaderText.PendingGameDone, applied, remaining.Count));
            return applied;
        }

        public static void Uninstall(string gameDir, Action<string> log)
        {
            var patcher = Path.Combine(gameDir, BepInExDirName, "patchers", "GK2.WorkshopAutoLoader.dll");
            if (File.Exists(patcher)) { File.Delete(patcher); log?.Invoke("Удалено: patchers\\GK2.WorkshopAutoLoader.dll"); }
            var loader = Path.Combine(gameDir, BepInExDirName, LoaderFileName);
            if (File.Exists(loader)) { File.Delete(loader); log?.Invoke("Удалено: " + LoaderFileName); }
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
