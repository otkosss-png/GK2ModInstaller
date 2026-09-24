using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GK2ModInstaller.Core
{
    public sealed class LoaderOptions
    {
        public string WorkshopRoot { get; set; }
        public string WorkshopAcfPath { get; set; }
        public string BepInExRoot { get; set; }
    }

    public sealed class LoaderSummary
    {
        public int Items, Approved, Updates, Blocked, Postponed, Removed, Migrated, Duplicates, Findings;

        public override string ToString()
        {
            return string.Format(
                "Workshop: айтемов {0}, одобрено {1}, обновлений {2}, заблокировано {3}, отложено {4}, удалено {5}, миграция {6}, предупреждений {7}.",
                Items, Approved, Updates, Blocked, Postponed, Removed, Migrated, Findings);
        }
    }

    public static class WorkshopLoader
    {
        public const string StagingDirName = "_Workshop";
        public const string TrustFileName = "GK2_WorkshopLoader.trust.txt";
        public const string PendingFileName = "GK2_WorkshopLoader.pending.txt";

        public static LoaderSummary Run(LoaderOptions options, IDialog dialog, Action<string> log)
        {
            var summary = new LoaderSummary();
            if (options == null) { log?.Invoke("Workshop: нет опций — пропуск"); return summary; }
            if (string.IsNullOrEmpty(options.WorkshopRoot) || !Directory.Exists(options.WorkshopRoot))
            {
                log?.Invoke("Workshop: папка не найдена — " + options.WorkshopRoot);
                return summary;
            }
            if (string.IsNullOrEmpty(options.BepInExRoot) || !Directory.Exists(options.BepInExRoot))
            {
                log?.Invoke("Workshop: BepInEx не найден — " + options.BepInExRoot);
                return summary;
            }

            var pluginsDir = Path.Combine(options.BepInExRoot, "plugins");
            var configDir = Path.Combine(options.BepInExRoot, "config");
            var stagingRoot = Path.Combine(pluginsDir, StagingDirName);
            var trustPath = Path.Combine(configDir, TrustFileName);
            Directory.CreateDirectory(stagingRoot);

            // Папка игры — родитель BepInEx. Без неё моды в папку игры установить нельзя.
            var gameRoot = Directory.GetParent(options.BepInExRoot)?.FullName;

            var trust = TrustStore.Load(trustPath, log);
            var items = WorkshopItemsScanner.Scan(options.WorkshopRoot, log);
            if (string.IsNullOrEmpty(gameRoot))
            {
                log?.Invoke("Workshop: папка игры не определена — моды в папку игры не поддерживаются");
                items = items.Where(i => i.Kind != WorkshopItemKind.GameFolder).ToList();
            }

            // Установленным считаем и то, что лежит в стейджинге (плагины), и то, для чего есть бэкап
            // (моды в папку игры): последнее нужно для миграции и распознавания "уже стоит".
            var stagedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in Directory.GetDirectories(stagingRoot)) stagedSet.Add(Path.GetFileName(d));
            var backupBase = Path.Combine(configDir, GameFolderInstaller.BackupDirName);
            if (Directory.Exists(backupBase))
                foreach (var d in Directory.GetDirectories(backupBase)) stagedSet.Add(Path.GetFileName(d));
            var stagedIds = stagedSet.ToList();

            var manualAssemblies = CollectManualAssemblies(pluginsDir);

            summary.Items = items.Count;
            var plan = ConsentPlanner.Build(
                items, trust,
                item => ModFingerprint.Compute(item.SourceDir),
                item => ScanItem(item),
                manualAssemblies, stagedIds);

            // Единый вопрос про моды, уже установленные прежним авто-загрузчиком (New + папка стейджинга есть).
            var migration = plan.Where(p => p.Kind == DecisionKind.New && p.Staged).ToList();
            var migratedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (migration.Count > 0)
            {
                var prompts = migration.Select(ToPrompt).ToList();
                BulkAnswer bulk = BulkAnswer.Later;
                try { if (dialog != null) bulk = dialog.AskBulkTrust(prompts); }
                catch (Exception ex) { log?.Invoke("Workshop: сводный диалог недоступен (" + ex.Message + ")"); }

                foreach (var p in migration)
                {
                    if (bulk == BulkAnswer.All)
                    {
                        trust.Set(new TrustEntry
                        {
                            Id = p.Item.Id,
                            Sha256 = p.Fingerprint,
                            State = TrustState.Approved,
                            Title = p.Item.Title,
                            Note = "миграция " + DateTime.Now.ToString("yyyy-MM-dd")
                        });
                        migratedIds.Add(p.Item.Id);
                        summary.Migrated++;
                    }
                    else if (bulk == BulkAnswer.Later)
                    {
                        trust.Set(new TrustEntry { Id = p.Item.Id, Sha256 = p.Fingerprint, State = TrustState.Ask, Title = p.Item.Title, Note = "миграция отложена" });
                        migratedIds.Add(p.Item.Id);
                    }
                    // BulkAnswer.AskEach — спрашиваем индивидуально в общем цикле (kind остаётся New).
                }
                if (bulk != BulkAnswer.AskEach) log?.Invoke("Workshop: миграция прежних установок — " + migration.Count + " шт., ответ: " + bulk);
            }

            var pending = new List<ModPrompt>();
            foreach (var entry in plan)
            {
                if (migratedIds.Contains(entry.Item.Id)) continue;
                summary.Duplicates += entry.Duplicates != null ? entry.Duplicates.Count : 0;
                summary.Findings += entry.Findings != null ? entry.Findings.Count : 0;
                try
                {
                    if (entry.Kind == DecisionKind.New || entry.Kind == DecisionKind.Update)
                        ConsentAndApply(entry, stagingRoot, configDir, gameRoot, trust, dialog, pending, summary, log);
                    else
                        ApplyKnownOrBlocked(entry, summary, stagingRoot, configDir, gameRoot, log);
                }
                catch (Exception ex)
                {
                    log?.Invoke("Workshop: айтем " + entry.Item.Id + " — ошибка: " + ex.Message);
                }
            }

            if (pending.Count > 0)
                PendingList.Write(Path.Combine(configDir, PendingFileName), pending, log);
            if (pending.Count > 0 || summary.Approved > 0 || summary.Updates > 0 || summary.Blocked > 0 || summary.Removed > 0 || migration.Count > 0)
            {
                try
                {
                    trust.Save(trustPath);
                }
                catch (Exception ex)
                {
                    log?.Invoke("trust: не сохранён (" + ex.Message + ") — решения этого запуска будут потеряны");
                }
            }
            LogAcfUpdates(options, plan, log);
            log?.Invoke(summary.ToString());
            return summary;
        }

        // Спрашивает игрока про New/Update; ответ определяет копирование и запись в trust.
        private static void ConsentAndApply(PlanEntry entry, string stagingRoot, string configDir, string gameRoot,
            TrustStore trust, IDialog dialog, List<ModPrompt> pending, LoaderSummary summary, Action<string> log)
        {
            var target = Path.Combine(stagingRoot, entry.Item.Id);
            var prompt = ToPrompt(entry);

            ConsentAnswer answer = ConsentAnswer.Later;
            try
            {
                if (dialog != null) answer = dialog.Ask(prompt);
            }
            catch (Exception ex)
            {
                log?.Invoke("Workshop: диалог недоступен (" + ex.Message + ") — мод отложен: " + entry.Item.Id);
                answer = ConsentAnswer.Later;
            }

            if (answer == ConsentAnswer.Approve)
            {
                var backupRoot = Path.Combine(configDir, GameFolderInstaller.BackupDirName, entry.Item.Id);
                if (entry.Item.Kind == WorkshopItemKind.GameFolder)
                {
                    if (string.IsNullOrEmpty(gameRoot))
                    {
                        log?.Invoke("Workshop: game-folder мод " + entry.Item.Id + " — папка игры не определена, пропуск");
                    }
                    else
                    {
                        // Сначала откатываем прошлую установку: бэкап должен хранить истинные оригиналы.
                        GameFolderInstaller.Restore(gameRoot, backupRoot, log);
                        int n = GameFolderInstaller.Install(entry.Item.SourceDir, gameRoot, backupRoot, log);
                        log?.Invoke("Workshop: game-folder мод " + entry.Item.Id + " — установлено файлов " + n);
                    }
                }
                else
                {
                    WorkshopSync.DeleteDir(target);
                    WorkshopSync.CopyDir(entry.Item.SourceDir, target);
                    CopyItemConfigs(entry.Item, configDir, log);
                }
                trust.Set(new TrustEntry
                {
                    Id = entry.Item.Id,
                    Sha256 = entry.Fingerprint,
                    State = TrustState.Approved,
                    Title = entry.Item.Title,
                    Note = (entry.Kind == DecisionKind.Update ? "обновление " : "одобрено ") + DateTime.Now.ToString("yyyy-MM-dd")
                });
                if (entry.Kind == DecisionKind.Update) summary.Updates++; else summary.Approved++;
                log?.Invoke("Workshop: одобрен мод " + entry.Item.Id + " (" + entry.Item.Title + ")");
            }
            else if (answer == ConsentAnswer.Deny)
            {
                if (entry.Item.Kind == WorkshopItemKind.GameFolder)
                {
                    if (!string.IsNullOrEmpty(gameRoot))
                        GameFolderInstaller.Restore(gameRoot,
                            Path.Combine(configDir, GameFolderInstaller.BackupDirName, entry.Item.Id), log);
                }
                else
                {
                    WorkshopSync.DeleteDir(target);
                }
                trust.Set(new TrustEntry
                {
                    Id = entry.Item.Id,
                    Sha256 = entry.Fingerprint,
                    State = TrustState.Blocked,
                    Title = entry.Item.Title,
                    Note = "заблокирован " + DateTime.Now.ToString("yyyy-MM-dd")
                });
                summary.Blocked++;
                log?.Invoke("Workshop: заблокирован мод " + entry.Item.Id);
            }
            else
            {
                if (entry.Kind == DecisionKind.Update && entry.Trust != null)
                {
                    // Обновление отложено: на диске остаётся прежняя одобренная версия.
                    // НЕ перезаписываем хеш на новый — иначе старые файлы в стейджинге
                    // разойдутся с новым хешем в trust, и на следующем запуске миграция
                    // (New + Staged) позволит «довериться» новому исходнику, не спросив.
                    entry.Trust.Note = "обновление отложено " + DateTime.Now.ToString("yyyy-MM-dd");
                    trust.Set(entry.Trust);
                }
                else
                {
                    trust.Set(new TrustEntry
                    {
                        Id = entry.Item.Id,
                        Sha256 = entry.Fingerprint,
                        State = TrustState.Ask,
                        Title = entry.Item.Title,
                        Note = "отложено " + DateTime.Now.ToString("yyyy-MM-dd")
                    });
                }
                summary.Postponed++;
                pending.Add(prompt);
                log?.Invoke("Workshop: отложен мод " + entry.Item.Id);
            }
        }

        private static void ApplyKnownOrBlocked(PlanEntry entry, LoaderSummary summary, string stagingRoot, string configDir, string gameRoot, Action<string> log)
        {
            var target = Path.Combine(stagingRoot, entry.Item.Id);
            var backupRoot = Path.Combine(configDir, GameFolderInstaller.BackupDirName, entry.Item.Id);
            // Removed-записи создаются по id из стейджинга и не знают Kind. Признак мода в папку игры —
            // сохранённый бэкап: у плагинов его не бывает.
            bool gameFolder = entry.Item.Kind == WorkshopItemKind.GameFolder || Directory.Exists(backupRoot);
            switch (entry.Kind)
            {
                case DecisionKind.Known:
                    if (entry.Item.Kind == WorkshopItemKind.GameFolder)
                    {
                        if (string.IsNullOrEmpty(gameRoot))
                        {
                            log?.Invoke("Workshop: game-folder мод " + entry.Item.Id + " — папка игры не определена, пропуск");
                            break;
                        }
                        // Уже одобрено: если установки нет (пропал манифест) — восстанавливаем.
                        if (!File.Exists(Path.Combine(backupRoot, GameFolderInstaller.ManifestFileName)))
                        {
                            int n = GameFolderInstaller.Install(entry.Item.SourceDir, gameRoot, backupRoot, log);
                            log?.Invoke("Workshop: восстановлена установка game-folder мода " + entry.Item.Id + " — файлов " + n);
                        }
                    }
                    // Плагин: копируем, только если копии нет (первый запуск после ручной чистки).
                    else if (!Directory.Exists(target))
                    {
                        WorkshopSync.CopyDir(entry.Item.SourceDir, target);
                        CopyItemConfigs(entry.Item, configDir, log);
                        log?.Invoke("Workshop: восстановлена копия одобренного мода " + entry.Item.Id);
                    }
                    break;
                case DecisionKind.Blocked:
                    if (gameFolder)
                    {
                        if (!string.IsNullOrEmpty(gameRoot)) GameFolderInstaller.Restore(gameRoot, backupRoot, log);
                    }
                    else
                    {
                        WorkshopSync.DeleteDir(target);
                    }
                    summary.Blocked++;
                    log?.Invoke("Workshop: заблокированный мод не грузим — " + entry.Item.Id);
                    break;
                case DecisionKind.Removed:
                    if (gameFolder)
                    {
                        if (!string.IsNullOrEmpty(gameRoot)) GameFolderInstaller.Restore(gameRoot, backupRoot, log);
                        log?.Invoke("Workshop: game-folder мод отписан, файлы возвращены — " + entry.Item.Id);
                    }
                    else
                    {
                        WorkshopSync.DeleteDir(target);
                        log?.Invoke("Workshop: мод отписан, копия удалена — " + entry.Item.Id);
                    }
                    summary.Removed++;
                    break;
                default:
                    // New/Update обрабатываются выше.
                    break;
            }
        }

        private static ModPrompt ToPrompt(PlanEntry entry)
        {
            return new ModPrompt
            {
                Id = entry.Item.Id,
                Title = entry.Item.Title,
                Version = entry.Item.Version,
                IsUpdate = entry.Kind == DecisionKind.Update,
                Files = entry.Item.DllFiles != null ? entry.Item.DllFiles.Select(Path.GetFileName).ToList() : new List<string>(),
                Target = entry.Item.Kind == WorkshopItemKind.GameFolder
                    ? "папка игры (GraveyardKeeper2_Data\\Managed и т.п.)"
                    : "BepInEx\\plugins",
                Findings = entry.Findings ?? new List<Finding>(),
                Duplicates = entry.Duplicates ?? new List<string>()
            };
        }

        private static void LogAcfUpdates(LoaderOptions options, List<PlanEntry> plan, Action<string> log)
        {
            try
            {
                if (string.IsNullOrEmpty(options.WorkshopAcfPath) || !File.Exists(options.WorkshopAcfPath)) return;
                var times = AcfTimes.Parse(File.ReadAllText(options.WorkshopAcfPath));
                foreach (var entry in plan)
                {
                    long t;
                    if (!times.TryGetValue(entry.Item.Id, out t)) continue;
                    log?.Invoke("Workshop: ACF — у мода " + entry.Item.Id + " timeupdated=" + t);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("Workshop: ACF не разобран (" + ex.Message + ")");
            }
        }

        private static void CopyItemConfigs(WorkshopItem item, string configDir, Action<string> log)
        {
            var src = Path.Combine(item.Dir, "BepInEx", "config");
            WorkshopSync.CopyConfigs(src, configDir, log);
        }

        private static IReadOnlyList<Finding> ScanItem(WorkshopItem item)
        {
            var all = new List<Finding>();
            if (item.DllFiles == null) return all;
            foreach (var dll in item.DllFiles)
            {
                var findings = CodeScan.Scan(dll);
                if (findings != null) all.AddRange(findings);
            }
            return all;
        }

        private static List<string> CollectManualAssemblies(string pluginsDir)
        {
            var names = new List<string>();
            if (!Directory.Exists(pluginsDir)) return names;
            foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll", SearchOption.AllDirectories))
            {
                var rel = dll.Substring(pluginsDir.Length).TrimStart('\\', '/');
                if (rel.StartsWith(StagingDirName, StringComparison.OrdinalIgnoreCase)) continue;
                var info = AssemblyInfoReader.TryRead(dll, null);
                if (!string.IsNullOrEmpty(info.AssemblyName)) names.Add(info.AssemblyName);
            }
            return names;
        }
    }
}
