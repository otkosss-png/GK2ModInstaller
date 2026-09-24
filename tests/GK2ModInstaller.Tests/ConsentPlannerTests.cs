using System.Collections.Generic;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class ConsentPlannerTests
    {
        private static WorkshopItem Item(string id, params string[] asmNames) => new WorkshopItem
        {
            Id = id,
            Dir = "ws\\" + id,
            PluginsDir = "ws\\" + id + "\\BepInEx\\plugins",
            DllFiles = new List<string> { "ws\\" + id + "\\BepInEx\\plugins\\Mod.dll" },
            AssemblyNames = asmNames.ToList(),
            Title = "Mod " + id,
            Version = "1.0"
        };

        private static TrustStore Trust(string id, string sha, TrustState state)
        {
            var s = TrustStore.Load(null, null);
            s.Set(new TrustEntry { Id = id, Sha256 = sha, State = state, Title = "Mod " + id });
            return s;
        }

        private static ConsentPlannerHook Hook() => new ConsentPlannerHook();

        private sealed class ConsentPlannerHook
        {
            public List<string> Scanned = new List<string>();
            public IReadOnlyList<Finding> Scan(WorkshopItem item)
            {
                Scanned.Add(item.Id);
                return new List<Finding> { new Finding { Category = FindingCategory.Network, Detail = "x" } };
            }
        }

        [Fact]
        public void New_when_no_trust_entry_or_ask()
        {
            var hook = Hook();
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, TrustStore.Load(null, null), i => "fp", hook.Scan, null, null);
            Assert.Equal(DecisionKind.New, plan[0].Kind);
            Assert.Contains("111", hook.Scanned);
        }

        [Fact]
        public void Known_when_hash_matches_and_no_scan_happens()
        {
            var hook = Hook();
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, Trust("111", "fp", TrustState.Approved), i => "fp", hook.Scan, null, null);
            Assert.Equal(DecisionKind.Known, plan[0].Kind);
            Assert.Empty(hook.Scanned);
        }

        [Fact]
        public void Update_when_hash_changed_and_scan_happens()
        {
            var hook = Hook();
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, Trust("111", "old", TrustState.Approved), i => "new", hook.Scan, null, null);
            Assert.Equal(DecisionKind.Update, plan[0].Kind);
            Assert.Contains("111", hook.Scanned);
            Assert.Single(plan[0].Findings);
        }

        [Fact]
        public void Blocked_when_state_no_and_no_scan()
        {
            var hook = Hook();
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, Trust("111", "fp", TrustState.Blocked), i => "fp", hook.Scan, null, null);
            Assert.Equal(DecisionKind.Blocked, plan[0].Kind);
            Assert.Empty(hook.Scanned);
        }

        [Fact]
        public void Removed_for_staged_ids_that_vanished_and_staged_flag_is_set()
        {
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, Trust("111", "fp", TrustState.Approved), i => "fp", _ => null,
                null, new[] { "111", "999" });
            Assert.True(plan.Single(p => p.Item.Id == "111").Staged);
            var removed = plan.Single(p => p.Kind == DecisionKind.Removed);
            Assert.Equal("999", removed.Item.Id);
        }

        [Fact]
        public void Duplicates_are_matched_by_assembly_name_case_insensitively()
        {
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "MyMod") }, TrustStore.Load(null, null), i => "fp", _ => null,
                new[] { "mymod", "Other" }, null);
            Assert.Equal(new[] { "MyMod" }, plan[0].Duplicates.ToArray());
        }

        [Fact]
        public void Null_scanner_and_null_fingerprint_do_not_throw()
        {
            var plan = ConsentPlanner.Build(new[] { Item("111") }, null, null, null, null, null);
            Assert.Equal(DecisionKind.New, plan[0].Kind);
            Assert.Empty(plan[0].Findings);
            Assert.Empty(plan[0].Duplicates);
        }
    }
}
