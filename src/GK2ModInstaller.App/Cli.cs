using System;
using System.IO;
using System.Reflection;
using GK2ModInstaller.Core;

namespace GK2ModInstaller.App
{
    internal static class Cli
    {
        public static void Run(string mode, string gameDir)
        {
            string logPath = Path.Combine(Path.GetTempPath(), "gk2installer_cli.log");
            Action<string> log = s =>
            {
                try { File.AppendAllText(logPath, s + Environment.NewLine); } catch { }
            };
            try { File.WriteAllText(logPath, "GK2ModInstaller CLI " + mode + " -> " + gameDir + Environment.NewLine); } catch { }

            if (!GameLocator.ValidateGameDir(gameDir)) { log(LoaderText.Get("InstBadFolder")); return; }

            if (mode == "--uninstall" || mode == "--uninstall-keep")
            {
                bool keep = mode == "--uninstall-keep";
                BepInExInstaller.Uninstall(gameDir, log, keep);
                log(LoaderText.Get(keep ? "InstCliRemovedKeep" : "InstCliRemoved"));
                return;
            }

            using (var bep = Open("BepInEx_win_x64_5.4.23.5.zip"))
            using (var fw = Open("GK2.Framework.zip"))
            using (var patcher = Open("GK2.WorkshopAutoLoader.dll"))
            using (var loader = Open("GK2.WorkshopLoader.dll"))
                BepInExInstaller.Install(gameDir, bep, fw, patcher, loader, false, log);

            var problems = BepInExInstaller.Verify(gameDir, true);
            foreach (var p in problems) log(LoaderText.Get("InstProblem") + p);
            if (problems.Count == 0) log(LoaderText.Get("InstCliInstalledTo") + gameDir);
        }

        private static Stream Open(string name)
        {
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (s != null) return s;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            return File.Exists(path) ? File.OpenRead(path) : null;
        }
    }
}
