using BepInEx.Logging;
using GK2ModInstaller.Core;

namespace GK2ModInstaller.Loader
{
    // ЗАМОРОЖЕННЫЙ контракт: заглушка в BepInEx\patchers вызывает именно этот метод.
    // Менять сигнатуру можно только вместе с заглушкой (защищено тестом EntryContractTests).
    public static class Entry
    {
        public static void Run(string bepInExRoot, string gameRoot, string workshopRoot, string acfPath, string source)
        {
            var log = Logger.CreateLogSource("GK2.WorkshopLoader");
            log.LogInfo($"загрузчик {typeof(Entry).Assembly.GetName().Version} (источник: {source})");
            WorkshopLoader.Run(
                new LoaderOptions { WorkshopRoot = workshopRoot, WorkshopAcfPath = acfPath, BepInExRoot = bepInExRoot },
                new Win32Dialog(), log.LogInfo);
        }
    }
}
