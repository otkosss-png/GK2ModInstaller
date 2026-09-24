using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GK2ModInstaller.Core
{
    public static class VdfParser
    {
        private static readonly Regex PathLine =
            new Regex("\"path\"\\s+\"(?<v>(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

        public static IReadOnlyList<string> ParseLibraryPaths(string vdfText)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(vdfText)) return result;
            foreach (Match m in PathLine.Matches(vdfText))
            {
                var path = m.Groups["v"].Value.Replace("\\\\", "\\");
                if (!string.IsNullOrWhiteSpace(path) && !result.Contains(path)) result.Add(path);
            }
            return result;
        }
    }
}
