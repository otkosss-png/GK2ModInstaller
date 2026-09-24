# GK2 Workshop Loader — согласие на моды (consent) — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Автозагрузчик Workshop-модов GK2 перестаёт запускать мод без согласия игрока: диалог одобрения для новых модов и обновлений, trust-файл, отпечаток версии, миграция уже стоящих модов, детект дубликатов, статический код-чек (предупреждения).

**Architecture:** Вся логика — в `GK2ModInstaller.Core` (netstandard2.0, без BepInEx) и покрыта тестами; файлы Core **линкуются** в проект патчера (`Compile Include`), поэтому `GK2.WorkshopAutoLoader.dll` остаётся самодостаточной. Cecil-часть (метаданные сборок, статический скан IL) живёт в Core (ссылка на `Mono.Cecil` из папки игры, `Private=false`; в тестах — копированием). Диалог — единственная BepInEx-side часть: `Win32Dialog : IDialog` через `user32.MessageBoxW` в проекте патчера.

**Tech Stack:** C# 9, .NET Standard 2.0 (Core, Patcher), .NET Framework 4.8 (App), net8.0 + xUnit (тесты), Mono.Cecil 0.11.x (из `BepInEx\core`), Win32 `user32.MessageBoxW` (P/Invoke).

**Spec:** `docs/superpowers/specs/2026-09-24-gk2-workshop-loader-consent-design.md`

## Global Constraints

- Целевые фреймворки: `Core` и `Patcher` — **netstandard2.0**, `LangVersion 9.0`; `App` — net48; тесты — **net8.0** (xUnit 2.9.2).
- `Core` **не должен ссылаться на BepInEx** (только `Mono.Cecil`, `Private=false`, HintPath из `$(GameDir)\BepInEx\core`).
- Патчер компилирует исходники Core **линком** (`<Compile Include="..\GK2ModInstaller.Core\<File>.cs" Link="<File>.cs" />`) — каждый новый файл Core, нужный патчеру, обязан быть добавлен в `GK2ModInstaller.Patcher.csproj`.
- **Запрещено** модифицировать/удалять что-либо в `steamapps\workshop\content\...` — только чтение. Удаляем и копируем только внутри `BepInEx`.
- Игровая папка по умолчанию: `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2` (свойство `GameDir`).
- Все пользовательские тексты (лог, диалоги, trust-файл) — на русском; кодировка файлов — UTF-8 без BOM.
- Сборка: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release` (workdir — корень `GK2ModInstaller`); тесты: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`.
- Тесты, работающие с файлами, используют временные папки в `%TEMP%` и всегда убирают за собой (`try/finally`).
- Имена/формат trust-файла: строки `id|sha256|state|title|note`, `state` ∈ `yes|no|ask`, строки `#` — комментарии.
- Не ломать существующее поведение инсталлятора (`--install/--uninstall`, эмбеддинг патчера).

## File Structure

**Core (`src/GK2ModInstaller.Core/`)** — новая логика:
- `TrustStore.cs` — `TrustState`, `TrustEntry`, `TrustStore` (Load/Save/Get/Set/Remove/All, бэкап битого файла).
- `ModFingerprint.cs` — SHA-256-отпечаток версии мода по содержимому `BepInEx\plugins\**` (конфиги исключены).
- `WorkshopItems.cs` — `AssemblyInfo`, `AssemblyInfoReader` (Cecil: имя/название/версия), `WorkshopItem`, `WorkshopItemsScanner`.
- `CodeScan.cs` — `FindingCategory`, `Finding`, `CodeScan` (Cecil-скан IL).
- `ConsentPlanner.cs` — `DecisionKind`, `PlanEntry`, `ConsentPlanner`.
- `AcfTimes.cs` — `timeupdated` из `appworkshop_4358690.acf` (справочный лог).
- `LoaderDialog.cs` — `ConsentAnswer`, `BulkAnswer`, `ModPrompt` (в т.ч. сборка текста диалога), `IDialog`.
- `PendingList.cs` — запись `GK2_WorkshopLoader.pending.txt`.
- `WorkshopLoader.cs` — оркестратор (`LoaderOptions`, `LoaderSummary`, `Run`).
- `WorkshopSync.cs` — **меняется**: остаётся низкоуровневым помощником копирования (`CopyDir` → public, `CopyConfigs`), `RunOnce`/`Sync` удаляются (их роль берёт `WorkshopLoader`).

**Patcher (`src/GK2ModInstaller.Patcher/`)**:
- `Win32Dialog.cs` — новый, `IDialog` через `user32.MessageBoxW`.
- `WorkshopAutoLoaderPatcher.cs` — **меняется**: резолв путей → `WorkshopLoader.Run(...)`.
- `GK2ModInstaller.Patcher.csproj` — **меняется**: линки всех новых файлов Core.

**Тесты (`tests/GK2ModInstaller.Tests/`)**:
- `TrustStoreTests.cs`, `ModFingerprintTests.cs`, `WorkshopItemsTests.cs`, `CodeScanTests.cs`, `ConsentPlannerTests.cs`, `AcfTimesTests.cs`, `ModPromptTests.cs`, `WorkshopLoaderTests.cs` — новые.
- `FakeDialog.cs`, `DangerousSample.cs`, `TestFs.cs` — тестовые помощники.
- `WorkshopSyncTests.cs` — **удаляется** (сценарии переезжают в `WorkshopLoaderTests`).
- `GK2ModInstaller.Tests.csproj` — **меняется**: ссылка на `Mono.Cecil` (copy-local), `GameDir`.

**Документация/релиз**: `README.md` (секция про согласие), `E:\GK2Upload\GK2WorkshopAutoLoader\...` + описание Workshop-айтема 3807406994, GitHub Release v1.2.0.

---

### Task 1: TrustStore

**Files:**
- Create: `src/GK2ModInstaller.Core/TrustStore.cs`
- Test: `tests/GK2ModInstaller.Tests/TrustStoreTests.cs`

**Interfaces:**
- Consumes: —
- Produces: `enum TrustState { Ask, Approved, Blocked }`; `class TrustEntry { string Id, Sha256, Title, Note; TrustState State; }`; `class TrustStore { TrustEntry Get(string id); void Set(TrustEntry e); bool Remove(string id); IReadOnlyList<TrustEntry> All(); static TrustStore Load(string path, Action<string> log); void Save(string path); static string StateText(TrustState); static TrustState ParseState(string); }`

- [ ] **Step 1: Написать падающий тест**

`tests/GK2ModInstaller.Tests/TrustStoreTests.cs`:
```csharp
using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class TrustStoreTests
    {
        private static string TmpFile() => Path.Combine(Path.GetTempPath(), "gk2trust_" + Guid.NewGuid().ToString("N") + ".txt");

        [Fact]
        public void Missing_file_gives_empty_store()
        {
            var store = TrustStore.Load(TmpFile(), null);
            Assert.Null(store.Get("111"));
            Assert.Empty(store.All());
        }

        [Fact]
        public void Save_then_Load_roundtrips_with_header_and_note_pipes()
        {
            string path = TmpFile();
            try
            {
                var store = TrustStore.Load(path, null);
                store.Set(new TrustEntry { Id = "111", Sha256 = "abc", State = TrustState.Approved, Title = "My Mod", Note = "одобрено | вручную" });
                store.Set(new TrustEntry { Id = "222", Sha256 = "def", State = TrustState.Blocked, Title = "Bad", Note = "" });
                store.Save(path);

                string text = File.ReadAllText(path);
                Assert.Contains("#", text);

                var again = TrustStore.Load(path, null);
                var a = again.Get("111");
                Assert.Equal(TrustState.Approved, a.State);
                Assert.Equal("abc", a.Sha256);
                Assert.Equal("одобрено | вручную", a.Note);
                Assert.Equal(TrustState.Blocked, again.Get("222").State);
                Assert.Equal(2, again.All().Count);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Ask_state_is_default_for_unknown_and_parsed_unknown_words()
        {
            Assert.Equal(TrustState.Ask, TrustStore.ParseState("ask"));
            Assert.Equal(TrustState.Ask, TrustStore.ParseState("что-то"));
            Assert.Equal("yes", TrustStore.StateText(TrustState.Approved));
            Assert.Equal("no", TrustStore.StateText(TrustState.Blocked));
            Assert.Equal("ask", TrustStore.StateText(TrustState.Ask));
        }

        [Fact]
        public void Broken_lines_are_skipped_and_comments_kept()
        {
            string path = TmpFile();
            try
            {
                File.WriteAllText(path, "# мой комментарий\nмусор без разделителей\n333|hash|yes|T|\n");
                var logs = new System.Collections.Generic.List<string>();
                var store = TrustStore.Load(path, logs.Add);
                Assert.Contains("# мой комментарий", File.ReadAllText(path, System.Text.Encoding.UTF8) is string ? "# мой комментарий" : "");
                Assert.Null(store.Get("мусор без разделителей"));
                Assert.Equal(TrustState.Approved, store.Get("333").State);
                Assert.Contains(logs, l => l.Contains("пропущена строка"));

                store.Save(path);
                Assert.Contains("# мой комментарий", File.ReadAllText(path));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Remove_and_All_are_ordinal_sorted()
        {
            var store = TrustStore.Load(TmpFile(), null);
            store.Set(new TrustEntry { Id = "2", State = TrustState.Ask, Sha256 = "" });
            store.Set(new TrustEntry { Id = "10", State = TrustState.Ask, Sha256 = "" });
            Assert.Equal(new[] { "10", "2" }, new[] { store.All()[0].Id, store.All()[1].Id });
            Assert.True(store.Remove("2"));
            Assert.False(store.Remove("2"));
            Assert.Null(store.Get("2"));
        }
    }
}
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~TrustStoreTests"` (workdir `C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller`)
Expected: ошибка компиляции `The type or namespace name 'TrustStore' could not be found`.

- [ ] **Step 3: Реализовать минимально**

`src/GK2ModInstaller.Core/TrustStore.cs`:
```csharp
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
                    if (parts.Length < 3) { log?.Invoke("trust: пропущена строка: " + line); continue; }
                    var entry = new TrustEntry
                    {
                        Id = parts[0].Trim(),
                        Sha256 = parts[1].Trim(),
                        State = ParseState(parts[2].Trim()),
                        Title = parts.Length > 3 ? parts[3].Trim() : "",
                        Note = parts.Length > 4 ? string.Join("|", parts, 4, parts.Length - 4).Trim() : ""
                    };
                    if (entry.Id.Length > 0) store.Set(entry);
                    else log?.Invoke("trust: пропущена строка без id: " + line);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("trust: файл не прочитан (" + ex.Message + "), начинаю заново");
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
                sb.AppendLine("# GK2 Workshop Loader: решения по модам. Удалите строку — спросят снова.");
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
```

