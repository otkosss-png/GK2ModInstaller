using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using GK2ModInstaller.Core;
using Mono.Cecil;

namespace GK2ModInstaller.Patcher
{
    public static class WorkshopAutoLoaderPatcher
    {
        private const string WorkshopId = "4358690";

        public static IEnumerable<string> TargetDLLs { get { yield return "Assembly-CSharp.dll"; } }

        public static void Patch(AssemblyDefinition assembly)
        {
            var log = Logger.CreateLogSource("GK2.WorkshopLoader");
            string bep = Paths.BepInExRootPath;
            string steamapps = FindSteamAppsRoot(Directory.GetParent(bep)?.FullName);
            string workshop = steamapps == null ? null : Path.Combine(steamapps, "workshop", "content", WorkshopId);
            string acf = steamapps == null ? null : Path.Combine(steamapps, "workshop", "appworkshop_" + WorkshopId + ".acf");
            log.LogInfo("Workshop root: " + (workshop ?? "<не найден>"));

            var options = new LoaderOptions { WorkshopRoot = workshop, WorkshopAcfPath = acf, BepInExRoot = bep };
            try
            {
                WorkshopLoader.Run(options, new Win32Dialog(), log.LogInfo);
            }
            catch (System.Exception ex)
            {
                log.LogError("Автозагрузка Workshop упала: " + ex);
            }
        }

        private static string FindSteamAppsRoot(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir)) return null;
            var dir = new DirectoryInfo(gameDir);
            while (dir != null && !dir.Name.Equals("steamapps", System.StringComparison.OrdinalIgnoreCase))
                dir = dir.Parent;
            return dir == null ? null : dir.FullName;
        }
    }
}
