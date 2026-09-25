using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GK2ModInstaller.Core
{
    public enum TrustState { Ask, Approved, Blocked }

    public sealed class TrustEntry
    {
        public string Id { get; set; }
        public string Sha256 { get; set; }
        public TrustState State { get; set; }
        public string Title { get; set; }
        public string Note { get; set; }
    }

    public sealed class TrustStore
    {
        private readonly List<string> _header = new List<string>();
        private readonly Dictionary<string, TrustEntry> _items =
            new Dictionary<string, TrustEntry>(StringComparer.OrdinalIgnoreCase);

        public static string StateText(TrustState state)
        {
            switch (state)
            {
                case TrustState.Approved: return "yes";
                case TrustState.Blocked: return "no";
                default: return "ask";
            }
        }

        public static TrustState ParseState(string text)
        {
            if (string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)) return TrustState.Approved;
            if (string.Equals(text, "no", StringComparison.OrdinalIgnoreCase)) return TrustState.Blocked;
            return TrustState.Ask;
        }

        public TrustEntry Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            TrustEntry e;
            return _items.TryGetValue(id, out e) ? e : null;
        }

        public void Set(TrustEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id)) return;
            _items[entry.Id] = entry;
        }

        public bool Remove(string id)
        {
            return !string.IsNullOrEmpty(id) && _items.Remove(id);
        }

        public IReadOnlyList<TrustEntry> All()
        {
            var list = new List<TrustEntry>(_items.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        public static TrustStore Load(string path, Action<string> log)
        {
            var store = new TrustStore();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return store;
            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("#", StringComparison.Ordinal)) { store._header.Add(raw); continue; }
                    var parts = line.Split('|');
                    if (parts.Length < 3) { log?.Invoke(LoaderText.TrustSkippedLine + line); continue; }
                    var entry = new TrustEntry
                    {
                        Id = parts[0].Trim(),
                        Sha256 = parts[1].Trim(),
                        State = ParseState(parts[2].Trim()),
                        Title = parts.Length > 3 ? parts[3].Trim() : "",
                        Note = parts.Length > 4 ? string.Join("|", parts, 4, parts.Length - 4).Trim() : ""
                    };
                    if (entry.Id.Length > 0) store.Set(entry);
                    else log?.Invoke(LoaderText.TrustSkippedNoId + line);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke(string.Format(LoaderText.TrustNotReadFormat, ex.Message));
                try { File.Move(path, path + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)); }
                catch { }
                return new TrustStore();
            }
            return store;
        }

        public void Save(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            if (_header.Count == 0)
                sb.AppendLine(LoaderText.TrustHeader);
            foreach (var h in _header) sb.AppendLine(h);
            foreach (var e in All())
            {
                sb.Append(e.Id).Append('|')
                  .Append(e.Sha256 ?? "").Append('|')
                  .Append(StateText(e.State)).Append('|')
                  .Append(e.Title ?? "").Append('|')
                  .Append(e.Note ?? "").AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