- [ ] **Step 4: Запустить тесты — убедиться, что проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~TrustStoreTests"`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 5: Коммит**

```bash
git add src/GK2ModInstaller.Core/TrustStore.cs tests/GK2ModInstaller.Tests/TrustStoreTests.cs
git commit -m "feat(core): TrustStore for mod consent decisions"
```

---

### Task 2: ModFingerprint

**Files:**
- Create: `src/GK2ModInstaller.Core/ModFingerprint.cs`
- Test: `tests/GK2ModInstaller.Tests/ModFingerprintTests.cs`

**Interfaces:**
- Consumes: —
- Produces: `static class ModFingerprint { static string Compute(string pluginsDir); }` (пустая строка, если папки нет).

- [ ] **Step 1: Написать падающий тест**

`tests/GK2ModInstaller.Tests/ModFingerprintTests.cs`:
```csharp
using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class ModFingerprintTests
    {
        private static string Tmp() => Path.Combine(Path.GetTempPath(), "gk2fp_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void Same_content_gives_same_fingerprint_regardless_of_writes()
        {
            string dir = Tmp();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "sub"));
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAA");
                File.WriteAllText(Path.Combine(dir, "sub", "res.txt"), "R");
                string a = ModFingerprint.Compute(dir);
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAA");
                Assert.Equal(a, ModFingerprint.Compute(dir));
                Assert.Equal(64, a.Length);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Changed_dll_changes_fingerprint()
        {
            string dir = Tmp();
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAA");
                string a = ModFingerprint.Compute(dir);
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAB");
                Assert.NotEqual(a, ModFingerprint.Compute(dir));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Config_files_do_not_affect_fingerprint()
        {
            string dir = Tmp();
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "Mod.dll"), "AAA");
                string a = ModFingerprint.Compute(dir);
                File.WriteAllText(Path.Combine(dir, "settings.cfg"), "user edited");
                Assert.Equal(a, ModFingerprint.Compute(dir));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Missing_directory_gives_empty_string()
        {
            Assert.Equal("", ModFingerprint.Compute(Path.Combine(Path.GetTempPath(), "gk2nope_" + Guid.NewGuid().ToString("N"))));
            Assert.Equal("", ModFingerprint.Compute(null));
        }
    }
}
```

- [ ] **Step 2: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~ModFingerprintTests"`
Expected: ошибка компиляции — нет `ModFingerprint`.

- [ ] **Step 3: Реализовать**

`src/GK2ModInstaller.Core/ModFingerprint.cs`:
```csharp
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
                if (rel.Contains("/config/")) continue;
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
```

- [ ] **Step 4: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~ModFingerprintTests"`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 5: Коммит**

```bash
git add src/GK2ModInstaller.Core/ModFingerprint.cs tests/GK2ModInstaller.Tests/ModFingerprintTests.cs
git commit -m "feat(core): mod version fingerprint (sha256 over plugin files)"
```

---

### Task 3: AssemblyInfoReader + WorkshopItemsScanner (+ Cecil в тестах)

**Files:**
- Create: `src/GK2ModInstaller.Core/WorkshopItems.cs`
- Modify: `src/GK2ModInstaller.Core/GK2ModInstaller.Core.csproj` (ссылка на Mono.Cecil)
- Modify: `tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj` (Mono.Cecil copy-local)
- Test: `tests/GK2ModInstaller.Tests/WorkshopItemsTests.cs`

**Interfaces:**
- Consumes: —
- Produces: `class AssemblyInfo { string AssemblyName, Title, Version; }`; `static class AssemblyInfoReader { static AssemblyInfo TryRead(string dllPath, Action<string> log); }`; `class WorkshopItem { string Id, Dir, PluginsDir, Title, Version; IReadOnlyList<string> DllFiles, AssemblyNames; }`; `static class WorkshopItemsScanner { static List<WorkshopItem> Scan(string workshopRoot, Action<string> log); }`

- [ ] **Step 1: Подключить Mono.Cecil к Core и тестам**

`src/GK2ModInstaller.Core/GK2ModInstaller.Core.csproj` — в конец, перед `</Project>`:
```xml
  <PropertyGroup>
    <GameDir Condition="'$(GameDir)' == ''">E:\SteamLibrary\steamapps\common\Graveyard Keeper 2</GameDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Mono.Cecil"><HintPath>$(GameDir)\BepInEx\core\Mono.Cecil.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
```

`tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj` — в конец, перед `</Project>`:
```xml
  <PropertyGroup>
    <GameDir Condition="'$(GameDir)' == ''">E:\SteamLibrary\steamapps\common\Graveyard Keeper 2</GameDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Mono.Cecil"><HintPath>$(GameDir)\BepInEx\core\Mono.Cecil.dll</HintPath><Private>true</Private></Reference>
  </ItemGroup>
```

- [ ] **Step 2: Написать падающий тест**

`tests/GK2ModInstaller.Tests/WorkshopItemsTests.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class WorkshopItemsTests
    {
        private static string Tmp() => Path.Combine(Path.GetTempPath(), "gk2items_" + Guid.NewGuid().ToString("N"));

        private static void MakeItem(string workshopRoot, string id, string dllName, string content, string cfgName)
        {
            var p = Path.Combine(workshopRoot, id, "BepInEx", "plugins");
            Directory.CreateDirectory(p);
            File.WriteAllBytes(Path.Combine(p, dllName), System.Text.Encoding.ASCII.GetBytes(content));
            if (cfgName != null)
            {
                var c = Path.Combine(workshopRoot, id, "BepInEx", "config");
                Directory.CreateDirectory(c);
                File.WriteAllText(Path.Combine(c, cfgName), "cfg");
            }
        }

        [Fact]
        public void Item_without_plugin_dll_is_skipped_silently()
        {
            string root = Tmp();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "100", "BepInEx", "Languages"));
                File.WriteAllText(Path.Combine(root, "100", "BepInEx", "Languages", "l.json"), "{}");
                var logs = new System.Collections.Generic.List<string>();
                var items = WorkshopItemsScanner.Scan(root, logs.Add);
                Assert.Empty(items);
                Assert.DoesNotContain(logs, l => l.Contains("100"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Broken_dll_is_tolerated_and_title_falls_back_to_file_name()
        {
            string root = Tmp();
            try
            {
                MakeItem(root, "200", "MyMod.dll", "не сборка", null);
                var logs = new System.Collections.Generic.List<string>();
                var items = WorkshopItemsScanner.Scan(root, logs.Add);
                var item = Assert.Single(items);
                Assert.Equal("200", item.Id);
                Assert.Equal("MyMod", item.Title);
                Assert.Equal("", item.Version);
                Assert.Equal(new[] { "MyMod" }, item.AssemblyNames.ToArray());
                Assert.Single(item.DllFiles);
                Assert.Contains(logs, l => l.Contains("метаданные"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Real_assembly_metadata_is_read()
        {
            string dll = typeof(GK2ModInstaller.Core.TrustStore).Assembly.Location;
            var info = AssemblyInfoReader.TryRead(dll, null);
            Assert.Equal("GK2ModInstaller.Core", info.AssemblyName);
            Assert.False(string.IsNullOrEmpty(info.Version));
        }

        [Fact]
        public void Items_are_sorted_by_id_and_configs_are_not_part_of_dll_list()
        {
            string root = Tmp();
            try
            {
                MakeItem(root, "300", "A.dll", "x", "mod.cfg");
                MakeItem(root, "150", "B.dll", "y", null);
                var items = WorkshopItemsScanner.Scan(root, null);
                Assert.Equal(new[] { "150", "300" }, items.Select(i => i.Id).ToArray());
                Assert.All(items, i => Assert.All(i.DllFiles, f => Assert.EndsWith(".dll", f)));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Missing_root_gives_empty_list()
        {
            Assert.Empty(WorkshopItemsScanner.Scan(Path.Combine(Path.GetTempPath(), "gk2none_" + Guid.NewGuid().ToString("N")), null));
            Assert.Empty(WorkshopItemsScanner.Scan(null, null));
        }
    }
}
```

- [ ] **Step 3: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~WorkshopItemsTests"`
Expected: ошибка компиляции — нет `WorkshopItemsScanner`.

- [ ] **Step 4: Реализовать**

`src/GK2ModInstaller.Core/WorkshopItems.cs`:
```csharp
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

    public sealed class WorkshopItem
    {
        public string Id { get; set; }
        public string Dir { get; set; }
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
                if (!Directory.Exists(plugins)) continue;

                var dlls = Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories)
                                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                    .ToList();
                if (dlls.Count == 0) continue;

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
                    PluginsDir = plugins,
                    DllFiles = dlls,
                    AssemblyNames = names,
                    Title = string.IsNullOrEmpty(title) ? Path.GetFileName(dir) : title,
                    Version = version ?? ""
                });
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return result;
        }
    }
}
```

- [ ] **Step 5: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~WorkshopItemsTests"`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 6: Коммит**

```bash
git add src/GK2ModInstaller.Core/WorkshopItems.cs src/GK2ModInstaller.Core/GK2ModInstaller.Core.csproj tests/GK2ModInstaller.Tests/WorkshopItemsTests.cs tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj
git commit -m "feat(core): workshop item scanner with assembly metadata; cecil refs"
```

---

### Task 4: CodeScan

**Files:**
- Create: `src/GK2ModInstaller.Core/CodeScan.cs`
- Create: `tests/GK2ModInstaller.Tests/DangerousSample.cs`
- Test: `tests/GK2ModInstaller.Tests/CodeScanTests.cs`

**Interfaces:**
- Consumes: —
- Produces: `enum FindingCategory { Network, Process, FileDelete, CodeLoad, Registry, Native }`; `class Finding { FindingCategory Category; string Detail; }`; `static class CodeScan { static IReadOnlyList<Finding> Scan(string dllPath); }`

- [ ] **Step 1: Добавить «опасную» тестовую сборку**

