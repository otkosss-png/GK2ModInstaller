using System.IO;
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

            // Resolve the interface language before anything user-visible is written.
            LoaderText.Language = LoaderConfig.Resolve(
                Path.Combine(bepInExRoot, "config", LoaderConfig.FileName),
                LoaderText.DetectFor(System.Globalization.CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName) == LoaderLanguage.Ru,
                log.LogInfo);

            log.LogInfo(string.Format(LoaderText.StartupFormat, typeof(Entry).Assembly.GetName().Version, source));
            WorkshopLoader.Run(
                new LoaderOptions { WorkshopRoot = workshopRoot, WorkshopAcfPath = acfPath, BepInExRoot = bepInExRoot },
                new Win32Dialog(), log.LogInfo);
        }
    }
}
