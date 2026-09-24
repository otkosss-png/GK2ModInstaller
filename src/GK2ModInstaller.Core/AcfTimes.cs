using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GK2ModInstaller.Core
{
    // Минимальный разбор appworkshop_<appid>.acf: достаём "timeupdated" для каждого id айтема.
    // Нужен только для справочного лога «у мода доступно обновление».
    public static class AcfTimes
    {
        private static readonly Regex ItemBlock = new Regex("^\\s*\"(?<id>\\d+)\"\\s*\\{?\\s*$", RegexOptions.Compiled);
        private static readonly Regex TimeUpdated = new Regex("^\\s*\"timeupdated\"\\s*\"(?<t>\\d+)\"", RegexOptions.Compiled);
        private static readonly Regex Closing = new Regex("^\\s*\\}\\s*$", RegexOptions.Compiled);

        public static Dictionary<string, long> Parse(string acfText)
        {
            var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(acfText)) return map;

            string currentId = null;
            foreach (var raw in acfText.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (currentId == null)
                {
                    var m = ItemBlock.Match(line);
                    if (m.Success) currentId = m.Groups["id"].Value;
                    continue;
                }

                var t = TimeUpdated.Match(line);
                if (t.Success)
                {
                    long value;
                    if (long.TryParse(t.Groups["t"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                        map[currentId] = value;
                    currentId = null;
                    continue;
                }

                if (Closing.IsMatch(line)) currentId = null;
            }
            return map;
        }
    }
}