`tests/GK2ModInstaller.Tests/DangerousSample.cs`:
```csharp
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace GK2ModInstaller.Tests
{
    // Эти методы никогда не вызываются — они нужны только как IL для проверки CodeScan.
    internal static class DangerousSample
    {
        public static void Touch()
        {
            var client = new HttpClient();
            var start = new ProcessStartInfo("cmd.exe");
            File.Delete("some.tmp");
            var asm = Assembly.Load("Nothing");
            System.Console.WriteLine(client != null && start != null && asm != null);
        }
    }
}
```

- [ ] **Step 2: Написать падающий тест**

`tests/GK2ModInstaller.Tests/CodeScanTests.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class CodeScanTests
    {
        [Fact]
        public void Finds_network_process_filedelete_and_codeload_in_test_assembly()
        {
            var found = CodeScan.Scan(typeof(DangerousSample).Assembly.Location);
            var cats = found.Select(f => f.Category).Distinct().ToList();
            Assert.Contains(FindingCategory.Network, cats);
            Assert.Contains(FindingCategory.Process, cats);
            Assert.Contains(FindingCategory.FileDelete, cats);
            Assert.Contains(FindingCategory.CodeLoad, cats);
            Assert.All(found, f => Assert.False(string.IsNullOrEmpty(f.Detail)));
        }

        [Fact]
        public void Clean_assembly_has_no_network_process_or_registry()
        {
            var found = CodeScan.Scan(typeof(GK2ModInstaller.Core.ModFingerprint).Assembly.Location);
            Assert.DoesNotContain(found, f => f.Category == FindingCategory.Network);
            Assert.DoesNotContain(found, f => f.Category == FindingCategory.Process);
            Assert.DoesNotContain(found, f => f.Category == FindingCategory.Registry);
        }

        [Fact]
        public void Broken_dll_gives_no_findings_and_does_not_throw()
        {
            string path = Path.Combine(Path.GetTempPath(), "gk2scan_" + Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllText(path, "не сборка");
            try { Assert.Empty(CodeScan.Scan(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Missing_file_gives_no_findings()
        {
            Assert.Empty(CodeScan.Scan(Path.Combine(Path.GetTempPath(), "gk2scan_missing_" + Guid.NewGuid().ToString("N") + ".dll")));
            Assert.Empty(CodeScan.Scan(null));
        }
    }
}
```

- [ ] **Step 3: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~CodeScanTests"`
Expected: ошибка компиляции — нет `CodeScan` (и `ConsentPlanner` появится только в Task 5 — на этом шаге достаточно, чтобы тест не компилировался из-за `CodeScan`).

- [ ] **Step 4: Реализовать**

`src/GK2ModInstaller.Core/CodeScan.cs`:
```csharp
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
```

- [ ] **Step 5: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~CodeScanTests"`
Expected: `Passed! - Failed: 0, Passed: 4`. Если `Clean_assembly...` падает — проверить, что в Core нет ссылок на `Process`/`HttpClient`/`Registry`.

- [ ] **Step 6: Коммит**

```bash
git add src/GK2ModInstaller.Core/CodeScan.cs tests/GK2ModInstaller.Tests/CodeScanTests.cs tests/GK2ModInstaller.Tests/DangerousSample.cs
git commit -m "feat(core): static code scan (network/process/filedelete/codeload/registry/pinvoke)"
```

---

### Task 5: ConsentPlanner

**Files:**
- Create: `src/GK2ModInstaller.Core/ConsentPlanner.cs`
- Test: `tests/GK2ModInstaller.Tests/ConsentPlannerTests.cs`

**Interfaces:**
- Consumes: `TrustStore`, `TrustEntry`, `TrustState` (Task 1); `WorkshopItem` (Task 3); `Finding` (Task 4).
- Produces: `enum DecisionKind { New, Update, Known, Blocked, Removed }`; `class PlanEntry { DecisionKind Kind; WorkshopItem Item; TrustEntry Trust; string Fingerprint; bool Staged; IReadOnlyList<Finding> Findings; IReadOnlyList<string> Duplicates; }`; `static class ConsentPlanner { static List<PlanEntry> Build(IReadOnlyList<WorkshopItem> items, TrustStore trust, Func<WorkshopItem,string> fingerprint, Func<WorkshopItem,IReadOnlyList<Finding>> scanFindings, IReadOnlyList<string> manualAssemblies, IReadOnlyList<string> stagedIds); }`

- [ ] **Step 1: Написать падающий тест**

`tests/GK2ModInstaller.Tests/ConsentPlannerTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class ConsentPlannerTests
    {
        private static WorkshopItem Item(string id, params string[] asmNames) => new WorkshopItem
        {
            Id = id,
            Dir = "ws\\" + id,
            PluginsDir = "ws\\" + id + "\\BepInEx\\plugins",
            DllFiles = new List<string> { "ws\\" + id + "\\BepInEx\\plugins\\Mod.dll" },
            AssemblyNames = asmNames.ToList(),
            Title = "Mod " + id,
            Version = "1.0"
        };

        private static TrustStore Trust(string id, string sha, TrustState state)
        {
            var s = TrustStore.Load(null, null);
            s.Set(new TrustEntry { Id = id, Sha256 = sha, State = state, Title = "Mod " + id });
            return s;
        }

        private static ConsentPlannerHook Hook() => new ConsentPlannerHook();

        private sealed class ConsentPlannerHook
        {
            public List<string> Scanned = new List<string>();
            public IReadOnlyList<Finding> Scan(WorkshopItem item)
            {
                Scanned.Add(item.Id);
                return new List<Finding> { new Finding { Category = FindingCategory.Network, Detail = "x" } };
            }
        }

        [Fact]
        public void New_when_no_trust_entry_or_ask()
        {
            var hook = Hook();
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, TrustStore.Load(null, null), i => "fp", hook.Scan, null, null);
            Assert.Equal(DecisionKind.New, plan[0].Kind);
            Assert.Contains("111", hook.Scanned);
        }

        [Fact]
        public void Known_when_hash_matches_and_no_scan_happens()
        {
            var hook = Hook();
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, Trust("111", "fp", TrustState.Approved), i => "fp", hook.Scan, null, null);
            Assert.Equal(DecisionKind.Known, plan[0].Kind);
            Assert.Empty(hook.Scanned);
        }

        [Fact]
        public void Update_when_hash_changed_and_scan_happens()
        {
            var hook = Hook();
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, Trust("111", "old", TrustState.Approved), i => "new", hook.Scan, null, null);
            Assert.Equal(DecisionKind.Update, plan[0].Kind);
            Assert.Contains("111", hook.Scanned);
            Assert.Single(plan[0].Findings);
        }

        [Fact]
        public void Blocked_when_state_no_and_no_scan()
        {
            var hook = Hook();
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, Trust("111", "fp", TrustState.Blocked), i => "fp", hook.Scan, null, null);
            Assert.Equal(DecisionKind.Blocked, plan[0].Kind);
            Assert.Empty(hook.Scanned);
        }

        [Fact]
        public void Removed_for_staged_ids_that_vanished_and_staged_flag_is_set()
        {
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "A") }, Trust("111", "fp", TrustState.Approved), i => "fp", _ => null,
                null, new[] { "111", "999" });
            Assert.True(plan.Single(p => p.Item.Id == "111").Staged);
            var removed = plan.Single(p => p.Kind == DecisionKind.Removed);
            Assert.Equal("999", removed.Item.Id);
        }

        [Fact]
        public void Duplicates_are_matched_by_assembly_name_case_insensitively()
        {
            var plan = ConsentPlanner.Build(
                new[] { Item("111", "MyMod") }, TrustStore.Load(null, null), i => "fp", _ => null,
                new[] { "mymod", "Other" }, null);
            Assert.Equal(new[] { "MyMod" }, plan[0].Duplicates.ToArray());
        }

        [Fact]
        public void Null_scanner_and_null_fingerprint_do_not_throw()
        {
            var plan = ConsentPlanner.Build(new[] { Item("111") }, null, null, null, null, null);
            Assert.Equal(DecisionKind.New, plan[0].Kind);
            Assert.Empty(plan[0].Findings);
            Assert.Empty(plan[0].Duplicates);
        }
    }
}
```

- [ ] **Step 2: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~ConsentPlannerTests"`
Expected: ошибка компиляции — нет `ConsentPlanner`.

- [ ] **Step 3: Реализовать**

`src/GK2ModInstaller.Core/ConsentPlanner.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace GK2ModInstaller.Core
{
    public enum DecisionKind { New, Update, Known, Blocked, Removed }

    public sealed class PlanEntry
    {
        public DecisionKind Kind { get; set; }
        public WorkshopItem Item { get; set; }
        public TrustEntry Trust { get; set; }
        public string Fingerprint { get; set; }
        public bool Staged { get; set; }
        public IReadOnlyList<Finding> Findings { get; set; }
        public IReadOnlyList<string> Duplicates { get; set; }
    }

    public static class ConsentPlanner
    {
        public static List<PlanEntry> Build(
            IReadOnlyList<WorkshopItem> items,
            TrustStore trust,
            Func<WorkshopItem, string> fingerprint,
            Func<WorkshopItem, IReadOnlyList<Finding>> scanFindings,
            IReadOnlyList<string> manualAssemblies,
            IReadOnlyList<string> stagedIds)
        {
            var result = new List<PlanEntry>();
            var manual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (manualAssemblies != null) foreach (var a in manualAssemblies) manual.Add(a);
            var staged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (stagedIds != null) foreach (var id in stagedIds) staged.Add(id);
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    present.Add(item.Id);
                    string fp = fingerprint != null ? (fingerprint(item) ?? "") : "";
                    var entry = trust != null ? trust.Get(item.Id) : null;

                    DecisionKind kind;
                    if (entry == null || entry.State == TrustState.Ask) kind = DecisionKind.New;
                    else if (entry.State == TrustState.Blocked) kind = DecisionKind.Blocked;
                    else kind = string.Equals(entry.Sha256, fp, StringComparison.OrdinalIgnoreCase)
                        ? DecisionKind.Known
                        : DecisionKind.Update;

                    bool needsScan = kind == DecisionKind.New || kind == DecisionKind.Update;
                    var duplicates = new List<string>();
                    if (item.AssemblyNames != null)
                        foreach (var name in item.AssemblyNames)
                            if (!string.IsNullOrEmpty(name) && manual.Contains(name)) duplicates.Add(name);

                    result.Add(new PlanEntry
                    {
                        Kind = kind,
                        Item = item,
                        Trust = entry,
                        Fingerprint = fp,
                        Staged = staged.Contains(item.Id),
                        Findings = needsScan && scanFindings != null ? scanFindings(item) : new List<Finding>(),
                        Duplicates = duplicates
                    });
                }
            }

            if (stagedIds != null)
            {
                foreach (var id in stagedIds)
                {
                    if (string.IsNullOrEmpty(id) || present.Contains(id)) continue;
                    result.Add(new PlanEntry
                    {
                        Kind = DecisionKind.Removed,
                        Item = new WorkshopItem { Id = id, Title = id, AssemblyNames = new List<string>(), DllFiles = new List<string>() },
                        Fingerprint = "",
                        Staged = true,
                        Findings = new List<Finding>(),
                        Duplicates = new List<string>()
                    });
                }
            }

            return result;
        }
    }
}
```

