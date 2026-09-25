using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class WorkshopLoaderTests
    {
        public WorkshopLoaderTests()
        {
            LoaderText.Language = LoaderLanguage.En;
        }

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
                Assert.Contains(logs, l => l.StartsWith("Workshop:") && l.Contains("postponed"));
                string pending = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.PendingFileName);
                Assert.Contains("1000", File.ReadAllText(pending));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Migration_bulk_yes_trusts_all_and_does_not_ask_each()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", null);
                MakeWorkshop.Item(options.WorkshopRoot, "222", "MOD2", null);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "111", "MOD");
                MakeWorkshop.MakeStaged(options.BepInExRoot, "222", "MOD2");

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal(new[] { "bulk:111", "bulk:222" }, dialog.Asked.ToArray());
                var store = TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null);
                Assert.Equal(TrustState.Approved, store.Get("111").State);
                Assert.Equal(TrustState.Approved, store.Get("222").State);
                Assert.Equal(2, summary.Migrated);
                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "111"), "Mod.dll")));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Migration_bulk_no_asks_each_separately()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", null);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "111", "MOD");
                var dialog = new FakeDialog();
                dialog.OnBulk = _ => BulkAnswer.AskEach;
                dialog.OnAsk = _ => ConsentAnswer.Deny;

                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal(new[] { "bulk:111", "111" }, dialog.Asked.ToArray());
                Assert.Equal(TrustState.Blocked,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("111").State);
                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "111")));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Migration_bulk_later_keeps_files_and_asks_again_next_time()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", null);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "111", "MOD");
                var dialog = new FakeDialog();
                dialog.OnBulk = _ => BulkAnswer.Later;

                WorkshopLoader.Run(options, dialog, null);

                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "111"), "Mod.dll")));
                Assert.Equal(TrustState.Ask,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("111").State);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Acf_newer_timeupdated_is_logged_for_known_mod()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", null);
                string fp = ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "111", "BepInEx", "plugins"));
                string trust = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "111", Sha256 = fp, State = TrustState.Approved, Title = "Mod", Note = "2026-09-01" });
                store.Save(trust);
                File.WriteAllText(options.WorkshopAcfPath,
                    "\"AppWorkshop\"\n{\n\t\"WorkshopItemsInstalled\"\n\t{\n\t\t\"111\"\n\t\t{\n\t\t\t\"timeupdated\"\t\t\"3200000000\"\n\t\t}\n\t}\n}\n");
                MakeWorkshop.MakeStaged(options.BepInExRoot, "111", "MOD");

                var logs = new System.Collections.Generic.List<string>();
                WorkshopLoader.Run(options, new FakeDialog(), logs.Add);

                Assert.Contains(logs, l => l.Contains("111") && l.Contains("ACF"));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Update_later_keeps_old_approval_and_second_run_reasks_without_bulk()
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

                int bulkCalls = 0;
                dialog.OnBulk = _ => { bulkCalls++; return BulkAnswer.All; };
                dialog.OnAsk = _ => ConsentAnswer.Later;

                var first = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal("OLD", File.ReadAllText(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "888"), "Mod.dll")));
                Assert.Equal(1, first.Postponed);
                Assert.Equal(0, first.Updates);
                Assert.Equal(0, bulkCalls);
                var afterFirst = TrustStore.Load(trustPath, null).Get("888");
                Assert.Equal(TrustState.Approved, afterFirst.State);
                Assert.Equal("старый-хеш", afterFirst.Sha256);
                Assert.Contains("postponed", afterFirst.Note ?? "");

                dialog.Asked.Clear();
                var second = WorkshopLoader.Run(options, dialog, null);

                Assert.Contains("888", dialog.Asked.ToArray());
                Assert.Equal(0, bulkCalls);
                Assert.Equal(1, second.Postponed);
                Assert.Equal("OLD", File.ReadAllText(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "888"), "Mod.dll")));
                var afterSecond = TrustStore.Load(trustPath, null).Get("888");
                Assert.Equal(TrustState.Approved, afterSecond.State);
                Assert.Equal("старый-хеш", afterSecond.Sha256);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Unwritable_trust_file_does_not_abort_run_and_is_logged()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                // Путь-родитель trust-файла (BepInEx\config) — это ФАЙЛ, записать нельзя.
                File.WriteAllText(Path.Combine(options.BepInExRoot, "config"), "not a directory");
                MakeWorkshop.MakeStaged(options.BepInExRoot, "999", "OLD");

                var logs = new List<string>();
                var summary = WorkshopLoader.Run(options, new FakeDialog(), logs.Add);

                Assert.NotNull(summary);
                Assert.Equal(1, summary.Removed);
                Assert.Contains(logs, l => l.Contains("trust"));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void GameFolder_new_approved_copies_data_but_not_dll_and_is_recorded_in_trust()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.GameFolderItem(options.WorkshopRoot, "700", @"GraveyardKeeper2_Data\Managed\Mod.dll", "DLL");
                MakeWorkshop.GameFolderItem(options.WorkshopRoot, "700", @"Languages\l\language.json", "{}");

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal(new[] { "700" }, dialog.Asked.ToArray());
                Assert.Equal("{}", File.ReadAllText(Path.Combine(root, "Languages", "l", "language.json")));
                Assert.False(File.Exists(Path.Combine(root, "GraveyardKeeper2_Data", "Managed", "Mod.dll")));
                var entry = TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("700");
                Assert.Equal(TrustState.Approved, entry.State);
                Assert.Equal(ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "700", "CopyToGameFolder")), entry.Sha256);
                Assert.Equal(1, summary.Approved);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void GameFolder_blocked_restores_the_game_folder()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.GameFolderItem(options.WorkshopRoot, "701", @"GraveyardKeeper2_Data\Managed\Mod.dll", "DLL");
                Directory.CreateDirectory(MakeWorkshop.BackupDir(options.BepInExRoot, "701"));
                File.WriteAllText(MakeWorkshop.BackupManifest(options.BepInExRoot, "701"), @"GraveyardKeeper2_Data\Managed\Mod.dll|0" + "\n");
                var gameFile = Path.Combine(root, "GraveyardKeeper2_Data", "Managed", "Mod.dll");
                Directory.CreateDirectory(Path.GetDirectoryName(gameFile));
                File.WriteAllText(gameFile, "DLL");
                string trust = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "701", Sha256 = "x", State = TrustState.Blocked, Title = "Bad" });
                store.Save(trust);

                var summary = WorkshopLoader.Run(options, new FakeDialog(), null);

                Assert.False(File.Exists(gameFile));
                Assert.Equal(1, summary.Blocked);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void GameFolder_removed_restores_the_game_folder()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                Directory.CreateDirectory(MakeWorkshop.BackupDir(options.BepInExRoot, "702"));
                File.WriteAllText(MakeWorkshop.BackupManifest(options.BepInExRoot, "702"), @"GraveyardKeeper2_Data\Managed\Old.dll|0" + "\n");
                var gameFile = Path.Combine(root, "GraveyardKeeper2_Data", "Managed", "Old.dll");
                Directory.CreateDirectory(Path.GetDirectoryName(gameFile));
                File.WriteAllText(gameFile, "OLD");
                string trust = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "702", Sha256 = "x", State = TrustState.Approved, Title = "Gone" });
                store.Save(trust);

                var summary = WorkshopLoader.Run(options, new FakeDialog(), null);

                Assert.False(File.Exists(gameFile));
                Assert.Equal(1, summary.Removed);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Backup_dir_id_counts_as_staged_for_migration()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.GameFolderItem(options.WorkshopRoot, "703", @"GraveyardKeeper2_Data\Managed\Mod.dll", "DLL");
                Directory.CreateDirectory(MakeWorkshop.BackupDir(options.BepInExRoot, "703"));
                File.WriteAllText(MakeWorkshop.BackupManifest(options.BepInExRoot, "703"), "x|0\n");

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal(new[] { "bulk:703" }, dialog.Asked.ToArray());
                Assert.Equal(1, summary.Migrated);
                Assert.Equal(TrustState.Approved,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("703").State);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void GameFolder_known_with_missing_manifest_reinstalls()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.GameFolderItem(options.WorkshopRoot, "704", @"GraveyardKeeper2_Data\Managed\data.bin", "DATA");
                string fp = ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "704", "CopyToGameFolder"));
                string trust = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "704", Sha256 = fp, State = TrustState.Approved, Title = "Mod" });
                store.Save(trust);

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Empty(dialog.Asked);
                Assert.True(File.Exists(Path.Combine(root, "GraveyardKeeper2_Data", "Managed", "data.bin")));
                Assert.True(File.Exists(MakeWorkshop.BackupManifest(options.BepInExRoot, "704")));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }
    }
}
