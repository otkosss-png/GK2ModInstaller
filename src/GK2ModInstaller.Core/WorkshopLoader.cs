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

            var trust = TrustStore.Load(trustPath, log);
            var items = WorkshopItemsScanner.Scan(options.WorkshopRoot, log);
            var stagedIds = Directory.GetDirectories(stagingRoot).Select(Path.GetFileName).ToList();
            var manualAssemblies = CollectManualAssemblies(pluginsDir);

            summary.Items = items.Count;
            var plan = ConsentPlanner.Build(
                items, trust,
                item => ModFingerprint.Compute(item.PluginsDir),
                item => ScanItem(item),
                manualAssemblies, stagedIds);

            foreach (var entry in plan)
            {
                summary.Duplicates += entry.Duplicates != null ? entry.Duplicates.Count : 0;
                summary.Findings += entry.Findings != null ? entry.Findings.Count : 0;
                try
                {
                    ApplyKnownOrBlocked(entry, summary, stagingRoot, configDir, log);
                }
                catch (Exception ex)
                {
                    log?.Invoke("Workshop: айтем " + entry.Item.Id + " — ошибка: " + ex.Message);
                }
            }

            if (summary.Removed > 0 || summary.Blocked > 0) trust.Save(trustPath);
            log?.Invoke(summary.ToString());
            return summary;
        }

        private static void ApplyKnownOrBlocked(PlanEntry entry, LoaderSummary summary, string stagingRoot, string configDir, Action<string> log)
        {
            var target = Path.Combine(stagingRoot, entry.Item.Id);
            switch (entry.Kind)
            {
                case DecisionKind.Known:
                    // Уже одобрено: копируем, только если копии нет (первый запуск после ручной чистки).
                    if (!Directory.Exists(target))
                    {
                        WorkshopSync.CopyDir(entry.Item.PluginsDir, target);
                        CopyItemConfigs(entry.Item, configDir, log);
                        log?.Invoke("Workshop: восстановлена копия одобренного мода " + entry.Item.Id);
                    }
                    break;
                case DecisionKind.Blocked:
                    WorkshopSync.DeleteDir(target);
                    summary.Blocked++;
                    log?.Invoke("Workshop: заблокированный мод не грузим — " + entry.Item.Id);
                    break;
                case DecisionKind.Removed:
                    WorkshopSync.DeleteDir(target);
                    summary.Removed++;
                    log?.Invoke("Workshop: мод отписан, копия удалена — " + entry.Item.Id);
                    break;
                default:
                    // New/Update обрабатываются в Task 9.
                    break;
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
