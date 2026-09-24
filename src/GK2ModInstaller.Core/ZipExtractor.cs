using System;
using System.IO;
using System.IO.Compression;

namespace GK2ModInstaller.Core
{
    public static class ZipExtractor
    {
        public static void ExtractTo(Stream zipStream, string destDir)
        {
            var destFull = Path.GetFullPath(destDir);
            Directory.CreateDirectory(destFull);
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false))
            {
                foreach (var entry in archive.Entries)
                {
                    var target = Path.GetFullPath(Path.Combine(destFull, entry.FullName));
                    if (!IsUnder(target, destFull))
                        throw new IOException("Zip entry escapes destination: " + entry.FullName);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var es = entry.Open())
                    using (var fs = File.Create(target))
                        es.CopyTo(fs);
                }
            }
        }

        private static bool IsUnder(string path, string root)
        {
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return true;
            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
