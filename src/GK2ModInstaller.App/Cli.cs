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

            if (!GameLocator.ValidateGameDir(gameDir)) { log("Неверная папка игры."); return; }

            if (mode == "--uninstall")
            {
                BepInExInstaller.Uninstall(gameDir, log);
                log("OK: удалено.");
                return;
            }

            using (var bep = Open("BepInEx_win_x64_5.4.23.5.zip"))
            using (var fw = Open("GK2.Framework.zip"))
                BepInExInstaller.Install(gameDir, bep, fw, false, log);

            var problems = BepInExInstaller.Verify(gameDir);
            foreach (var p in problems) log("Проблема: " + p);
            if (problems.Count == 0) log("OK: установлено в " + gameDir);
        }

        private static Stream Open(string name)
            => Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
    }
}