- [ ] **Step 4: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~ConsentPlannerTests"`
Expected: `Passed! - Failed: 0, Passed: 7`.

- [ ] **Step 5: Коммит**

```bash
git add src/GK2ModInstaller.Core/ConsentPlanner.cs tests/GK2ModInstaller.Tests/ConsentPlannerTests.cs
git commit -m "feat(core): consent planner (new/update/known/blocked/removed, staged, duplicates)"
```

---

### Task 6: AcfTimes

**Files:**
- Create: `src/GK2ModInstaller.Core/AcfTimes.cs`
- Test: `tests/GK2ModInstaller.Tests/AcfTimesTests.cs`

**Interfaces:**
- Consumes: —
- Produces: `static class AcfTimes { static Dictionary<string, long> Parse(string acfText); }` (ключ — id айтема, значение — `timeupdated`; пустой словарь при мусоре).

- [ ] **Step 1: Написать падающий тест**

`tests/GK2ModInstaller.Tests/AcfTimesTests.cs`:
```csharp
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class AcfTimesTests
    {
        private const string Fixture = @"""AppWorkshop""
{
	""appid""		""4358690""
	""WorkshopItemsInstalled""
	{
		""3807023815""
		{
			""size""		""1234""
			""timeupdated""		""1758600000""
		}
		""3807346541""
		{
			""timeupdated""		""1758700000""
			""size""		""999""
		}
	}
	""WorkshopItemDetails""
	{
		""timetouched""		""111""
	}
}";

        [Fact]
        public void Parses_timeupdated_per_item()
        {
            var map = AcfTimes.Parse(Fixture);
            Assert.Equal(2, map.Count);
            Assert.Equal(1758600000L, map["3807023815"]);
            Assert.Equal(1758700000L, map["3807346541"]);
        }

        [Fact]
        public void Garbage_text_gives_empty_map()
        {
            Assert.Empty(AcfTimes.Parse("нет тут ничего"));
            Assert.Empty(AcfTimes.Parse(null));
            Assert.Empty(AcfTimes.Parse(""));
        }
    }
}
```

- [ ] **Step 2: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~AcfTimesTests"`
Expected: ошибка компиляции — нет `AcfTimes`.

- [ ] **Step 3: Реализовать**

`src/GK2ModInstaller.Core/AcfTimes.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GK2ModInstaller.Core
{
    // Минимальный разбор appworkshop_<appid>.acf: достаём "timeupdated" для каждого id айтема.
    // Нужен только для справочного лога «у мода доступно обновление».
    public static class AcfTimes
    {
        private static readonly Regex ItemBlock = new Regex("^\\s*\"(?<id>\\d+)\"\\s*\\{?\\s*$", RegexOptions.Compiled);
        private static readonly Regex TimeUpdated = new Regex("^\\s*\"timeupdated\"\\s*\"(?<t>\\d+)\"", RegexOptions.Compiled);
        private static readonly Regex Closing = new Regex("^\\s*\\}\\s*$", RegexOptions.Compiled);

        public static Dictionary<string, long> Parse(string acfText)
        {
            var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(acfText)) return map;

            string currentId = null;
            foreach (var raw in acfText.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (currentId == null)
                {
                    var m = ItemBlock.Match(line);
                    if (m.Success) currentId = m.Groups["id"].Value;
                    continue;
                }

                var t = TimeUpdated.Match(line);
                if (t.Success)
                {
                    long value;
                    if (long.TryParse(t.Groups["t"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                        map[currentId] = value;
                    currentId = null;
                    continue;
                }

                if (Closing.IsMatch(line)) currentId = null;
            }
            return map;
        }
    }
}
```
(Формально `ItemBlock` может поймать и `"size" "1234"`, но по тесту и реальному ACF внутри блока первым идёт id-строка, а `timeupdated` — ключ без цифр; для `size` после `timeupdated` блок уже закрыт.)

- [ ] **Step 4: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~AcfTimesTests"`
Expected: `Passed! - Failed: 0, Passed: 2`.

- [ ] **Step 5: Коммит**

```bash
git add src/GK2ModInstaller.Core/AcfTimes.cs tests/GK2ModInstaller.Tests/AcfTimesTests.cs
git commit -m "feat(core): parse timeupdated from appworkshop acf"
```

---

### Task 7: Контракты диалога, текст промпта, PendingList

**Files:**
- Create: `src/GK2ModInstaller.Core/LoaderDialog.cs`
- Create: `src/GK2ModInstaller.Core/PendingList.cs`
- Test: `tests/GK2ModInstaller.Tests/ModPromptTests.cs`

**Interfaces:**
- Consumes: `Finding`, `FindingCategory` (Task 4).
- Produces: `enum ConsentAnswer { Approve, Deny, Later }`; `enum BulkAnswer { All, AskEach, Later }`; `class ModPrompt { string Id, Title, Version; bool IsUpdate; IReadOnlyList<string> Files; IReadOnlyList<Finding> Findings; IReadOnlyList<string> Duplicates; string Text(string header); }`; `interface IDialog { ConsentAnswer Ask(ModPrompt prompt); BulkAnswer AskBulkTrust(IReadOnlyList<ModPrompt> mods); void Warn(string text); }`; `static class PendingList { static void Write(string path, IEnumerable<ModPrompt> mods, Action<string> log); }`

- [ ] **Step 1: Написать падающий тест**

`tests/GK2ModInstaller.Tests/ModPromptTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class ModPromptTests
    {
        private static ModPrompt Sample(bool update = false) => new ModPrompt
        {
            Id = "3807023815",
            Title = "Better Auto Crafting",
            Version = "1.2",
            IsUpdate = update,
            Files = new List<string> { "BetterAutoCrafting.dll" },
            Findings = new List<Finding> { new Finding { Category = FindingCategory.Network, Detail = "System.Net.WebClient" } },
            Duplicates = new List<string> { "BetterAutoCrafting" }
        };

        [Fact]
        public void Text_for_new_mod_contains_title_id_findings_and_buttons_hint()
        {
            string text = Sample().Text("Мод из Workshop");
            Assert.Contains("Мод из Workshop", text);
            Assert.Contains("Better Auto Crafting", text);
            Assert.Contains("3807023815", text);
            Assert.Contains("сеть", text);
            Assert.Contains("Тот же мод уже стоит вручную", text);
            Assert.Contains("Cancel", text);
        }

        [Fact]
        public void Text_for_update_mentions_update_and_keeps_previous_version_hint()
        {
            string text = Sample(true).Text("Обновление мода");
            Assert.Contains("Обновление мода", text);
            Assert.Contains("прежняя версия", text);
        }

        [Fact]
        public void Text_without_findings_or_duplicates_says_clean()
        {
            var prompt = Sample();
            prompt.Findings = new List<Finding>();
            prompt.Duplicates = new List<string>();
            string text = prompt.Text("Мод из Workshop");
            Assert.Contains("ничего подозрительного", text);
            Assert.DoesNotContain("вручную", text);
        }

        [Fact]
        public void PendingList_writes_id_and_title_lines()
        {
            string path = Path.Combine(Path.GetTempPath(), "gk2pending_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                PendingList.Write(path, new[] { Sample() }, null);
                string text = File.ReadAllText(path);
                Assert.Contains("3807023815|Better Auto Crafting", text);
                Assert.Contains("#", text);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void PendingList_never_throws_on_bad_path()
        {
            var logs = new List<string>();
            PendingList.Write(Path.Combine(Path.GetTempPath(), "no_such_dir_" + Guid.NewGuid().ToString("N"), "x", "y.txt"), new[] { Sample() }, logs.Add);
            Assert.Contains(logs, l => l.Contains("pending"));
        }
    }
}
```

- [ ] **Step 2: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~ModPromptTests"`
Expected: ошибка компиляции — нет `ModPrompt`.

- [ ] **Step 3: Реализовать**

`src/GK2ModInstaller.Core/LoaderDialog.cs`:
```csharp
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
```

`src/GK2ModInstaller.Core/PendingList.cs`:
```csharp
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
```

- [ ] **Step 4: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~ModPromptTests"`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 5: Коммит**

```bash
git add src/GK2ModInstaller.Core/LoaderDialog.cs src/GK2ModInstaller.Core/PendingList.cs tests/GK2ModInstaller.Tests/ModPromptTests.cs
git commit -m "feat(core): dialog contracts, prompt text builder, pending list"
```

---

### Task 8: WorkshopLoader — Known/Blocked/Removed + копирование (рефакторинг WorkshopSync)

**Files:**
- Modify: `src/GK2ModInstaller.Core/WorkshopSync.cs` (оставить только файловые помощники)
- Create: `src/GK2ModInstaller.Core/WorkshopLoader.cs`
- Delete: `tests/GK2ModInstaller.Tests/WorkshopSyncTests.cs`
- Create: `tests/GK2ModInstaller.Tests/MakeWorkshop.cs` (общий помощник тестов)
- Test: `tests/GK2ModInstaller.Tests/WorkshopLoaderTests.cs`

**Interfaces:**
- Consumes: `TrustStore`, `WorkshopItemsScanner`, `ModFingerprint`, `CodeScan`, `ConsentPlanner`, `IDialog`, `ModPrompt`.
- Produces: `class LoaderOptions { string WorkshopRoot, WorkshopAcfPath, BepInExRoot; }`; `class LoaderSummary { int Items, Approved, Updates, Blocked, Postponed, Removed, Migrated, Duplicates, Findings; override string ToString(); }`; `static class WorkshopLoader { const string StagingDirName = "_Workshop", TrustFileName = "GK2_WorkshopLoader.trust.txt", PendingFileName = "GK2_WorkshopLoader.pending.txt"; static LoaderSummary Run(LoaderOptions options, IDialog dialog, Action<string> log); }`; `static class WorkshopSync { public static int CopyDir(string src, string dst); public static int CopyConfigs(string srcConfigDir, string configDir, Action<string> log); }`

