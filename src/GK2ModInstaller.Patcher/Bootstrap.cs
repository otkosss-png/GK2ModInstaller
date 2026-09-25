using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using GK2ModInstaller.Core;

namespace GK2ModInstaller.Patcher
{
    // Заглушка: загружает настоящий загрузчик из Workshop-айтема (или локального фолбэка) и вызывает Entry.Run.
    // Заморожена — вся логика живёт в GK2.WorkshopLoader.dll, обновляемом подпиской.
    internal static class Bootstrap
    {
        private const string WorkshopAppId = "4358690";
        private const string LoaderFileName = "GK2.WorkshopLoader.dll";

        internal static void Run()
        {
            var log = Logger.CreateLogSource("GK2.WorkshopLoader.Bootstrap");
            try
            {
                string bepInExRoot = Paths.BepInExRootPath;
                string gameRoot = Directory.GetParent(bepInExRoot)?.FullName;
                string steamapps = FindSteamAppsRoot(gameRoot);
                string workshopRoot = steamapps == null ? null : Path.Combine(steamapps, "workshop", "content", WorkshopAppId);
                string acfPath = steamapps == null ? null : Path.Combine(steamapps, "workshop", "appworkshop_" + WorkshopAppId + ".acf");

                string itemLoader = string.IsNullOrEmpty(workshopRoot)
                    ? null
                    : Path.Combine(workshopRoot, LoaderSourceSelector.LoaderItemId, "Loader", LoaderFileName);
                string localLoader = Path.Combine(bepInExRoot, LoaderFileName);

                string chosen = LoaderSourceSelector.Select(itemLoader, localLoader);
                log.LogInfo(LoaderSourceSelector.Describe(chosen, itemLoader, localLoader));
                if (chosen == null) return;

                ResolveDependencies(bepInExRoot);

                var assembly = Assembly.Load(File.ReadAllBytes(chosen));
                var entryType = assembly.GetType("GK2ModInstaller.Loader.Entry", throwOnError: false);
                var run = entryType?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                if (run == null)
                {
                    log.LogError("загрузчик: в " + chosen + " нет GK2ModInstaller.Loader.Entry.Run — обновите айтем");
                    return;
                }

                run.Invoke(null, new object[]
                {
                    bepInExRoot, gameRoot, workshopRoot, acfPath, LoaderSourceSelector.KindOf(chosen, itemLoader)
                });
            }
            catch (Exception ex)
            {
                log.LogError("загрузчик: ошибка запуска — " + ex);
            }
        }

        // Загрузчик грузится из байтов, поэтому его зависимости (Mono.Cecil) надо сделать резолвимыми.
        private static void ResolveDependencies(string bepInExRoot)
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    try
                    {
                        var simpleName = new AssemblyName(args.Name).Name + ".dll";
                        var path = Path.Combine(bepInExRoot, "core", simpleName);
                        return File.Exists(path) ? Assembly.Load(File.ReadAllBytes(path)) : null;
                    }
                    catch { return null; }
                };
                var cecil = Path.Combine(bepInExRoot, "core", "Mono.Cecil.dll");
                if (File.Exists(cecil)) Assembly.Load(File.ReadAllBytes(cecil));
            }
            catch { }
        }

        private static string FindSteamAppsRoot(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir)) return null;
            var dir = new DirectoryInfo(gameDir);
            while (dir != null && !dir.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase)) dir = dir.Parent;
            return dir == null ? null : dir.FullName;
        }
    }
}
