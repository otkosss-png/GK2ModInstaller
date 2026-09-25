using System;
using System.Collections.Generic;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class ModPromptTests
    {
        public ModPromptTests()
        {
            // Tests must not depend on the machine culture.
            LoaderText.Language = LoaderLanguage.En;
        }

        private static ModPrompt Sample(bool update = false) => new ModPrompt
        {
            Id = "3807023815",
            Title = "Better Auto Crafting",
            Version = "1.2",
            IsUpdate = update,
            Files = new List<string> { "BetterAutoCrafting.dll" },
            Findings = new List<Finding> { new Finding { Category = FindingCategory.Network, Detail = "System.Net.WebClient" } },
            Duplicates = new List<string> { "BetterAutoCrafting" }
        };

        [Fact]
        public void Text_for_new_mod_contains_title_id_findings_and_buttons_hint()
        {
            string text = Sample().Text("New Workshop mod");
            Assert.Contains("New Workshop mod", text);
            Assert.Contains("Better Auto Crafting", text);
            Assert.Contains("3807023815", text);
            Assert.Contains("network", text);
            Assert.Contains("installed manually", text);
            Assert.Contains("Cancel", text);
        }

        [Fact]
        public void Text_for_update_mentions_update_and_keeps_previous_version_hint()
        {
            string text = Sample(true).Text("Update of a Workshop mod");
            Assert.Contains("Update of a Workshop mod", text);
            Assert.Contains("previous approved version", text);
        }

        [Fact]
        public void Text_includes_target_when_set()
        {
            var prompt = Sample();
            prompt.Target = @"game folder (GraveyardKeeper2_Data\Managed etc.)";
            string text = prompt.Text("New Workshop mod");
            Assert.Contains("Target: ", text);
            Assert.Contains("game folder", text);
        }

        [Fact]
        public void Text_omits_target_line_when_not_set()
        {
            Assert.DoesNotContain("Target:", Sample().Text("New Workshop mod"));
        }

        [Fact]
        public void Text_without_findings_or_duplicates_says_clean()
        {
            var prompt = Sample();
            prompt.Findings = new List<Finding>();
            prompt.Duplicates = new List<string>();
            string text = prompt.Text("New Workshop mod");
            Assert.Contains("nothing suspicious found", text);
            Assert.DoesNotContain("manually", text);
        }

        [Fact]
        public void Text_in_russian_contains_russian_labels()
        {
            var prompt = Sample();
            prompt.Target = "BepInEx\\plugins";
            try
            {
                LoaderText.Language = LoaderLanguage.Ru;
                string text = prompt.Text("Новый мод из Workshop");
                Assert.Contains("Одобрить", text);
                Assert.Contains("Назначение:", text);
            }
            finally { LoaderText.Language = LoaderLanguage.En; }
        }

        [Fact]
        public void PendingList_writes_id_and_title_lines()
        {
            string path = Path.Combine(Path.GetTempPath(), "gk2pending_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                PendingList.Write(path, new[] { Sample() }, null);
                string text = File.ReadAllText(path);
                Assert.Contains("3807023815|Better Auto Crafting", text);
                Assert.Contains("#", text);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void PendingList_never_throws_on_bad_path()
        {
            // «Плохой путь» делаем так: на месте папки лежит файл — CreateDirectory упадёт.
            string blocker = Path.Combine(Path.GetTempPath(), "gk2blocker_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "x");
            try
            {
                var logs = new List<string>();
                PendingList.Write(Path.Combine(blocker, "y.txt"), new[] { Sample() }, logs.Add);
                Assert.Contains(logs, l => l.Contains("pending"));
            }
            finally { if (File.Exists(blocker)) File.Delete(blocker); }
        }
    }
}
