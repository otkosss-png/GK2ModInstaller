using System;
using System.IO;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class WorkshopLoaderTests
    {
        private static LoaderOptions Options(string root)
        {
            string bep = Path.Combine(root, "BepInEx");
            Directory.CreateDirectory(bep);
            return new LoaderOptions
            {
                WorkshopRoot = Path.Combine(root, "ws"),
                BepInExRoot = bep,
                WorkshopAcfPath = Path.Combine(root, "appworkshop_4358690.acf")
            };
        }

        [Fact]
        public void Known_approved_mod_is_not_asked_and_stays_in_place()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", "mod.cfg");
                string fp = ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "111", "BepInEx", "plugins"));
                var store = TrustStore.Load(Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt"), null);
                store.Set(new TrustEntry { Id = "111", Sha256 = fp, State = TrustState.Approved, Title = "Mod" });
                store.Save(Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt"));

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Empty(dialog.Asked);
                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "111"), "Mod.dll")));
                Assert.Equal(1, summary.Items);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Blocked_mod_is_never_asked_and_its_staging_is_removed()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "222", "MOD", null);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "222", "OLD");
                string trust = Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt");
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "222", Sha256 = "x", State = TrustState.Blocked, Title = "Bad" });
                store.Save(trust);

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Empty(dialog.Asked);
                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "222")));
                Assert.Equal(1, summary.Blocked);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Removed_item_is_deleted_from_staging_and_trust_entry_is_kept()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "999", "OLD");
                string trust = Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt");
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "999", Sha256 = "x", State = TrustState.Approved, Title = "Gone" });
                store.Save(trust);

                var summary = WorkshopLoader.Run(options, new FakeDialog(), null);

                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "999")));
                Assert.Equal(1, summary.Removed);
                Assert.NotNull(TrustStore.Load(trust, null).Get("999"));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Missing_workshop_root_is_reported_and_does_not_throw()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                var logs = new System.Collections.Generic.List<string>();
                var summary = WorkshopLoader.Run(options, new FakeDialog(), logs.Add);
                Assert.Equal(0, summary.Items);
                Assert.Contains(logs, l => l.Contains("Workshop"));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Config_is_not_overwritten_but_plugin_files_are_copied()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "333", "MOD", "mod.cfg");
                Directory.CreateDirectory(Path.Combine(options.BepInExRoot, "config"));
                File.WriteAllText(Path.Combine(options.BepInExRoot, "config", "mod.cfg"), "USER");
                string trust = Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt");
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "333", Sha256 = ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "333", "BepInEx", "plugins")), State = TrustState.Approved });
                store.Save(trust);

                WorkshopLoader.Run(options, new FakeDialog(), null);

                Assert.Equal("USER", File.ReadAllText(Path.Combine(options.BepInExRoot, "config", "mod.cfg")));
                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "333"), "Mod.dll")));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        private static LoaderOptions OptionsForNewMod(string root, string id, string dll, out FakeDialog dialog)
        {
            var options = Options(root);
            Directory.CreateDirectory(options.WorkshopRoot);
            MakeWorkshop.Item(options.WorkshopRoot, id, dll, "mod.cfg");
            dialog = new FakeDialog();
            return options;
        }

        [Fact]
        public void New_mod_approved_is_copied_and_recorded_in_trust()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "444", "MOD", out dialog);
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal(new[] { "444" }, dialog.Asked.ToArray());
                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "444"), "Mod.dll")));
                var entry = TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("444");
                Assert.Equal(TrustState.Approved, entry.State);
                Assert.Equal(ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "444", "BepInEx", "plugins")), entry.Sha256);
                Assert.Equal(1, summary.Approved);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void New_mod_denied_is_not_copied_and_blocked_in_trust()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "555", "MOD", out dialog);
                dialog.OnAsk = _ => ConsentAnswer.Deny;
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "555")));
                Assert.Equal(TrustState.Blocked,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("555").State);
                Assert.Equal(1, summary.Blocked);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void New_mod_later_is_not_copied_and_goes_to_pending()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "666", "MOD", out dialog);
                dialog.OnAsk = _ => ConsentAnswer.Later;
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "666")));
                string pending = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.PendingFileName);
                Assert.True(File.Exists(pending));
                Assert.Contains("666", File.ReadAllText(pending));
                Assert.Equal(1, summary.Postponed);
                Assert.Equal(TrustState.Ask,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("666").State);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Null_dialog_means_later_and_nothing_is_copied()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "777", "MOD", out dialog);
                var summary = WorkshopLoader.Run(options, null, null);

                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "777")));
                Assert.Equal(1, summary.Postponed);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Update_later_keeps_previous_approved_version_running()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "888", "NEW", out dialog);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "888", "OLD");
                string trustPath = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trustPath, null);
                store.Set(new TrustEntry { Id = "888", Sha256 = "старый-хеш", State = TrustState.Approved, Title = "Mod" });
                store.Save(trustPath);
                dialog.OnAsk = _ => ConsentAnswer.Later;

                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal("OLD", File.ReadAllText(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "888"), "Mod.dll")));
                Assert.Equal(1, summary.Postponed);
                Assert.Equal(0, summary.Updates);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Update_approved_replaces_files_and_updates_hash()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "999", "NEW", out dialog);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "999", "OLD");
                string trustPath = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trustPath, null);
                store.Set(new TrustEntry { Id = "999", Sha256 = "старый-хеш", State = TrustState.Approved, Title = "Mod" });
                store.Save(trustPath);

                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal("NEW", File.ReadAllText(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "999"), "Mod.dll")));
                Assert.Equal(1, summary.Updates);
                Assert.Equal(ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "999", "BepInEx", "plugins")),
                    TrustStore.Load(trustPath, null).Get("999").Sha256);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Findings_are_reported_in_log_and_summary()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "1000", "MOD", out dialog);
                dialog.OnAsk = _ => ConsentAnswer.Later;
                var logs = new System.Collections.Generic.List<string>();
                WorkshopLoader.Run(options, dialog, logs.Add);
                Assert.Contains(logs, l => l.StartsWith("Workshop:") && l.Contains("отложен"));
                string pending = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.PendingFileName);
                Assert.Contains("1000", File.ReadAllText(pending));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }
    }
}