- [ ] **Step 1: Общий тестовый помощник**

`tests/GK2ModInstaller.Tests/MakeWorkshop.cs`:
```csharp
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
```

- [ ] **Step 2: Написать падающий тест**

`tests/GK2ModInstaller.Tests/WorkshopLoaderTests.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class WorkshopLoaderTests
    {
        private static LoaderOptions Options(string root)
        {
            string bep = Path.Combine(root, "BepInEx");
            Directory.CreateDirectory(bep);
            return new LoaderOptions
            {
                WorkshopRoot = Path.Combine(root, "ws"),
                BepInExRoot = bep,
                WorkshopAcfPath = Path.Combine(root, "appworkshop_4358690.acf")
            };
        }

        [Fact]
        public void Known_approved_mod_is_not_asked_and_stays_in_place()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", "mod.cfg");
                string fp = ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "111", "BepInEx", "plugins"));
                var store = TrustStore.Load(Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt"), null);
                store.Set(new TrustEntry { Id = "111", Sha256 = fp, State = TrustState.Approved, Title = "Mod" });
                store.Save(Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt"));

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Empty(dialog.Asked);
                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "111"), "Mod.dll")));
                Assert.Equal(1, summary.Items);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Blocked_mod_is_never_asked_and_its_staging_is_removed()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "222", "MOD", null);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "222", "OLD");
                string trust = Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt");
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "222", Sha256 = "x", State = TrustState.Blocked, Title = "Bad" });
                store.Save(trust);

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Empty(dialog.Asked);
                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "222")));
                Assert.Equal(1, summary.Blocked);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Removed_item_is_deleted_from_staging_and_trust_entry_is_kept()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "999", "OLD");
                string trust = Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt");
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "999", Sha256 = "x", State = TrustState.Approved, Title = "Gone" });
                store.Save(trust);

                var summary = WorkshopLoader.Run(options, new FakeDialog(), null);

                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "999")));
                Assert.Equal(1, summary.Removed);
                Assert.NotNull(TrustStore.Load(trust, null).Get("999"));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Missing_workshop_root_is_reported_and_does_not_throw()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                var logs = new System.Collections.Generic.List<string>();
                var summary = WorkshopLoader.Run(options, new FakeDialog(), logs.Add);
                Assert.Equal(0, summary.Items);
                Assert.Contains(logs, l => l.Contains("Workshop"));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Config_is_not_overwritten_but_plugin_files_are_copied()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "333", "MOD", "mod.cfg");
                Directory.CreateDirectory(Path.Combine(options.BepInExRoot, "config"));
                File.WriteAllText(Path.Combine(options.BepInExRoot, "config", "mod.cfg"), "USER");
                string trust = Path.Combine(options.BepInExRoot, "config", "GK2_WorkshopLoader.trust.txt");
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "333", Sha256 = ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "333", "BepInEx", "plugins")), State = TrustState.Approved });
                store.Save(trust);

                WorkshopLoader.Run(options, new FakeDialog(), null);

                Assert.Equal("USER", File.ReadAllText(Path.Combine(options.BepInExRoot, "config", "mod.cfg")));
                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "333"), "Mod.dll")));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }
    }
}
```

- [ ] **Step 3: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~WorkshopLoaderTests"`
Expected: ошибка компиляции — нет `WorkshopLoader`.

- [ ] **Step 4: Реализовать**

`src/GK2ModInstaller.Core/WorkshopSync.cs` — заменить целиком на:
```csharp
using System;
using System.IO;

namespace GK2ModInstaller.Core
{
    // Низкоуровневые файловые операции автозагрузчика. Решения «что грузить» принимает WorkshopLoader.
    public static class WorkshopSync
    {
        public static int CopyDir(string src, string dst)
        {
            int n = 0;
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, dir.Substring(src.Length).TrimStart('\\', '/')));
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(dst, file.Substring(src.Length).TrimStart('\\', '/')), true);
                n++;
            }
            return n;
        }

        // Конфиги копируются только если такого файла ещё нет (не затираем правки игрока).
        public static int CopyConfigs(string srcConfigDir, string configDir, Action<string> log)
        {
            int n = 0;
            if (string.IsNullOrEmpty(srcConfigDir) || !Directory.Exists(srcConfigDir)) return 0;
            Directory.CreateDirectory(configDir);
            foreach (var cfg in Directory.GetFiles(srcConfigDir, "*.cfg", SearchOption.AllDirectories))
            {
                var target = Path.Combine(configDir, Path.GetFileName(cfg));
                if (File.Exists(target)) continue;
                File.Copy(cfg, target);
                log?.Invoke("config: " + Path.GetFileName(cfg));
                n++;
            }
            return n;
        }

        public static void DeleteDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
```

`src/GK2ModInstaller.Core/WorkshopLoader.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GK2ModInstaller.Core
{
    public sealed class LoaderOptions
    {
        public string WorkshopRoot { get; set; }
        public string WorkshopAcfPath { get; set; }
        public string BepInExRoot { get; set; }
    }

    public sealed class LoaderSummary
    {
        public int Items, Approved, Updates, Blocked, Postponed, Removed, Migrated, Duplicates, Findings;

        public override string ToString()
        {
            return string.Format(
                "Workshop: айтемов {0}, одобрено {1}, обновлений {2}, заблокировано {3}, отложено {4}, удалено {5}, миграция {6}, предупреждений {7}.",
                Items, Approved, Updates, Blocked, Postponed, Removed, Migrated, Findings);
        }
    }

    public static class WorkshopLoader
    {
        public const string StagingDirName = "_Workshop";
        public const string TrustFileName = "GK2_WorkshopLoader.trust.txt";
        public const string PendingFileName = "GK2_WorkshopLoader.pending.txt";

        public static LoaderSummary Run(LoaderOptions options, IDialog dialog, Action<string> log)
        {
            var summary = new LoaderSummary();
            if (options == null) { log?.Invoke("Workshop: нет опций — пропуск"); return summary; }
            if (string.IsNullOrEmpty(options.WorkshopRoot) || !Directory.Exists(options.WorkshopRoot))
            {
                log?.Invoke("Workshop: папка не найдена — " + options.WorkshopRoot);
                return summary;
            }
            if (string.IsNullOrEmpty(options.BepInExRoot) || !Directory.Exists(options.BepInExRoot))
            {
                log?.Invoke("Workshop: BepInEx не найден — " + options.BepInExRoot);
                return summary;
            }

            var pluginsDir = Path.Combine(options.BepInExRoot, "plugins");
            var configDir = Path.Combine(options.BepInExRoot, "config");
            var stagingRoot = Path.Combine(pluginsDir, StagingDirName);
            var trustPath = Path.Combine(configDir, TrustFileName);
            Directory.CreateDirectory(stagingRoot);

            var trust = TrustStore.Load(trustPath, log);
            var items = WorkshopItemsScanner.Scan(options.WorkshopRoot, log);
            var stagedIds = Directory.GetDirectories(stagingRoot).Select(Path.GetFileName).ToList();
            var manualAssemblies = CollectManualAssemblies(pluginsDir);

            summary.Items = items.Count;
            var plan = ConsentPlanner.Build(
                items, trust,
                item => ModFingerprint.Compute(item.PluginsDir),
                item => ScanItem(item),
                manualAssemblies, stagedIds);

            foreach (var entry in plan)
            {
                summary.Duplicates += entry.Duplicates != null ? entry.Duplicates.Count : 0;
                summary.Findings += entry.Findings != null ? entry.Findings.Count : 0;
                try
                {
                    ApplyKnownOrBlocked(entry, stagingRoot, configDir, log);
                }
                catch (Exception ex)
                {
                    log?.Invoke("Workshop: айтем " + entry.Item.Id + " — ошибка: " + ex.Message);
                }
            }

            if (summary.Removed > 0 || summary.Blocked > 0) trust.Save(trustPath);
            log?.Invoke(summary.ToString());
            return summary;
        }

        private static void ApplyKnownOrBlocked(PlanEntry entry, string stagingRoot, string configDir, Action<string> log)
        {
            var target = Path.Combine(stagingRoot, entry.Item.Id);
            switch (entry.Kind)
            {
                case DecisionKind.Known:
                    // Уже одобрено: копируем, только если копии нет (первый запуск после ручной чистки).
                    if (!Directory.Exists(target))
                    {
                        WorkshopSync.CopyDir(entry.Item.PluginsDir, target);
                        CopyItemConfigs(entry.Item, configDir, log);
                        log?.Invoke("Workshop: восстановлена копия одобренного мода " + entry.Item.Id);
                    }
                    break;
                case DecisionKind.Blocked:
                    WorkshopSync.DeleteDir(target);
                    log?.Invoke("Workshop: заблокированный мод не грузим — " + entry.Item.Id);
                    break;
                case DecisionKind.Removed:
                    WorkshopSync.DeleteDir(target);
                    log?.Invoke("Workshop: мод отписан, копия удалена — " + entry.Item.Id);
                    break;
                default:
                    // New/Update обрабатываются в Task 9.
                    break;
            }
        }

        private static void CopyItemConfigs(WorkshopItem item, string configDir, Action<string> log)
        {
            var src = Path.Combine(item.Dir, "BepInEx", "config");
            WorkshopSync.CopyConfigs(src, configDir, log);
        }

        private static IReadOnlyList<Finding> ScanItem(WorkshopItem item)
        {
            var all = new List<Finding>();
            if (item.DllFiles == null) return all;
            foreach (var dll in item.DllFiles)
            {
                var findings = CodeScan.Scan(dll);
                if (findings != null) all.AddRange(findings);
            }
            return all;
        }

        private static List<string> CollectManualAssemblies(string pluginsDir)
        {
            var names = new List<string>();
            if (!Directory.Exists(pluginsDir)) return names;
            foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll", SearchOption.AllDirectories))
            {
                var rel = dll.Substring(pluginsDir.Length).TrimStart('\\', '/');
                if (rel.StartsWith(StagingDirName, StringComparison.OrdinalIgnoreCase)) continue;
                var info = AssemblyInfoReader.TryRead(dll, null);
                if (!string.IsNullOrEmpty(info.AssemblyName)) names.Add(info.AssemblyName);
            }
            return names;
        }
    }
}
```

