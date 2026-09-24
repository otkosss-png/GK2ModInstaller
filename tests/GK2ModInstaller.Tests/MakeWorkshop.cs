using System;
using System.Collections.Generic;
using System.IO;

namespace GK2ModInstaller.Tests
{
    internal sealed class ModPromptLog
    {
        public readonly List<string> Asked = new List<string>();
    }

    internal static class MakeWorkshop
    {
        public static string Tmp() => Path.Combine(Path.GetTempPath(), "gk2loader_" + Guid.NewGuid().ToString("N"));

        // Создаёт фейковый workshop-айтем: папка BepInEx\plugins с .dll и (опц.) config
        public static void Item(string workshopRoot, string id, string dllText, string cfgName = null)
        {
            var plugins = Path.Combine(workshopRoot, id, "BepInEx", "plugins");
            Directory.CreateDirectory(plugins);
            File.WriteAllText(Path.Combine(plugins, "Mod.dll"), dllText);
            if (cfgName != null)
            {
                var config = Path.Combine(workshopRoot, id, "BepInEx", "config");
                Directory.CreateDirectory(config);
                File.WriteAllText(Path.Combine(config, cfgName), "cfg");
            }
        }

        // Создаёт game-folder айтем: CopyToGameFolder\<relPath> с содержимым content.
        public static void GameFolderItem(string workshopRoot, string id, string relPath, string content)
        {
            var target = Path.Combine(workshopRoot, id, "CopyToGameFolder", relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, content);
        }

        public static string BackupDir(string bepInExRoot, string id)
            => Path.Combine(bepInExRoot, "config", "GK2_WorkshopLoader.backup", id);

        public static string BackupManifest(string bepInExRoot, string id)
            => Path.Combine(BackupDir(bepInExRoot, id), "manifest.txt");

        public static string Staging(string bepInExRoot, string id)
            => Path.Combine(bepInExRoot, "plugins", "_Workshop", id);

        public static void MakeStaged(string bepInExRoot, string id, string text)
        {
            var dir = Staging(bepInExRoot, id);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Mod.dll"), text);
        }

        public static void SafeDelete(string root)
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    internal sealed class FakeDialog : GK2ModInstaller.Core.IDialog
    {
        public Func<GK2ModInstaller.Core.ModPrompt, GK2ModInstaller.Core.ConsentAnswer> OnAsk =
            _ => GK2ModInstaller.Core.ConsentAnswer.Approve;
        public Func<IReadOnlyList<GK2ModInstaller.Core.ModPrompt>, GK2ModInstaller.Core.BulkAnswer> OnBulk =
            _ => GK2ModInstaller.Core.BulkAnswer.All;
        public readonly List<string> Asked = new List<string>();
        public readonly List<string> Warns = new List<string>();

        public GK2ModInstaller.Core.ConsentAnswer Ask(GK2ModInstaller.Core.ModPrompt prompt)
        {
            Asked.Add(prompt.Id);
            return OnAsk(prompt);
        }

        public GK2ModInstaller.Core.BulkAnswer AskBulkTrust(IReadOnlyList<GK2ModInstaller.Core.ModPrompt> mods)
        {
            foreach (var m in mods) Asked.Add("bulk:" + m.Id);
            return OnBulk(mods);
        }

        public void Warn(string text) { Warns.Add(text); }
    }
}
