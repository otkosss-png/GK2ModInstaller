using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GK2ModInstaller.Core
{
    // Одно отложенное действие над папкой игры.
    // delete  — удалить файл (legacy-копия DLL, которую нельзя тронуть из-под игры);
    // restore — восстановить файл мода из его бэкапа (откат, который не прошёл).
    public sealed class PendingGameAction
    {
        public const string DeleteKind = "delete";
        public const string RestoreKind = "restore";

        public string Kind { get; set; }
        public string Rel { get; set; }
        public string Id { get; set; }

        public string ToLine()
        {
            if (string.Equals(Kind, RestoreKind, StringComparison.OrdinalIgnoreCase))
                return RestoreKind + "|" + (Rel ?? "") + "|" + (Id ?? "");
            return DeleteKind + "|" + (Rel ?? "");
        }
    }

    // Очередь операций с папкой игры, которые загрузчик не может выполнить из-под запущенной
    // игры (файлы в Managed заняты процессом). Пишет загрузчик, применяет инсталлятор при
    // закрытой игре — BepInExInstaller.ApplyPendingGameActions. См. §10 спеки.
    public static class PendingGameActions
    {
        public const string FileName = "GK2_WorkshopLoader.pendinggame.txt";

        public static List<PendingGameAction> Read(string path, Action<string> log)
        {
            var list = new List<PendingGameAction>();
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return list;
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    var parts = line.Split('|');
                    var kind = parts[0].Trim().ToLowerInvariant();
                    if (kind == PendingGameAction.DeleteKind)
                    {
                        if (parts.Length < 2 || parts[1].Trim().Length == 0)
                        {
                            log?.Invoke(LoaderText.PendingGameSkippedLine + line);
                            continue;
                        }
                        list.Add(new PendingGameAction { Kind = kind, Rel = parts[1].Trim() });
                    }
                    else if (kind == PendingGameAction.RestoreKind)
                    {
                        if (parts.Length < 3 || parts[1].Trim().Length == 0)
                        {
                            log?.Invoke(LoaderText.PendingGameSkippedLine + line);
                            continue;
                        }
                        list.Add(new PendingGameAction { Kind = kind, Rel = parts[1].Trim(), Id = parts[2].Trim() });
                    }
                    else
                    {
                        log?.Invoke(LoaderText.PendingGameUnknownKind + line);
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke(string.Format(LoaderText.PendingGameNotReadFormat, ex.Message));
            }
            return list;
        }

        public static void Write(string path, IEnumerable<PendingGameAction> actions, Action<string> log)
        {
            try
            {
                var distinct = Distinct(actions);
                if (distinct.Count == 0)
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
                    return;
                }
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine(LoaderText.PendingGameHeader);
                foreach (var a in distinct) sb.AppendLine(a.ToLine());
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                log?.Invoke(string.Format(LoaderText.PendingGameNotWrittenFormat, ex.Message));
            }
        }

        // Добавляет действие, не создавая дубликатов (одно и то же delete/restore).
        public static void Add(string path, PendingGameAction action, Action<string> log)
        {
            if (action == null || string.IsNullOrEmpty(action.Rel)) return;
            var list = Read(path, log);
            list.Add(action);
            Write(path, list, log);
        }

        private static List<PendingGameAction> Distinct(IEnumerable<PendingGameAction> actions)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<PendingGameAction>();
            if (actions == null) return list;
            foreach (var a in actions)
            {
                if (a == null || string.IsNullOrEmpty(a.Rel)) continue;
                if (seen.Add(a.ToLine())) list.Add(a);
            }
            return list;
        }
    }
}