- [ ] **Step 5: Удалить старые тесты синка**

```bash
git rm tests/GK2ModInstaller.Tests/WorkshopSyncTests.cs
```

- [ ] **Step 6: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: все тесты проходят (в т.ч. 5 новых `WorkshopLoaderTests`), сборка без ошибок.

- [ ] **Step 7: Коммит**

```bash
git add -A
git commit -m "feat(core): WorkshopLoader applies known/blocked/removed decisions; WorkshopSync becomes file-ops helper"
```

---

### Task 9: WorkshopLoader — новые моды и обновления (диалог, deny, later, fail-closed)

**Files:**
- Modify: `src/GK2ModInstaller.Core/WorkshopLoader.cs`
- Test: `tests/GK2ModInstaller.Tests/WorkshopLoaderTests.cs` (добавить тесты)

**Interfaces:**
- Consumes: `ConsentAnswer`, `ModPrompt`, `PendingList` (Task 7), `ApplyKnownOrBlocked` (Task 8).
- Produces: поведение `Run` для `New`/`Update`; приватный `ConsentAndApply(PlanEntry, …)`; `LoaderSummary.Approved/Updates/Postponed`.

- [ ] **Step 1: Добавить падающие тесты**

Дописать в `tests/GK2ModInstaller.Tests/WorkshopLoaderTests.cs`:
```csharp
        private static LoaderOptions OptionsForNewMod(string root, string id, string dll, out FakeDialog dialog)
        {
            var options = Options(root);
            Directory.CreateDirectory(options.WorkshopRoot);
            MakeWorkshop.Item(options.WorkshopRoot, id, dll, "mod.cfg");
            dialog = new FakeDialog();
            return options;
        }

        [Fact]
        public void New_mod_approved_is_copied_and_recorded_in_trust()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "444", "MOD", out dialog);
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal(new[] { "444" }, dialog.Asked.ToArray());
                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "444"), "Mod.dll")));
                var entry = TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("444");
                Assert.Equal(TrustState.Approved, entry.State);
                Assert.Equal(ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "444", "BepInEx", "plugins")), entry.Sha256);
                Assert.Equal(1, summary.Approved);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void New_mod_denied_is_not_copied_and_blocked_in_trust()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "555", "MOD", out dialog);
                dialog.OnAsk = _ => ConsentAnswer.Deny;
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "555")));
                Assert.Equal(TrustState.Blocked,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("555").State);
                Assert.Equal(1, summary.Blocked);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void New_mod_later_is_not_copied_and_goes_to_pending()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "666", "MOD", out dialog);
                dialog.OnAsk = _ => ConsentAnswer.Later;
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "666")));
                string pending = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.PendingFileName);
                Assert.True(File.Exists(pending));
                Assert.Contains("666", File.ReadAllText(pending));
                Assert.Equal(1, summary.Postponed);
                Assert.Equal(TrustState.Ask,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("666").State);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Null_dialog_means_later_and_nothing_is_copied()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "777", "MOD", out dialog);
                var summary = WorkshopLoader.Run(options, null, null);

                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "777")));
                Assert.Equal(1, summary.Postponed);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Update_later_keeps_previous_approved_version_running()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "888", "NEW", out dialog);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "888", "OLD");
                string trustPath = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trustPath, null);
                store.Set(new TrustEntry { Id = "888", Sha256 = "старый-хеш", State = TrustState.Approved, Title = "Mod" });
                store.Save(trustPath);
                dialog.OnAsk = _ => ConsentAnswer.Later;

                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal("OLD", File.ReadAllText(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "888"), "Mod.dll")));
                Assert.Equal(1, summary.Postponed);
                Assert.Equal(0, summary.Updates);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Update_approved_replaces_files_and_updates_hash()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "999", "NEW", out dialog);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "999", "OLD");
                string trustPath = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trustPath, null);
                store.Set(new TrustEntry { Id = "999", Sha256 = "старый-хеш", State = TrustState.Approved, Title = "Mod" });
                store.Save(trustPath);

                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal("NEW", File.ReadAllText(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "999"), "Mod.dll")));
                Assert.Equal(1, summary.Updates);
                Assert.Equal(ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "999", "BepInEx", "plugins")),
                    TrustStore.Load(trustPath, null).Get("999").Sha256);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Findings_are_reported_in_log_and_summary()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                FakeDialog dialog;
                var options = OptionsForNewMod(root, "1000", "MOD", out dialog);
                dialog.OnAsk = _ => ConsentAnswer.Later;
                var logs = new System.Collections.Generic.List<string>();
                var summary = WorkshopLoader.Run(options, dialog, logs.Add);
                Assert.True(summary.Findings >= 0);
                Assert.Contains(logs, l => l.Contains("Workshop:"));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }
```

- [ ] **Step 2: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~WorkshopLoaderTests"`
Expected: падают новые тесты (`New_mod_approved_is_copied_and_recorded_in_trust` и др.) — `summary.Approved == 0`, файлы не скопированы.

- [ ] **Step 3: Реализовать**

В `src/GK2ModInstaller.Core/WorkshopLoader.cs`:
1) заменить блок `ApplyKnownOrBlocked` на вызов консента для New/Update — в цикле `foreach (var entry in plan)`:
```csharp
                try
                {
                    if (entry.Kind == DecisionKind.New || entry.Kind == DecisionKind.Update)
                        ConsentAndApply(entry, stagingRoot, configDir, trust, dialog, summary, log);
                    else
                        ApplyKnownOrBlocked(entry, stagingRoot, configDir, log);
                }
```
2) добавить методы:
```csharp
        private static void ConsentAndApply(PlanEntry entry, string stagingRoot, string configDir,
            TrustStore trust, IDialog dialog, LoaderSummary summary, Action<string> log)
        {
            var target = Path.Combine(stagingRoot, entry.Item.Id);
            var prompt = new ModPrompt
            {
                Id = entry.Item.Id,
                Title = entry.Item.Title,
                Version = entry.Item.Version,
                IsUpdate = entry.Kind == DecisionKind.Update,
                Files = entry.Item.DllFiles != null ? entry.Item.DllFiles.Select(Path.GetFileName).ToList() : new List<string>(),
                Findings = entry.Findings ?? new List<Finding>(),
                Duplicates = entry.Duplicates ?? new List<string>()
            };

            ConsentAnswer answer = ConsentAnswer.Later;
            try
            {
                if (dialog != null) answer = dialog.Ask(prompt);
            }
            catch (Exception ex)
            {
                log?.Invoke("Workshop: диалог недоступен (" + ex.Message + ") — мод отложен: " + entry.Item.Id);
                answer = ConsentAnswer.Later;
            }

            if (answer == ConsentAnswer.Approve)
            {
                WorkshopSync.DeleteDir(target);
                WorkshopSync.CopyDir(entry.Item.PluginsDir, target);
                CopyItemConfigs(entry.Item, configDir, log);
                trust.Set(new TrustEntry
                {
                    Id = entry.Item.Id,
                    Sha256 = entry.Fingerprint,
                    State = TrustState.Approved,
                    Title = entry.Item.Title,
                    Note = (entry.Kind == DecisionKind.Update ? "обновление " : "одобрено ") + DateTime.Now.ToString("yyyy-MM-dd")
                });
                if (entry.Kind == DecisionKind.Update) summary.Updates++; else summary.Approved++;
                log?.Invoke("Workshop: одобрен мод " + entry.Item.Id + " (" + entry.Item.Title + ")");
            }
            else if (answer == ConsentAnswer.Deny)
            {
                WorkshopSync.DeleteDir(target);
                trust.Set(new TrustEntry
                {
                    Id = entry.Item.Id,
                    Sha256 = entry.Fingerprint,
                    State = TrustState.Blocked,
                    Title = entry.Item.Title,
                    Note = "заблокирован " + DateTime.Now.ToString("yyyy-MM-dd")
                });
                summary.Blocked++;
                log?.Invoke("Workshop: заблокирован мод " + entry.Item.Id);
            }
            else
            {
                trust.Set(new TrustEntry
                {
                    Id = entry.Item.Id,
                    Sha256 = entry.Fingerprint,
                    State = TrustState.Ask,
                    Title = entry.Item.Title,
                    Note = "отложено " + DateTime.Now.ToString("yyyy-MM-dd")
                });
                summary.Postponed++;
                pending.Add(prompt);
                log?.Invoke("Workshop: отложен мод " + entry.Item.Id);
            }
        }
```
3) в начале `Run` добавить `var pending = new List<ModPrompt>();`, а после цикла — сохранить pending-файл и trust:
```csharp
            if (pending.Count > 0)
                PendingList.Write(Path.Combine(configDir, PendingFileName), pending, log);
            if (pending.Count > 0 || summary.Approved > 0 || summary.Updates > 0 || summary.Blocked > 0 || summary.Removed > 0)
                trust.Save(trustPath);
```
4) удалить прежнюю строку `if (summary.Removed > 0 || summary.Blocked > 0) trust.Save(trustPath);`
5) добавить `using System.Linq;` (уже есть) и `using System.Collections.Generic;` (уже есть).

- [ ] **Step 4: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: все тесты проходят.

- [ ] **Step 5: Коммит**

```bash
git add -A
git commit -m "feat(core): consent dialogs for new mods and updates (approve/deny/later, pending, fail-closed)"
```

---

### Task 10: Миграция уже установленных модов + ACF-лог

**Files:**
- Modify: `src/GK2ModInstaller.Core/WorkshopLoader.cs`
- Test: `tests/GK2ModInstaller.Tests/WorkshopLoaderTests.cs` (добавить тесты)

**Interfaces:**
- Consumes: `BulkAnswer`, `AcfTimes`.
- Produces: сводный диалог миграции; `LoaderSummary.Migrated`.

- [ ] **Step 1: Добавить падающие тесты**

