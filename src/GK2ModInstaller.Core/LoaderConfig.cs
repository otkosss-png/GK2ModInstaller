using System;
using System.IO;
using System.Text;

namespace GK2ModInstaller.Core
{
    // Language override file: <BepInEx>\config\GK2_WorkshopLoader.txt
    //   language = auto | en | ru
    // The file is created on first run. Parsing never throws: any problem falls back to auto.
    public static class LoaderConfig
    {
        public const string FileName = "GK2_WorkshopLoader.txt";

        private const string DefaultContent =
            "# GK2 Workshop Loader — interface language.\r\n" +
            "# language = auto | en | ru\r\n" +
            "#   auto — follow the system language (Russian UI -> Russian, otherwise English)\r\n" +
            "#   en   — always English\r\n" +
            "#   ru   — always Russian\r\n" +
            "language = auto\r\n";

        public static LoaderLanguage Resolve(string configFilePath, bool systemIsRussian, Action<string> log)
        {
            var auto = systemIsRussian ? LoaderLanguage.Ru : LoaderLanguage.En;
            try
            {
                if (string.IsNullOrEmpty(configFilePath)) return auto;

                if (!File.Exists(configFilePath))
                {
                    var dir = Path.GetDirectoryName(configFilePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(configFilePath, DefaultContent, new UTF8Encoding(false));
                    return auto;
                }

                foreach (var raw in File.ReadAllLines(configFilePath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    if (!string.Equals(key, "language", StringComparison.OrdinalIgnoreCase)) continue;
                    var value = line.Substring(eq + 1).Trim();
                    if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase)) return LoaderLanguage.En;
                    if (string.Equals(value, "ru", StringComparison.OrdinalIgnoreCase)) return LoaderLanguage.Ru;
                    if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase)) return auto;
                    log?.Invoke("loader config: unknown language value '" + value + "' — using auto");
                    return auto;
                }
                return auto;
            }
            catch (Exception ex)
            {
                log?.Invoke("loader config: not read (" + ex.Message + ") — using auto");
                return auto;
            }
        }
    }
}
