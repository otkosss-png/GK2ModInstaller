using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GK2ModInstaller.Core
{
    // Отпечаток версии мода: SHA-256 по содержимому всех файлов папки плагинов.
    // Конфиги (.cfg) исключены: они не перезаписываются при синке и правятся игроком.
    public static class ModFingerprint
    {
        public static string Compute(string pluginsDir)
        {
            if (string.IsNullOrEmpty(pluginsDir) || !Directory.Exists(pluginsDir)) return "";
            var files = new List<KeyValuePair<string, string>>();
            foreach (var full in Directory.GetFiles(pluginsDir, "*", SearchOption.AllDirectories))
            {
                var rel = full.Substring(pluginsDir.Length).TrimStart('\\', '/').Replace('\\', '/').ToLowerInvariant();
                if (rel.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)) continue;
                if (rel == "config" || rel.StartsWith("config/", StringComparison.Ordinal) || rel.Contains("/config/")) continue;
                files.Add(new KeyValuePair<string, string>(rel, full));
            }
            files.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder();
                foreach (var f in files)
                {
                    sb.Append(f.Key).Append('\n');
                    using (var stream = File.OpenRead(f.Value)) sb.Append(Hex(sha.ComputeHash(stream)));
                    sb.Append('\n');
                }
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
            }
        }

        private static string Hex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
