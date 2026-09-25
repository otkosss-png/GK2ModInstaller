using System;
using System.IO;

namespace GK2ModInstaller.Core
{
    // Какую сборку загрузчика грузить: приоритет — копия в Workshop-айтеме (обновляется подпиской),
    // иначе локальная копия, которую положил инсталлятор.
    public static class LoaderSourceSelector
    {
        public const string ItemSource = "item";
        public const string LocalSource = "local";
        public const string LoaderItemId = "3807406994";

        public static string Select(string itemLoaderPath, string localLoaderPath)
        {
            if (!string.IsNullOrEmpty(itemLoaderPath) && File.Exists(itemLoaderPath)) return itemLoaderPath;
            if (!string.IsNullOrEmpty(localLoaderPath) && File.Exists(localLoaderPath)) return localLoaderPath;
            return null;
        }

        public static string KindOf(string chosenPath, string itemLoaderPath)
        {
            return !string.IsNullOrEmpty(itemLoaderPath) && string.Equals(chosenPath, itemLoaderPath, StringComparison.OrdinalIgnoreCase)
                ? ItemSource
                : LocalSource;
        }

        public static string Describe(string chosenPath, string itemLoaderPath, string localLoaderPath)
        {
            if (string.IsNullOrEmpty(chosenPath))
                return "загрузчик не найден: ни в айтеме (" + (itemLoaderPath ?? "нет") + "), ни локально (" +
                       (localLoaderPath ?? "нет") + ") — подпишитесь на айтем " + LoaderItemId + " или запустите GK2 Mod Installer";
            return "загрузчик: " + chosenPath + " (источник: " + KindOf(chosenPath, itemLoaderPath) + ")";
        }
    }
}
