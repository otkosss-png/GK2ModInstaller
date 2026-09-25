using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace GK2ModInstaller.Core
{
    // Game-folder моды кладут .dll в GraveyardKeeper2_Data\Managed, но при запущенной игре
    // такие файлы заняты процессом — их нельзя ни обновить, ни удалить. Поэтому DLL грузим
    // в текущий процесс прямо из папки айтема (чтение байтов файл не блокирует), а в папку
    // игры копируем только данные (Languages, текстуры и т.п.). См. §10 спеки.
    public static class GameFolderDllLoader
    {
        // Возвращает число реально загруженных сборок. Никогда не бросает исключений.
        public static int Load(IEnumerable<string> dllPaths, Action<string> log)
        {
            var loaded = 0;
            if (dllPaths == null) return loaded;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in dllPaths)
            {
                try
                {
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                    var simpleName = Path.GetFileNameWithoutExtension(path);
                    if (!seen.Add(simpleName)) continue;
                    if (IsLoaded(simpleName))
                    {
                        log?.Invoke(string.Format(LoaderText.GameFolderDllAlreadyLoaded, simpleName));
                        continue;
                    }

                    Assembly.Load(File.ReadAllBytes(path));
                    loaded++;
                    log?.Invoke(string.Format(LoaderText.GameFolderDllLoaded, simpleName));
                }
                catch (Exception ex)
                {
                    log?.Invoke(string.Format(LoaderText.GameFolderDllFailed, Path.GetFileName(path), ex.Message));
                }
            }
            return loaded;
        }

        private static bool IsLoaded(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.Equals(asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
            return false;
        }
    }
}
