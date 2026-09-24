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
            var log = Logger.CreateLogSource("GK2.WorkshopAutoLoader");
            string bep = Paths.BepInExRootPath;
            string gameDir = Directory.GetParent(bep)?.FullName;
            string workshop = FindWorkshopRoot(gameDir);
            log.LogInfo("Workshop root: " + (workshop ?? "<не найден>"));
            WorkshopSync.RunOnce(workshop, bep, log.LogInfo);
        }

        private static string FindWorkshopRoot(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir)) return null;
            var dir = new DirectoryInfo(gameDir);
            while (dir != null && !dir.Name.Equals("steamapps", System.StringComparison.OrdinalIgnoreCase))
                dir = dir.Parent;
            return dir == null ? null : Path.Combine(dir.FullName, "workshop", "content", WorkshopId);
        }
    }
}
