using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace GK2ModInstaller.Core
{
    public sealed class AssemblyInfo
    {
        public string AssemblyName { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
    }

    public static class AssemblyInfoReader
    {
        public static AssemblyInfo TryRead(string dllPath, Action<string> log)
        {
            var fallback = new AssemblyInfo
            {
                AssemblyName = Path.GetFileNameWithoutExtension(dllPath),
                Title = Path.GetFileNameWithoutExtension(dllPath),
                Version = ""
            };
            try
            {
                using (var asm = AssemblyDefinition.ReadAssembly(dllPath))
                {
                    fallback.AssemblyName = asm.Name.Name;
                    fallback.Version = asm.Name.Version != null ? asm.Name.Version.ToString() : "";
                    foreach (var attr in asm.CustomAttributes)
                    {
                        if (attr.ConstructorArguments.Count == 0) continue;
                        var value = attr.ConstructorArguments[0].Value as string;
                        if (string.IsNullOrEmpty(value)) continue;
                        string name = attr.AttributeType != null ? attr.AttributeType.Name : "";
                        if ((name == "AssemblyTitleAttribute" || name == "AssemblyProductAttribute")
                            && fallback.Title == Path.GetFileNameWithoutExtension(dllPath))
                            fallback.Title = value;
                        else if (name == "AssemblyInformationalVersionAttribute")
                            fallback.Version = value;
                        else if (name == "AssemblyFileVersionAttribute" && string.IsNullOrEmpty(fallback.Version))
                            fallback.Version = value;
                    }
                    return fallback;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("мод: не сборка, метаданные не прочитаны — " + Path.GetFileName(dllPath) + " (" + ex.Message + ")");
                return fallback;
            }
        }
    }

    public enum WorkshopItemKind { BepInExPlugin, GameFolder }

    public sealed class WorkshopItem
    {
        public string Id { get; set; }
        public string Dir { get; set; }
        public WorkshopItemKind Kind { get; set; }
        public string SourceDir { get; set; }
        public string PluginsDir { get; set; }
        public IReadOnlyList<string> DllFiles { get; set; }
        public IReadOnlyList<string> AssemblyNames { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
    }

    public static class WorkshopItemsScanner
    {
        public static List<WorkshopItem> Scan(string workshopRoot, Action<string> log)
        {
            var result = new List<WorkshopItem>();
            if (string.IsNullOrEmpty(workshopRoot) || !Directory.Exists(workshopRoot)) return result;

            foreach (var dir in Directory.GetDirectories(workshopRoot))
            {
                var plugins = Path.Combine(dir, "BepInEx", "plugins");
                var copyToGameFolder = Path.Combine(dir, "CopyToGameFolder");

                WorkshopItemKind kind;
                string sourceDir;
                string pluginsDir = null;

                if (Directory.Exists(plugins) &&
                    Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories).Length > 0)
                {
                    kind = WorkshopItemKind.BepInExPlugin;
                    sourceDir = plugins;
                    pluginsDir = plugins;
                }
                else if (Directory.Exists(copyToGameFolder) &&
                         Directory.GetFiles(copyToGameFolder, "*", SearchOption.AllDirectories).Length > 0)
                {
                    kind = WorkshopItemKind.GameFolder;
                    sourceDir = copyToGameFolder;
                }
                else if (HasGameLayoutAtRoot(dir))
                {
                    kind = WorkshopItemKind.GameFolder;
                    sourceDir = dir;
                }
                else
                {
                    continue;
                }

                var dlls = Directory.GetFiles(sourceDir, "*.dll", SearchOption.AllDirectories)
                                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                    .ToList();

                var names = new List<string>();
                string title = null, version = "";
                foreach (var dll in dlls)
                {
                    var info = AssemblyInfoReader.TryRead(dll, log);
                    if (!string.IsNullOrEmpty(info.AssemblyName)) names.Add(info.AssemblyName);
                    if (title == null) { title = info.Title; version = info.Version; }
                }

                result.Add(new WorkshopItem
                {
                    Id = Path.GetFileName(dir),
                    Dir = dir,
                    Kind = kind,
                    SourceDir = sourceDir,
                    PluginsDir = pluginsDir,
                    DllFiles = dlls,
                    AssemblyNames = names,
                    Title = string.IsNullOrEmpty(title) ? Path.GetFileName(dir) : title,
                    Version = version ?? ""
                });
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return result;
        }

        // Айтем, у которого файлы для папки игры лежат прямо в корне (GraveyardKeeper2_Data / Languages).
        private static bool HasGameLayoutAtRoot(string dir)
        {
            foreach (var name in new[] { "GraveyardKeeper2_Data", "Languages" })
            {
                var sub = Path.Combine(dir, name);
                if (Directory.Exists(sub) && Directory.GetFiles(sub, "*", SearchOption.AllDirectories).Length > 0)
                    return true;
            }
            return false;
        }
    }
}
