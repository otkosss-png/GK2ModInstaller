using System;
using System.Collections.Generic;

namespace GK2ModInstaller.Core
{
    public enum DecisionKind { New, Update, Known, Blocked, Removed }

    public sealed class PlanEntry
    {
        public DecisionKind Kind { get; set; }
        public WorkshopItem Item { get; set; }
        public TrustEntry Trust { get; set; }
        public string Fingerprint { get; set; }
        public bool Staged { get; set; }
        public IReadOnlyList<Finding> Findings { get; set; }
        public IReadOnlyList<string> Duplicates { get; set; }
    }

    public static class ConsentPlanner
    {
        public static List<PlanEntry> Build(
            IReadOnlyList<WorkshopItem> items,
            TrustStore trust,
            Func<WorkshopItem, string> fingerprint,
            Func<WorkshopItem, IReadOnlyList<Finding>> scanFindings,
            IReadOnlyList<string> manualAssemblies,
            IReadOnlyList<string> stagedIds)
        {
            var result = new List<PlanEntry>();
            var manual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (manualAssemblies != null) foreach (var a in manualAssemblies) manual.Add(a);
            var staged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (stagedIds != null) foreach (var id in stagedIds) staged.Add(id);
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    present.Add(item.Id);
                    string fp = fingerprint != null ? (fingerprint(item) ?? "") : "";
                    var entry = trust != null ? trust.Get(item.Id) : null;

                    DecisionKind kind;
                    if (entry == null || entry.State == TrustState.Ask) kind = DecisionKind.New;
                    else if (entry.State == TrustState.Blocked) kind = DecisionKind.Blocked;
                    else kind = string.Equals(entry.Sha256, fp, StringComparison.OrdinalIgnoreCase)
                        ? DecisionKind.Known
                        : DecisionKind.Update;

                    bool needsScan = kind == DecisionKind.New || kind == DecisionKind.Update;
                    var duplicates = new List<string>();
                    if (item.AssemblyNames != null)
                        foreach (var name in item.AssemblyNames)
                            if (!string.IsNullOrEmpty(name) && manual.Contains(name)) duplicates.Add(name);

                    result.Add(new PlanEntry
                    {
                        Kind = kind,
                        Item = item,
                        Trust = entry,
                        Fingerprint = fp,
                        Staged = staged.Contains(item.Id),
                        Findings = needsScan && scanFindings != null ? scanFindings(item) : new List<Finding>(),
                        Duplicates = duplicates
                    });
                }
            }

            if (stagedIds != null)
            {
                foreach (var id in stagedIds)
                {
                    if (string.IsNullOrEmpty(id) || present.Contains(id)) continue;
                    result.Add(new PlanEntry
                    {
                        Kind = DecisionKind.Removed,
                        Item = new WorkshopItem { Id = id, Title = id, AssemblyNames = new List<string>(), DllFiles = new List<string>() },
                        Fingerprint = "",
                        Staged = true,
                        Findings = new List<Finding>(),
                        Duplicates = new List<string>()
                    });
                }
            }

            return result;
        }
    }
}
