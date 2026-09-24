using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class GameLocatorTests
    {
        private static string MakeGameDir(string libraryRoot)
        {
            string game = Path.Combine(libraryRoot, "steamapps", "common", GameLocator.GameFolderName);
            Directory.CreateDirectory(Path.Combine(game, "GraveyardKeeper2_Data", "Managed"));
            File.WriteAllText(Path.Combine(game, GameLocator.GameExeName), "x");
            File.WriteAllText(Path.Combine(game, "UnityPlayer.dll"), "x");
            File.WriteAllText(Path.Combine(game, "GraveyardKeeper2_Data", "Managed", "Assembly-CSharp.dll"), "x");
            return game;
        }

        [Fact]
        public void ValidateGameDir_true_for_valid_folder()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "gk2loc_" + Guid.NewGuid().ToString("N"));
            string game = MakeGameDir(tmp);
            try { Assert.True(GameLocator.ValidateGameDir(game)); }
            finally { Directory.Delete(tmp, true); }
        }

        [Fact]
        public void ValidateGameDir_false_for_random_folder()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "gk2loc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try { Assert.False(GameLocator.ValidateGameDir(tmp)); }
            finally { Directory.Delete(tmp, true); }
        }

        [Fact]
        public void FindGameInLibrary_finds_game()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "gk2loc_" + Guid.NewGuid().ToString("N"));
            string game = MakeGameDir(tmp);
            try { Assert.Equal(Path.GetFullPath(game), GameLocator.FindGameInLibrary(tmp)); }
            finally { Directory.Delete(tmp, true); }
        }
    }
}