Дописать в `tests/GK2ModInstaller.Tests/WorkshopLoaderTests.cs`:
```csharp
        [Fact]
        public void Migration_bulk_yes_trusts_all_and_does_not_ask_each()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", null);
                MakeWorkshop.Item(options.WorkshopRoot, "222", "MOD2", null);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "111", "MOD");
                MakeWorkshop.MakeStaged(options.BepInExRoot, "222", "MOD2");

                var dialog = new FakeDialog();
                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal(new[] { "bulk:111", "bulk:222" }, dialog.Asked.ToArray());
                var store = TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null);
                Assert.Equal(TrustState.Approved, store.Get("111").State);
                Assert.Equal(TrustState.Approved, store.Get("222").State);
                Assert.Equal(2, summary.Migrated);
                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "111"), "Mod.dll")));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Migration_bulk_no_asks_each_separately()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", null);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "111", "MOD");
                var dialog = new FakeDialog();
                dialog.OnBulk = _ => BulkAnswer.AskEach;
                dialog.OnAsk = _ => ConsentAnswer.Deny;

                var summary = WorkshopLoader.Run(options, dialog, null);

                Assert.Equal(new[] { "bulk:111", "111" }, dialog.Asked.ToArray());
                Assert.Equal(TrustState.Blocked,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("111").State);
                Assert.False(Directory.Exists(MakeWorkshop.Staging(options.BepInExRoot, "111")));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Migration_bulk_later_keeps_files_and_asks_again_next_time()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", null);
                MakeWorkshop.MakeStaged(options.BepInExRoot, "111", "MOD");
                var dialog = new FakeDialog();
                dialog.OnBulk = _ => BulkAnswer.Later;

                WorkshopLoader.Run(options, dialog, null);

                Assert.True(File.Exists(Path.Combine(MakeWorkshop.Staging(options.BepInExRoot, "111"), "Mod.dll")));
                Assert.Equal(TrustState.Ask,
                    TrustStore.Load(Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName), null).Get("111").State);
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }

        [Fact]
        public void Acf_newer_timeupdated_is_logged_for_known_mod()
        {
            string root = MakeWorkshop.Tmp();
            try
            {
                var options = Options(root);
                Directory.CreateDirectory(options.WorkshopRoot);
                MakeWorkshop.Item(options.WorkshopRoot, "111", "MOD", null);
                string fp = ModFingerprint.Compute(Path.Combine(options.WorkshopRoot, "111", "BepInEx", "plugins"));
                string trust = Path.Combine(options.BepInExRoot, "config", WorkshopLoader.TrustFileName);
                var store = TrustStore.Load(trust, null);
                store.Set(new TrustEntry { Id = "111", Sha256 = fp, State = TrustState.Approved, Title = "Mod", Note = "2026-09-01" });
                store.Save(trust);
                File.WriteAllText(options.WorkshopAcfPath,
                    "\"AppWorkshop\"\n{\n\t\"WorkshopItemsInstalled\"\n\t{\n\t\t\"111\"\n\t\t{\n\t\t\t\"timeupdated\"\t\t\"3200000000\"\n\t\t}\n\t}\n}\n");
                MakeWorkshop.MakeStaged(options.BepInExRoot, "111", "MOD");

                var logs = new System.Collections.Generic.List<string>();
                WorkshopLoader.Run(options, new FakeDialog(), logs.Add);

                Assert.Contains(logs, l => l.Contains("111") && l.Contains("ACF"));
            }
            finally { MakeWorkshop.SafeDelete(root); }
        }
```

- [ ] **Step 2: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~WorkshopLoaderTests"`
Expected: падают `Migration_*` (нет сводного диалога) и `Acf_newer_timeupdated_is_logged_for_known_mod`.

- [ ] **Step 3: Реализовать**

В `src/GK2ModInstaller.Core/WorkshopLoader.cs`:
1) после `ConsentPlanner.Build(...)` и перед `foreach (var entry in plan)` — блок миграции:
```csharp
            var migration = plan.Where(p => p.Kind == DecisionKind.New && p.Staged).ToList();
            var migratedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (migration.Count > 0)
            {
                var prompts = migration.Select(ToPrompt).ToList();
                BulkAnswer bulk = BulkAnswer.Later;
                try { if (dialog != null) bulk = dialog.AskBulkTrust(prompts); }
                catch (Exception ex) { log?.Invoke("Workshop: сводный диалог недоступен (" + ex.Message + ")"); }

                foreach (var p in migration)
                {
                    if (bulk == BulkAnswer.All)
                    {
                        trust.Set(new TrustEntry
                        {
                            Id = p.Item.Id,
                            Sha256 = p.Fingerprint,
                            State = TrustState.Approved,
                            Title = p.Item.Title,
                            Note = "миграция " + DateTime.Now.ToString("yyyy-MM-dd")
                        });
                        migratedIds.Add(p.Item.Id);
                        summary.Migrated++;
                    }
                    else if (bulk == BulkAnswer.Later)
                    {
                        trust.Set(new TrustEntry { Id = p.Item.Id, Sha256 = p.Fingerprint, State = TrustState.Ask, Title = p.Item.Title, Note = "миграция отложена" });
                    }
                    // BulkAnswer.AskEach — спрашиваем индивидуально в общем цикле (kind остаётся New)
                }
                if (bulk != BulkAnswer.AskEach) log?.Invoke("Workshop: миграция прежних установок — " + migration.Count + " шт., ответ: " + bulk);
            }
```
2) в основном цикле пропускать уже мигрировавшие:
```csharp
                if (migratedIds.Contains(entry.Item.Id)) continue;
```
3) вынести сборку промпта в метод и использовать его и в `ConsentAndApply`:
```csharp
        private static ModPrompt ToPrompt(PlanEntry entry)
        {
            return new ModPrompt
            {
                Id = entry.Item.Id,
                Title = entry.Item.Title,
                Version = entry.Item.Version,
                IsUpdate = entry.Kind == DecisionKind.Update,
                Files = entry.Item.DllFiles != null ? entry.Item.DllFiles.Select(Path.GetFileName).ToList() : new List<string>(),
                Findings = entry.Findings ?? new List<Finding>(),
                Duplicates = entry.Duplicates ?? new List<string>()
            };
        }
```
и заменить в `ConsentAndApply` локальный `var prompt = new ModPrompt {...}` на `var prompt = ToPrompt(entry);`.
4) ACF-лог: после сохранения trust в конце `Run`:
```csharp
            LogAcfUpdates(options, plan, log);
```
и метод:
```csharp
        private static void LogAcfUpdates(LoaderOptions options, List<PlanEntry> plan, Action<string> log)
        {
            try
            {
                if (string.IsNullOrEmpty(options.WorkshopAcfPath) || !File.Exists(options.WorkshopAcfPath)) return;
                var times = AcfTimes.Parse(File.ReadAllText(options.WorkshopAcfPath));
                foreach (var entry in plan)
                {
                    long t;
                    if (!times.TryGetValue(entry.Item.Id, out t)) continue;
                    log?.Invoke("Workshop: ACF — у мода " + entry.Item.Id + " timeupdated=" + t);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("Workshop: ACF не разобран (" + ex.Message + ")");
            }
        }
```

- [ ] **Step 4: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: все тесты проходят.

- [ ] **Step 5: Коммит**

```bash
git add -A
git commit -m "feat(core): bulk migration dialog for previously installed mods; acf timeupdated log"
```

---

### Task 11: Win32Dialog + подключение в патчер

**Files:**
- Create: `src/GK2ModInstaller.Patcher/Win32Dialog.cs`
- Modify: `src/GK2ModInstaller.Patcher/WorkshopAutoLoaderPatcher.cs`
- Modify: `src/GK2ModInstaller.Patcher/GK2ModInstaller.Patcher.csproj` (линки Core-файлов)
- Test: сборка + ручной E2E (ниже)

**Interfaces:**
- Consumes: `IDialog`, `ModPrompt`, `ConsentAnswer`, `BulkAnswer`, `WorkshopLoader`, `LoaderOptions` (Core).
- Produces: `sealed class Win32Dialog : IDialog` (патчер).

- [ ] **Step 1: Добавить линки Core-файлов в проект патчера**

В `src/GK2ModInstaller.Patcher/GK2ModInstaller.Patcher.csproj` заменить единственный `<Compile Include="..\GK2ModInstaller.Core\WorkshopSync.cs" .../>` на:
```xml
    <Compile Include="..\GK2ModInstaller.Core\WorkshopSync.cs" Link="WorkshopSync.cs" />
    <Compile Include="..\GK2ModInstaller.Core\WorkshopLoader.cs" Link="WorkshopLoader.cs" />
    <Compile Include="..\GK2ModInstaller.Core\WorkshopItems.cs" Link="WorkshopItems.cs" />
    <Compile Include="..\GK2ModInstaller.Core\TrustStore.cs" Link="TrustStore.cs" />
    <Compile Include="..\GK2ModInstaller.Core\ModFingerprint.cs" Link="ModFingerprint.cs" />
    <Compile Include="..\GK2ModInstaller.Core\CodeScan.cs" Link="CodeScan.cs" />
    <Compile Include="..\GK2ModInstaller.Core\ConsentPlanner.cs" Link="ConsentPlanner.cs" />
    <Compile Include="..\GK2ModInstaller.Core\AcfTimes.cs" Link="AcfTimes.cs" />
    <Compile Include="..\GK2ModInstaller.Core\LoaderDialog.cs" Link="LoaderDialog.cs" />
    <Compile Include="..\GK2ModInstaller.Core\PendingList.cs" Link="PendingList.cs" />
```

- [ ] **Step 2: Написать Win32Dialog**

