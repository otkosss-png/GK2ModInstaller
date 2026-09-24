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
    }
}
