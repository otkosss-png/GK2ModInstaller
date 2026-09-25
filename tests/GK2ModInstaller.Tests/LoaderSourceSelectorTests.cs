using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class LoaderSourceSelectorTests
    {
        private static string TmpDll(string name)
        {
            var dir = Path.Combine(Path.GetTempPath(), "gk2sel_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, name);
            File.WriteAllText(f, "x");
            return f;
        }

        [Fact]
        public void Item_copy_wins_when_both_exist()
        {
            string item = TmpDll("item.dll");
            string local = TmpDll("local.dll");
            Assert.Equal(item, LoaderSourceSelector.Select(item, local));
            Assert.Equal("item", LoaderSourceSelector.KindOf(item, item));
        }

        [Fact]
        public void Local_fallback_is_used_when_item_is_missing()
        {
            string local = TmpDll("local.dll");
            string item = Path.Combine(Path.GetDirectoryName(local), "nope.dll");
            Assert.Equal(local, LoaderSourceSelector.Select(item, local));
            Assert.Equal("local", LoaderSourceSelector.KindOf(local, item));
        }

        [Fact]
        public void Null_when_nothing_exists()
        {
            string missing = Path.Combine(Path.GetTempPath(), "gk2none_" + Guid.NewGuid().ToString("N"), "x.dll");
            Assert.Null(LoaderSourceSelector.Select(missing, null));
            Assert.Null(LoaderSourceSelector.Select(null, null));
            Assert.Null(LoaderSourceSelector.Select("", ""));
        }

        [Fact]
        public void Describe_mentions_paths_and_hint_when_nothing_found()
        {
            string local = TmpDll("local.dll");
            string item = Path.Combine(Path.GetDirectoryName(local), "nope.dll");
            string ok = LoaderSourceSelector.Describe(local, item, local);
            Assert.Contains(local, ok);
            Assert.Contains("local", ok);

            string fail = LoaderSourceSelector.Describe(null, item, null);
            Assert.Contains("3807406994", fail);
            Assert.Contains("GK2 Mod Installer", fail);
        }
    }
}
