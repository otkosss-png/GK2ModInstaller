using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class VdfParserTests
    {
        [Fact]
        public void Parses_library_paths()
        {
            string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"E:\\\\SteamLibrary\"\n\t}\n}";
            var paths = VdfParser.ParseLibraryPaths(vdf).ToArray();
            Assert.Equal(new[] { "C:\\Program Files (x86)\\Steam", "E:\\SteamLibrary" }, paths);
        }

        [Fact]
        public void Empty_text_gives_empty_list()
        {
            Assert.Empty(VdfParser.ParseLibraryPaths(""));
        }
    }
}
