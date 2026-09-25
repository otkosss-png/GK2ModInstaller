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
            if (Files != null && Files.Count > 0) sb.AppendLine(LoaderText.FilesPrefix + string.Join(", ", Files));
            if (!string.IsNullOrEmpty(Target)) sb.AppendLine(LoaderText.TargetPrefix + Target);

            if (Findings != null && Findings.Count > 0)
                sb.AppendLine(LoaderText.CodeCheckPrefix + string.Join(", ", Categories()));
            else
                sb.AppendLine(LoaderText.CodeCheckPrefix + LoaderText.CodeCheckClean);

            if (Duplicates != null && Duplicates.Count > 0)
                sb.AppendLine(LoaderText.DuplicateLine + string.Join(", ", Duplicates));

            sb.AppendLine();
            if (IsUpdate)
                sb.AppendLine(LoaderText.UpdateHint);
            sb.AppendLine(LoaderText.ApproveQuestion);
            sb.AppendLine();
            sb.AppendLine(LoaderText.ButtonHint);
            return sb.ToString();
        }

        private List<string> Categories()
        {
            var list = new List<string>();
            foreach (var f in Findings)
            {
                string name;
                switch (f.Category)
                {
                    case FindingCategory.Network: name = LoaderText.CategoryNetwork; break;
                    case FindingCategory.Process: name = LoaderText.CategoryProcess; break;
                    case FindingCategory.FileDelete: name = LoaderText.CategoryFileDelete; break;
                    case FindingCategory.CodeLoad: name = LoaderText.CategoryCodeLoad; break;
                    case FindingCategory.Registry: name = LoaderText.CategoryRegistry; break;
                    default: name = LoaderText.CategoryNative; break;
                }
                if (!list.Contains(name)) list.Add(name);
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
