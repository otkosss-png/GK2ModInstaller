using System.Collections.Generic;
using System.IO;

namespace GK2ModInstaller.Core
{
    public static class GameLocator
    {
        public const string GameFolderName = "Graveyard Keeper 2";
        public const string GameExeName = "GraveyardKeeper2.exe";

        public static bool ValidateGameDir(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;
            return File.Exists(Path.Combine(dir, GameExeName))
                && File.Exists(Path.Combine(dir, "UnityPlayer.dll"))
                && File.Exists(Path.Combine(dir, "GraveyardKeeper2_Data", "Managed", "Assembly-CSharp.dll"));
        }

        public static string FindGameInLibrary(string libraryRoot)
        {
            if (string.IsNullOrWhiteSpace(libraryRoot)) return null;
            var candidate = Path.Combine(libraryRoot, "steamapps", "common", GameFolderName);
            return ValidateGameDir(candidate) ? Path.GetFullPath(candidate) : null;
        }

        public static string FindGameInSteamRoot(string steamRoot)
        {
            if (string.IsNullOrWhiteSpace(steamRoot)) return null;
            var roots = new List<string> { steamRoot };
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            try { if (File.Exists(vdf)) roots.AddRange(VdfParser.ParseLibraryPaths(File.ReadAllText(vdf))); }
            catch { }
            foreach (var r in roots)
            {
                var found = FindGameInLibrary(r);
                if (found != null) return found;
            }
            return null;
        }
    }
}
