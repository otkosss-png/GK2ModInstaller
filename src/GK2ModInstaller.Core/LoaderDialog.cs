using System.Collections.Generic;
using System.Text;

namespace GK2ModInstaller.Core
{
    public enum ConsentAnswer { Approve, Deny, Later }
    public enum BulkAnswer { All, AskEach, Later }

    // Текст промпта собирается здесь (в Core) — так его можно тестировать без игры.
    public sealed class ModPrompt
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public bool IsUpdate { get; set; }
        public IReadOnlyList<string> Files { get; set; }
        public string Target { get; set; }
        public IReadOnlyList<Finding> Findings { get; set; }
        public IReadOnlyList<string> Duplicates { get; set; }

        public string Text(string header)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            sb.AppendLine();
            sb.Append(Title ?? Id);
            if (!string.IsNullOrEmpty(Version)) sb.Append(" v").Append(Version);
            sb.Append(" (id ").Append(Id).Append(')').AppendLine();
            if (Files != null && Files.Count > 0) sb.AppendLine("Файлы: " + string.Join(", ", Files));
            if (!string.IsNullOrEmpty(Target)) sb.AppendLine("Назначение: " + Target);

            if (Findings != null && Findings.Count > 0)
                sb.AppendLine("Проверка кода: " + string.Join(", ", Categories()));
            else
                sb.AppendLine("Проверка кода: ничего подозрительного не найдено");

            if (Duplicates != null && Duplicates.Count > 0)
                sb.AppendLine("Тот же мод уже стоит вручную в BepInEx\\plugins: " + string.Join(", ", Duplicates));

            sb.AppendLine();
            if (IsUpdate)
                sb.AppendLine("Если ответить Cancel — прежняя одобренная версия продолжит работать.");
            sb.AppendLine("Мод запускается с правами игры (файлы, интернет). Одобрить?");
            sb.AppendLine();
            sb.AppendLine("Yes — одобрить · No — заблокировать навсегда · Cancel — спросить позже");
            return sb.ToString();
        }

        private List<string> Categories()
        {
            var list = new List<string>();
            foreach (var f in Findings)
            {
                string ru;
                switch (f.Category)
                {
                    case FindingCategory.Network: ru = "сеть"; break;
                    case FindingCategory.Process: ru = "запуск программ"; break;
                    case FindingCategory.FileDelete: ru = "удаление/перемещение файлов"; break;
                    case FindingCategory.CodeLoad: ru = "загрузка кода"; break;
                    case FindingCategory.Registry: ru = "реестр"; break;
                    default: ru = "нативный код (DllImport)"; break;
                }
                if (!list.Contains(ru)) list.Add(ru);
            }
            return list;
        }
    }

    public interface IDialog
    {
        ConsentAnswer Ask(ModPrompt prompt);
        BulkAnswer AskBulkTrust(IReadOnlyList<ModPrompt> mods);
        void Warn(string text);
    }
}