`src/GK2ModInstaller.Patcher/Win32Dialog.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using GK2ModInstaller.Core;

namespace GK2ModInstaller.Patcher
{
    // Диалог через user32.MessageBoxW: WinForms в игре нет, а P/Invoke работает.
    public sealed class Win32Dialog : IDialog
    {
        private const uint MB_YESNOCANCEL = 0x00000003;
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONWARNING = 0x00000030;
        private const uint MB_ICONINFORMATION = 0x00000040;
        private const uint MB_TOPMOST = 0x00040000;
        private const uint MB_SETFOREGROUND = 0x00010000;
        private const int IDYES = 6;
        private const int IDNO = 7;
        private const string Caption = "GK2 Workshop Loader";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        public ConsentAnswer Ask(ModPrompt prompt)
        {
            string header = prompt.IsUpdate ? "Обновление мода из Workshop" : "Новый мод из Workshop";
            int r = MessageBoxW(IntPtr.Zero, prompt.Text(header), Caption,
                MB_YESNOCANCEL | MB_ICONWARNING | MB_TOPMOST | MB_SETFOREGROUND);
            if (r == IDYES) return ConsentAnswer.Approve;
            if (r == IDNO) return ConsentAnswer.Deny;
            return ConsentAnswer.Later;
        }

        public BulkAnswer AskBulkTrust(IReadOnlyList<ModPrompt> mods)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Найдено " + mods.Count + " мод(ов), установленных прежней версией автозагрузчика — без вашего согласия.");
            sb.AppendLine("Они уже стоят в BepInEx\\plugins\\_Workshop и работают. Доверять им всем?");
            sb.AppendLine();
            foreach (var m in mods) sb.AppendLine("• " + m.Title + " (id " + m.Id + ")");
            sb.AppendLine();
            sb.AppendLine("Yes — доверять всем · No — спросить про каждый отдельно · Cancel — оставить как есть и спросить позже");

            int r = MessageBoxW(IntPtr.Zero, sb.ToString(), Caption,
                MB_YESNOCANCEL | MB_ICONWARNING | MB_TOPMOST | MB_SETFOREGROUND);
            if (r == IDYES) return BulkAnswer.All;
            if (r == IDNO) return BulkAnswer.AskEach;
            return BulkAnswer.Later;
        }

        public void Warn(string text)
        {
            MessageBoxW(IntPtr.Zero, text, Caption, MB_OK | MB_ICONINFORMATION | MB_TOPMOST | MB_SETFOREGROUND);
        }
    }
}
```

- [ ] **Step 3: Подключить в патчере**

`src/GK2ModInstaller.Patcher/WorkshopAutoLoaderPatcher.cs` — заменить целиком:
```csharp
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using GK2ModInstaller.Core;
using Mono.Cecil;

namespace GK2ModInstaller.Patcher
{
    public static class WorkshopAutoLoaderPatcher
    {
        private const string WorkshopId = "4358690";

        public static IEnumerable<string> TargetDLLs { get { yield return "Assembly-CSharp.dll"; } }

        public static void Patch(AssemblyDefinition assembly)
        {
            var log = Logger.CreateLogSource("GK2.WorkshopLoader");
            string bep = Paths.BepInExRootPath;
            string steamapps = FindSteamAppsRoot(Directory.GetParent(bep)?.FullName);
            string workshop = steamapps == null ? null : Path.Combine(steamapps, "workshop", "content", WorkshopId);
            string acf = steamapps == null ? null : Path.Combine(steamapps, "workshop", "appworkshop_" + WorkshopId + ".acf");
            log.LogInfo("Workshop root: " + (workshop ?? "<не найден>"));

            var options = new LoaderOptions { WorkshopRoot = workshop, WorkshopAcfPath = acf, BepInExRoot = bep };
            try
            {
                WorkshopLoader.Run(options, new Win32Dialog(), log.LogInfo);
            }
            catch (System.Exception ex)
            {
                log.LogError("Автозагрузка Workshop упала: " + ex);
            }
        }

        private static string FindSteamAppsRoot(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir)) return null;
            var dir = new DirectoryInfo(gameDir);
            while (dir != null && !dir.Name.Equals("steamapps", System.StringComparison.OrdinalIgnoreCase))
                dir = dir.Parent;
            return dir == null ? null : dir.FullName;
        }
    }
}
```

- [ ] **Step 4: Собрать решение и прогнать тесты**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release` (workdir `GK2ModInstaller`), затем `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: `Сборка успешно завершена`, все тесты проходят; `src\GK2ModInstaller.App\bin\Release\GK2.WorkshopAutoLoader.dll` обновился (MSBuild-таргет `BuildAndCopyPatcher`).

- [ ] **Step 5: Ручной E2E (документировать результат в коммите)**

1. Скопировать `GK2.WorkshopAutoLoader.dll` в `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2\BepInEx\patchers\`; очистить `BepInEx\cache`.
2. Запустить игру → должен появиться сводный диалог миграции (для уже стоящих Workshop-модов) → `Yes`.
3. Проверить `BepInEx\config\GK2_WorkshopLoader.trust.txt` — строки `yes` с хешами; в `LogOutput.log` — сводка `Workshop: ...`.
4. Подписаться на новый код-мод → запуск → диалог нового мода → `Cancel` → в игре мода нет, он в `GK2_WorkshopLoader.pending.txt`.
5. Повторный запуск → тот же диалог → `Yes` → мод загрузился, строка `yes`.
6. Удалить строку мода из trust → запуск → снова диалог.

- [ ] **Step 6: Коммит**

```bash
git add -A
git commit -m "feat(patcher): Win32 consent dialog wired into workshop loader"
```

---

### Task 12: Документация и релиз v1.2.0

**Files:**
- Modify: `README.md`
- Modify: `dist/RELEASE_NOTES.md`
- Modify: `E:\GK2Upload\GK2WorkshopAutoLoader\README.txt` (описание для Workshop — вне git)
- Modify: `E:\GK2Upload\GK2WorkshopAutoLoader\BepInEx\patchers\GK2.WorkshopAutoLoader.dll` (залить новую)

**Interfaces:**
- Consumes: всё предыдущее.
- Produces: обновлённые README/описание, опубликованный GitHub Release и Workshop-айтем.

- [ ] **Step 1: README — секция про согласие**

Добавить в `README.md` после секции про автозагрузку:
```markdown
## Согласие на моды (с версии 1.2.0)

Автозагрузчик больше не запускает мод без вашего решения:

- **Новый мод** — при первом запуске диалог: `Yes` одобрить, `No` заблокировать навсегда,
  `Cancel` спросить в следующий раз.
- **Обновление мода** — тоже спрашивает; при `Cancel` продолжает работать прежняя одобренная версия.
- **Миграция** — моды, поставленные прежней версией, один раз спросят сводно.
- **Проверка кода** — перед решением показываем, использует ли мод сеть, запуск процессов,
  удаление файлов, загрузку кода, реестр или Win32. Это подсказка, а не гарантия.
- **Дубликаты** — предупреждаем, если тот же мод стоит ещё и вручную в `BepInEx\plugins`.

Решения хранятся в `BepInEx\config\GK2_WorkshopLoader.trust.txt` (строки `id|sha256|yes|no|ask|...`).
Удалите строку — мод спросят заново. Моды, ожидающие решения, перечислены в
`BepInEx\config\GK2_WorkshopLoader.pending.txt`. Совпадает всё — мод не трогаем.

Если диалог не появился (например, игра стартует в фоне) — нажмите Alt+Tab: окно могло уйти назад.
```

- [ ] **Step 2: RELEASE_NOTES**

Дописать в `dist/RELEASE_NOTES.md`:
```markdown
## v1.2.0

- Согласие на моды: диалог для новых модов и обновлений (Yes / No / Cancel).
- Trust-файл `GK2_WorkshopLoader.trust.txt`: решения можно править вручную.
- Отложенное обновление не мешает работать прежней одобренной версии.
- Сводный диалог миграции для модов, поставленных прежними версиями.
- Статическая проверка кода (сеть, процессы, удаление файлов, загрузка кода, реестр, Win32).
- Предупреждение о дубликатах (мод стоит и из Workshop, и вручную).
```

- [ ] **Step 3: Собрать и обновить папку загрузки Workshop-айтема**

```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release
Copy-Item "src\GK2ModInstaller.App\bin\Release\GK2.WorkshopAutoLoader.dll" "E:\GK2Upload\GK2WorkshopAutoLoader\BepInEx\patchers\GK2.WorkshopAutoLoader.dll" -Force
```
Затем в `E:\GK2Upload\GK2WorkshopAutoLoader\README.txt` дописать в конец раздел:
```
Согласие на моды (v1.2.0)
-------------------------
Новый мод и обновление не запускаются без вашего решения: диалог Yes (одобрить) /
No (заблокировать навсегда) / Cancel (спросить позже). При Cancel обновления продолжает
работать прежняя одобренная версия. Перед решением показываем результат проверки кода
(сеть, процессы, удаление файлов, загрузка кода, реестр, Win32) и предупреждение о дубликате.
Решения — в BepInEx\config\GK2_WorkshopLoader.trust.txt (удалите строку, чтобы спросили снова),
ожидающие решения — в GK2_WorkshopLoader.pending.txt. Если окна не видно — нажмите Alt+Tab.
```
И в `E:\GK2Upload\description.txt` (описание айтема) заменить блок «Features» на:
```
Features
- Subscribe and play — no more copying BepInEx folders by hand.
- Nothing runs without your consent — new mods need approval (Yes / No / Cancel).
- Updates need approval too — postponed updates keep the previously approved version running.
- Automatic code check — warns if a mod uses the network, starts programs, deletes files, loads extra code, touches the registry or uses Win32.
- Duplicate detection — tells you when a mod is installed both from the Workshop and manually.
- Unsubscribe = removed on the next start.
- Ignores items without a BepInEx plugin (translations etc.).

Решения хранятся в BepInEx\config\GK2_WorkshopLoader.trust.txt (строки id|sha256|yes|no|ask|название|дата);
удалите строку — мод спросят заново. Моды, ожидающие решения, — в GK2_WorkshopLoader.pending.txt.
Если диалог не появился — нажмите Alt+Tab (окно могло уйти за игру).
```

- [ ] **Step 4: Опубликовать обновление Workshop-айтема**

```powershell
& "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\tools\GK2Publisher\bin\Release\GK2Publisher.exe" --update 3807406994 --folder "E:\GK2Upload\GK2WorkshopAutoLoader" --desc-file "E:\GK2Upload\description.txt" --public
```
Expected: `SetItemContent=True`, `SubmitItemUpdate: k_EResultOK item 3807406994`. Публикацию выполнять только после «ок» пользователя.

- [ ] **Step 5: GitHub Release v1.2.0**

```powershell
Set-Location "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller"
git tag v1.2.0; git push origin master; git push origin v1.2.0
& "C:\Program Files\GitHub CLI\gh.exe" release create v1.2.0 "dist\GK2ModInstaller.exe" --title "GK2 Mod Installer v1.2.0" --notes-file "dist\RELEASE_NOTES.md"
```

- [ ] **Step 6: Коммит и запись в AGENTS.md**

```bash
git add -A
git commit -m "docs: release notes and README for loader consent (v1.2.0)"
```
Дописать в `C:\Users\Проньки\Documents\OpenCode\AGENTS.md` в раздел GK2: trust-файл, форматы, где лежит диалог, версия патчера.
