using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class LoaderTextTests
    {
        private static bool HasCyrillic(string text)
            => text != null && text.Any(c => c >= 0x0400 && c <= 0x04FF);

        [Theory]
        [InlineData("ru", LoaderLanguage.Ru)]
        [InlineData("RU", LoaderLanguage.Ru)]
        [InlineData("ru-RU", LoaderLanguage.En)] // DetectFor expects a two-letter name, not a tag
        [InlineData("en", LoaderLanguage.En)]
        [InlineData("de", LoaderLanguage.En)]
        [InlineData("", LoaderLanguage.En)]
        [InlineData(null, LoaderLanguage.En)]
        public void DetectFor_maps_two_letter_language_names(string name, LoaderLanguage expected)
        {
            Assert.Equal(expected, LoaderText.DetectFor(name));
        }

        [Fact]
        public void AutoDetect_follows_current_ui_culture()
        {
            var original = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
                LoaderText.Language = LoaderLanguage.En;
                LoaderText.AutoDetect();
                Assert.Equal(LoaderLanguage.Ru, LoaderText.Language);

                CultureInfo.CurrentUICulture = new CultureInfo("en-US");
                LoaderText.AutoDetect();
                Assert.Equal(LoaderLanguage.En, LoaderText.Language);
            }
            finally
            {
                CultureInfo.CurrentUICulture = original;
                LoaderText.Language = LoaderLanguage.En;
            }
        }

        [Fact]
        public void Every_string_exists_in_both_languages_and_english_has_no_cyrillic()
        {
            Assert.NotEmpty(LoaderText.All);
            foreach (var pair in LoaderText.All)
            {
                Assert.False(string.IsNullOrEmpty(pair.Value.En), pair.Key + ": no English text");
                Assert.False(string.IsNullOrEmpty(pair.Value.Ru), pair.Key + ": no Russian text");
                Assert.False(HasCyrillic(pair.Value.En), pair.Key + ": English text contains Cyrillic: " + pair.Value.En);
            }
        }

        [Fact]
        public void Switching_language_switches_mod_prompt_output()
        {
            var prompt = new ModPrompt
            {
                Id = "1",
                Title = "T",
                Findings = new List<Finding>(),
                Duplicates = new List<string>()
            };
            try
            {
                LoaderText.Language = LoaderLanguage.En;
                string en = prompt.Text("H");
                LoaderText.Language = LoaderLanguage.Ru;
                string ru = prompt.Text("H");

                Assert.Contains("Approve", en);
                Assert.Contains("Одобрить", ru);
                Assert.NotEqual(en, ru);
            }
            finally { LoaderText.Language = LoaderLanguage.En; }
        }
    }
}
