using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class TrustStoreTests
    {
        public TrustStoreTests()
        {
            LoaderText.Language = LoaderLanguage.En;
        }

        private static string TmpFile() => Path.Combine(Path.GetTempPath(), "gk2trust_" + Guid.NewGuid().ToString("N") + ".txt");

        [Fact]
        public void Missing_file_gives_empty_store()
        {
            var store = TrustStore.Load(TmpFile(), null);
            Assert.Null(store.Get("111"));
            Assert.Empty(store.All());
        }

        [Fact]
        public void Save_then_Load_roundtrips_with_header_and_note_pipes()
        {
            string path = TmpFile();
            try
            {
                var store = TrustStore.Load(path, null);
                store.Set(new TrustEntry { Id = "111", Sha256 = "abc", State = TrustState.Approved, Title = "My Mod", Note = "одобрено | вручную" });
                store.Set(new TrustEntry { Id = "222", Sha256 = "def", State = TrustState.Blocked, Title = "Bad", Note = "" });
                store.Save(path);

                string text = File.ReadAllText(path);
                Assert.Contains("#", text);

                var again = TrustStore.Load(path, null);
                var a = again.Get("111");
                Assert.Equal(TrustState.Approved, a.State);
                Assert.Equal("abc", a.Sha256);
                Assert.Equal("одобрено | вручную", a.Note);
                Assert.Equal(TrustState.Blocked, again.Get("222").State);
                Assert.Equal(2, again.All().Count);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Ask_state_is_default_for_unknown_and_parsed_unknown_words()
        {
            Assert.Equal(TrustState.Ask, TrustStore.ParseState("ask"));
            Assert.Equal(TrustState.Ask, TrustStore.ParseState("что-то"));
            Assert.Equal("yes", TrustStore.StateText(TrustState.Approved));
            Assert.Equal("no", TrustStore.StateText(TrustState.Blocked));
            Assert.Equal("ask", TrustStore.StateText(TrustState.Ask));
        }

        [Fact]
        public void Broken_lines_are_skipped_and_comments_kept()
        {
            string path = TmpFile();
            try
            {
                File.WriteAllText(path, "# мой комментарий\nмусор без разделителей\n333|hash|yes|T|\n");
                var logs = new System.Collections.Generic.List<string>();
                var store = TrustStore.Load(path, logs.Add);
                Assert.Null(store.Get("мусор без разделителей"));
                Assert.Equal(TrustState.Approved, store.Get("333").State);
                Assert.Contains(logs, l => l.Contains("skipped line"));

                store.Save(path);
                Assert.Contains("# мой комментарий", File.ReadAllText(path));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Remove_and_All_are_ordinal_sorted()
        {
            var store = TrustStore.Load(TmpFile(), null);
            store.Set(new TrustEntry { Id = "2", State = TrustState.Ask, Sha256 = "" });
            store.Set(new TrustEntry { Id = "10", State = TrustState.Ask, Sha256 = "" });
            Assert.Equal(new[] { "10", "2" }, new[] { store.All()[0].Id, store.All()[1].Id });
            Assert.True(store.Remove("2"));
            Assert.False(store.Remove("2"));
            Assert.Null(store.Get("2"));
        }
    }
}
