using System;
using System.Collections.Generic;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class PendingGameActionsTests
    {
        public PendingGameActionsTests()
        {
            LoaderText.Language = LoaderLanguage.En;
        }

        private static string NewDir()
        {
            var d = Path.Combine(Path.GetTempPath(), "gk2pg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        [Fact]
        public void Add_then_read_round_trips_and_dedupes()
        {
            string dir = NewDir();
            try
            {
                string path = Path.Combine(dir, PendingGameActions.FileName);
                PendingGameActions.Add(path, new PendingGameAction { Kind = PendingGameAction.DeleteKind, Rel = @"Managed\a.dll" }, null);
                PendingGameActions.Add(path, new PendingGameAction { Kind = PendingGameAction.DeleteKind, Rel = @"Managed\a.dll" }, null);
                PendingGameActions.Add(path, new PendingGameAction { Kind = PendingGameAction.RestoreKind, Rel = @"Languages\l.json", Id = "42" }, null);

                var list = PendingGameActions.Read(path, null);

                Assert.Equal(2, list.Count);
                Assert.Contains(list, a => a.Kind == PendingGameAction.DeleteKind && a.Rel == @"Managed\a.dll");
                Assert.Contains(list, a => a.Kind == PendingGameAction.RestoreKind && a.Rel == @"Languages\l.json" && a.Id == "42");
                string text = File.ReadAllText(path);
                Assert.Contains("delete|", text);
                Assert.Contains("restore|", text);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public void Read_skips_comments_and_malformed_lines()
        {
            string dir = NewDir();
            try
            {
                string path = Path.Combine(dir, PendingGameActions.FileName);
                File.WriteAllText(path, "# header\ndelete|\nrestore|x\nbogus|y\nrestore|ok|7\n");

                var logs = new List<string>();
                var list = PendingGameActions.Read(path, logs.Add);

                Assert.Single(list);
                Assert.Equal("ok", list[0].Rel);
                Assert.Equal("7", list[0].Id);
                Assert.NotEmpty(logs);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public void Write_of_empty_list_removes_the_file()
        {
            string dir = NewDir();
            try
            {
                string path = Path.Combine(dir, PendingGameActions.FileName);
                PendingGameActions.Write(path, new[] { new PendingGameAction { Kind = PendingGameAction.DeleteKind, Rel = "x.dll" } }, null);
                Assert.True(File.Exists(path));

                PendingGameActions.Write(path, new PendingGameAction[0], null);

                Assert.False(File.Exists(path));
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public void Read_of_missing_or_null_is_empty()
        {
            string missing = Path.Combine(Path.GetTempPath(), "gk2pg_missing_" + Guid.NewGuid().ToString("N"), "x.txt");
            Assert.Empty(PendingGameActions.Read(missing, null));
            Assert.Empty(PendingGameActions.Read(null, null));
        }
    }
}
