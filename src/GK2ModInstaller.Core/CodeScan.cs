using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace GK2ModInstaller.Core
{
    public enum FindingCategory { Network, Process, FileDelete, CodeLoad, Registry, Native }

    public sealed class Finding
    {
        public FindingCategory Category { get; set; }
        public string Detail { get; set; }
    }

    // Справочный статический скан: ищем по ссылкам на типы/члены и по P/Invoke.
    // Ничего не блокируем — результаты только показываются в диалоге и пишутся в лог.
    public static class CodeScan
    {
        private struct Rule
        {
            public FindingCategory Category;
            public string Prefix;
        }

        private static readonly Rule[] TypeRules =
        {
            new Rule { Category = FindingCategory.Network, Prefix = "System.Net.Http.HttpClient" },
            new Rule { Category = FindingCategory.Network, Prefix = "System.Net.WebClient" },
            new Rule { Category = FindingCategory.Network, Prefix = "System.Net.HttpWebRequest" },
            new Rule { Category = FindingCategory.Network, Prefix = "System.Net.WebRequest" },
            new Rule { Category = FindingCategory.Network, Prefix = "System.Net.Sockets.Socket" },
            new Rule { Category = FindingCategory.Network, Prefix = "System.Net.Dns" },
            new Rule { Category = FindingCategory.Process, Prefix = "System.Diagnostics.Process" },
            new Rule { Category = FindingCategory.CodeLoad, Prefix = "System.Reflection.Emit" },
            new Rule { Category = FindingCategory.CodeLoad, Prefix = "System.Runtime.Loader.AssemblyLoadContext" },
            new Rule { Category = FindingCategory.Registry, Prefix = "Microsoft.Win32.Registry" },
        };

        private static readonly Rule[] MemberRules =
        {
            new Rule { Category = FindingCategory.CodeLoad, Prefix = "System.Reflection.Assembly::Load" },
            new Rule { Category = FindingCategory.CodeLoad, Prefix = "System.AppDomain::Load" },
            new Rule { Category = FindingCategory.FileDelete, Prefix = "System.IO.File::Delete" },
            new Rule { Category = FindingCategory.FileDelete, Prefix = "System.IO.Directory::Delete" },
            new Rule { Category = FindingCategory.FileDelete, Prefix = "System.IO.File::Move" },
            new Rule { Category = FindingCategory.FileDelete, Prefix = "System.IO.File::Replace" },
        };

        public static IReadOnlyList<Finding> Scan(string dllPath)
        {
            var found = new List<Finding>();
            if (string.IsNullOrEmpty(dllPath) || !System.IO.File.Exists(dllPath)) return found;
            try
            {
                using (var asm = AssemblyDefinition.ReadAssembly(dllPath))
                {
                    var module = asm.MainModule;
                    var seen = new HashSet<string>();
                    foreach (var typeRef in module.GetTypeReferences())
                        Match(found, seen, TypeRules, typeRef.FullName);
                    foreach (var memberRef in module.GetMemberReferences())
                    {
                        string name = memberRef.DeclaringType != null
                            ? memberRef.DeclaringType.FullName + "::" + memberRef.Name
                            : memberRef.Name;
                        Match(found, seen, MemberRules, name);
                    }
                    foreach (var type in module.Types) ScanType(type, found, seen);
                }
            }
            catch (Exception)
            {
                // не сборка — предупреждений нет
            }
            return found;
        }

        private static void ScanType(TypeDefinition type, List<Finding> found, HashSet<string> seen)
        {
            foreach (var method in type.Methods)
            {
                if (!method.IsPInvokeImpl && !method.HasPInvokeInfo) continue;
                string detail = type.FullName + "::" + method.Name;
                if (seen.Add("Native:" + detail)) found.Add(new Finding { Category = FindingCategory.Native, Detail = detail });
            }
            foreach (var nested in type.NestedTypes) ScanType(nested, found, seen);
        }

        private static void Match(List<Finding> found, HashSet<string> seen, Rule[] rules, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var rule in rules)
            {
                if (!name.StartsWith(rule.Prefix, StringComparison.Ordinal)) continue;
                if (!seen.Add(rule.Category + ":" + rule.Prefix)) return;
                found.Add(new Finding { Category = rule.Category, Detail = name });
                return;
            }
        }
    }
}
