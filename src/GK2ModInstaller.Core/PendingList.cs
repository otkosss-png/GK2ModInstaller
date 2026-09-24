using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GK2ModInstaller.Core
{
    public static class PendingList
    {
        public static void Write(string path, IEnumerable<ModPrompt> mods, Action<string> log)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine("# GK2 Workshop Loader: моды, ожидающие решения. Правила — в GK2_WorkshopLoader.trust.txt.");
                if (mods != null)
                    foreach (var m in mods)
                        sb.AppendLine((m.Id ?? "?") + "|" + (m.Title ?? ""));
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                log?.Invoke("pending: файл не записан (" + ex.Message + ")");
            }
        }
    }
}
