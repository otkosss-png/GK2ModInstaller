# GK2 Mod Installer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GUI-установщик, который в пару кликов ставит BepInEx 5.4.23.5 x64 + GK2 Mod Framework в папку Graveyard Keeper 2.

**Architecture:** Три проекта — `Core` (netstandard2.0, чистая логика: поиск игры, распаковка, установка/проверка/удаление), `App` (net48 WinForms, вшитые архивы BepInEx/фреймворка, UI), `Tests` (net8.0 xUnit на `Core`). Приложение референсит Core; релиз — один exe.

**Tech Stack:** C#, .NET Framework 4.8 (WinForms), .NET Standard 2.0, xUnit, `System.IO.Compression.ZipArchive`, реестр Steam.

**Spec:** `docs/superpowers/specs/2026-09-24-gk2-mod-installer-design.md`

## Global Constraints

- Целевой фреймворк App = **net48**; Core = **netstandard2.0** (без NuGet); Tests = **net8.0**.
- Сборка/тесты: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release` и `... test -c Release` (workdir проекта).
- Игра: appid **4358690**, exe `GraveyardKeeper2.exe`, валидация папки = есть `GraveyardKeeper2.exe` + `UnityPlayer.dll` + `GraveyardKeeper2_Data\Managed\Assembly-CSharp.dll`.
- BepInEx-архив: `BepInEx_win_x64_5.4.23.5.zip` (639 118 б.), URL `https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip`.
- Фреймворк-архив: раскрывается в **корень игры**, внутри пути `BepInEx/plugins/GK2.Framework.dll` + `BepInEx/plugins/GK2.Framework/Localization/...`.
- Удаление BepInEx трогает ТОЛЬКО: `BepInEx\`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt`.
- Коммиты после каждого таска (git init в Task 1). Если пользователь против git — пропускать шаги Commit.

---

## File Structure

- `GK2ModInstaller.sln`
- `src/GK2ModInstaller.Core/GK2ModInstaller.Core.csproj` (netstandard2.0)
  - `VdfParser.cs` — парсинг `libraryfolders.vdf`
  - `GameLocator.cs` — поиск/валидация папки игры
  - `ZipExtractor.cs` — распаковка zip (с защитой от zip-slip)
  - `BepInExInstaller.cs` — Install/Verify/Uninstall
- `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj` (net48 WinExe)
  - `Program.cs`, `MainForm.cs`
  - `Resources/BepInEx_win_x64_5.4.23.5.zip`, `Resources/GK2.Framework.zip` (EmbeddedResource)
  - `Resources/LICENSE.BepInEx.txt`, `Resources/LICENSE.GK2Framework.txt`
- `tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj` (net8.0)
  - `VdfParserTests.cs`, `GameLocatorTests.cs`, `BepInExInstallerTests.cs`

---

### Task 1: Solution scaffold + build

**Files:**
- Create: `GK2ModInstaller.sln`, `src/GK2ModInstaller.Core/GK2ModInstaller.Core.csproj`, `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj`, `tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj`
- Create: `src/GK2ModInstaller.Core/Placeholder.cs`, `src/GK2ModInstaller.App/Program.cs`, `tests/GK2ModInstaller.Tests/PlaceholderTests.cs`

**Interfaces:**
- Produces: сборку Core (netstandard2.0), App (net48 WinExe), Tests (net8.0 xUnit), на которые ссылаются последующие таски.

- [ ] **Step 1: Создать решения и проекты**

```powershell
$root = "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller"
Set-Location $root
New-Item -ItemType Directory -Force -Path src\GK2ModInstaller.Core, src\GK2ModInstaller.App, tests\GK2ModInstaller.Tests | Out-Null
```

`src/GK2ModInstaller.Core/GK2ModInstaller.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
```

`src/GK2ModInstaller.App/GK2ModInstaller.App.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>GK2ModInstaller</AssemblyName>
    <RootNamespace>GK2ModInstaller.App</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\GK2ModInstaller.Core\GK2ModInstaller.Core.csproj" />
  </ItemGroup>
  <!-- Если WinForms не резолвится на net48 — добавить:
       <Reference Include="System.Windows.Forms" /><Reference Include="System.Drawing" /> -->
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

`tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\GK2ModInstaller.Core\GK2ModInstaller.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Заглушки кода**

`src/GK2ModInstaller.Core/Placeholder.cs`:
```csharp
namespace GK2ModInstaller.Core
{
    public static class Placeholder { public const string Version = "0.0.0"; }
}
```

`src/GK2ModInstaller.App/Program.cs`:
```csharp
using System;
using System.Windows.Forms;

namespace GK2ModInstaller.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MessageBox.Show("scaffold");
        }
    }
}
```

`tests/GK2ModInstaller.Tests/PlaceholderTests.cs`:
```csharp
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class PlaceholderTests
    {
        [Fact]
        public void Scaffold_builds() => Assert.Equal("0.0.0", Placeholder.Version);
    }
}
```

- [ ] **Step 3: Создать sln, добавить проекты, собрать и прогнать тесты**

```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" new sln -n GK2ModInstaller
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln GK2ModInstaller.sln add src\GK2ModInstaller.Core\GK2ModInstaller.Core.csproj src\GK2ModInstaller.App\GK2ModInstaller.App.csproj tests\GK2ModInstaller.Tests\GK2ModInstaller.Tests.csproj
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release
```
Expected: build 0 ошибок; `test` — 1 passed.

- [ ] **Step 4: git init + commit**

```powershell
git init
"bin/`nobj/" | Out-File .gitignore -Encoding utf8
git add -A
git commit -m "chore: scaffold GK2ModInstaller solution"
```

---

### Task 2: VdfParser + GameLocator

**Files:**
- Create: `src/GK2ModInstaller.Core/VdfParser.cs`, `src/GK2ModInstaller.Core/GameLocator.cs`
- Test: `tests/GK2ModInstaller.Tests/VdfParserTests.cs`, `tests/GK2ModInstaller.Tests/GameLocatorTests.cs`

**Interfaces:**
- Produces: `VdfParser.ParseLibraryPaths(string) -> IReadOnlyList<string>`;
  `GameLocator.FindGameInSteamRoot(string) -> string`,
  `GameLocator.FindGameInLibrary(string) -> string`,
  `GameLocator.ValidateGameDir(string) -> bool`;
  `GameLocator.GameFolderName`, `GameLocator.GameExeName`.

- [ ] **Step 1: Написать падающие тесты**

`tests/GK2ModInstaller.Tests/VdfParserTests.cs`:
```csharp
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class VdfParserTests
    {
        [Fact]
        public void Parses_library_paths()
        {
            string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"E:\\\\SteamLibrary\"\n\t}\n}";
            var paths = VdfParser.ParseLibraryPaths(vdf).ToArray();
            Assert.Equal(new[] { "C:\\Program Files (x86)\\Steam", "E:\\SteamLibrary" }, paths);
        }

        [Fact]
        public void Empty_text_gives_empty_list()
        {
            Assert.Empty(VdfParser.ParseLibraryPaths(""));
        }
    }
}
```

`tests/GK2ModInstaller.Tests/GameLocatorTests.cs`:
```csharp
using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class GameLocatorTests
    {
        private static string MakeGameDir(string libraryRoot)
        {
            string game = Path.Combine(libraryRoot, "steamapps", "common", GameLocator.GameFolderName);
            Directory.CreateDirectory(Path.Combine(game, "GraveyardKeeper2_Data", "Managed"));
            File.WriteAllText(Path.Combine(game, GameLocator.GameExeName), "x");
            File.WriteAllText(Path.Combine(game, "UnityPlayer.dll"), "x");
            File.WriteAllText(Path.Combine(game, "GraveyardKeeper2_Data", "Managed", "Assembly-CSharp.dll"), "x");
            return game;
        }

        [Fact]
        public void ValidateGameDir_true_for_valid_folder()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "gk2loc_" + Guid.NewGuid().ToString("N"));
            string game = MakeGameDir(tmp);
            try { Assert.True(GameLocator.ValidateGameDir(game)); }
            finally { Directory.Delete(tmp, true); }
        }

        [Fact]
        public void ValidateGameDir_false_for_random_folder()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "gk2loc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try { Assert.False(GameLocator.ValidateGameDir(tmp)); }
            finally { Directory.Delete(tmp, true); }
        }

        [Fact]
        public void FindGameInLibrary_finds_game()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "gk2loc_" + Guid.NewGuid().ToString("N"));
            string game = MakeGameDir(tmp);
            try { Assert.Equal(Path.GetFullPath(game), GameLocator.FindGameInLibrary(tmp)); }
            finally { Directory.Delete(tmp, true); }
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тесты падают**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release` (workdir проекта)
Expected: FAIL — `VdfParser`/`GameLocator` не существуют.

- [ ] **Step 3: Реализовать**

`src/GK2ModInstaller.Core/VdfParser.cs`:
```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GK2ModInstaller.Core
{
    public static class VdfParser
    {
        private static readonly Regex PathLine =
            new Regex("\"path\"\\s+\"(?<v>(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

        public static IReadOnlyList<string> ParseLibraryPaths(string vdfText)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(vdfText)) return result;
            foreach (Match m in PathLine.Matches(vdfText))
            {
                var path = m.Groups["v"].Value.Replace("\\\\", "\\");
                if (!string.IsNullOrWhiteSpace(path) && !result.Contains(path)) result.Add(path);
            }
            return result;
        }
    }
}
```

`src/GK2ModInstaller.Core/GameLocator.cs`:
```csharp
using System.Collections.Generic;
using System.IO;

namespace GK2ModInstaller.Core
{
    public static class GameLocator
    {
        public const string GameFolderName = "Graveyard Keeper 2";
        public const string GameExeName = "GraveyardKeeper2.exe";

        public static bool ValidateGameDir(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;
            return File.Exists(Path.Combine(dir, GameExeName))
                && File.Exists(Path.Combine(dir, "UnityPlayer.dll"))
                && File.Exists(Path.Combine(dir, "GraveyardKeeper2_Data", "Managed", "Assembly-CSharp.dll"));
        }

        public static string FindGameInLibrary(string libraryRoot)
        {
            if (string.IsNullOrWhiteSpace(libraryRoot)) return null;
            var candidate = Path.Combine(libraryRoot, "steamapps", "common", GameFolderName);
            return ValidateGameDir(candidate) ? Path.GetFullPath(candidate) : null;
        }

        public static string FindGameInSteamRoot(string steamRoot)
        {
            if (string.IsNullOrWhiteSpace(steamRoot)) return null;
            var roots = new List<string> { steamRoot };
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            try { if (File.Exists(vdf)) roots.AddRange(VdfParser.ParseLibraryPaths(File.ReadAllText(vdf))); }
            catch { }
            foreach (var r in roots)
            {
                var found = FindGameInLibrary(r);
                if (found != null) return found;
            }
            return null;
        }
    }
}
```

- [ ] **Step 4: Тесты проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: PASS (все тесты).

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat(core): vdf parser and game locator"
```

---

### Task 3: ZipExtractor + BepInExInstaller

**Files:**
- Create: `src/GK2ModInstaller.Core/ZipExtractor.cs`, `src/GK2ModInstaller.Core/BepInExInstaller.cs`
- Test: `tests/GK2ModInstaller.Tests/BepInExInstallerTests.cs`

**Interfaces:**
- Consumes: `GameLocator.GameExeName` (не обязательно).
- Produces: `ZipExtractor.ExtractTo(Stream, string)`;
  `BepInExInstaller.IsGameRunning() -> bool`,
  `Verify(string gameDir) -> IReadOnlyList<string>` (пусто = ок),
  `Install(string gameDir, Stream bepinexZip, Stream frameworkZip, bool backupExisting, Action<string> log)`,
  `Uninstall(string gameDir, Action<string> log)`,
  `BepInExInstaller.BepInExDirName`, `BepInExInstaller.BepInExRootFiles`.

- [ ] **Step 1: Написать падающие тесты**

`tests/GK2ModInstaller.Tests/BepInExInstallerTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class BepInExInstallerTests
    {
        private static MemoryStream Zip(params (string name, string content)[] files)
        {
            var ms = new MemoryStream();
            using (var a = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var f in files)
                {
                    var e = a.CreateEntry(f.name);
                    using (var s = e.Open()) using (var w = new StreamWriter(s)) w.Write(f.content);
                }
            ms.Position = 0;
            return ms;
        }

        [Fact]
        public void Extract_writes_nested_files()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2zip_" + Guid.NewGuid().ToString("N"));
            try
            {
                ZipExtractor.ExtractTo(Zip(("a.txt", "A"), ("sub/b.txt", "B")), dir);
                Assert.Equal("A", File.ReadAllText(Path.Combine(dir, "a.txt")));
                Assert.Equal("B", File.ReadAllText(Path.Combine(dir, "sub", "b.txt")));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Verify_reports_missing_on_empty_dir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2ver_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try { Assert.NotEmpty(BepInExInstaller.Verify(dir)); }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Install_then_verify_is_clean()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2ins_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                using (var bep = Zip(("winhttp.dll", "x"), ("doorstop_config.ini", "x"), ("BepInEx/core/BepInEx.dll", "x")))
                using (var fw = Zip(("BepInEx/plugins/GK2.Framework.dll", "x")))
                    BepInExInstaller.Install(dir, bep, fw, false, null);
                Assert.Empty(BepInExInstaller.Verify(dir));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Uninstall_removes_only_bepinex_files()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2un_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "BepInEx", "core"));
            File.WriteAllText(Path.Combine(dir, "BepInEx", "core", "BepInEx.dll"), "x");
            File.WriteAllText(Path.Combine(dir, "winhttp.dll"), "x");
            File.WriteAllText(Path.Combine(dir, "doorstop_config.ini"), "x");
            File.WriteAllText(Path.Combine(dir, "GameFile.txt"), "keep");
            try
            {
                BepInExInstaller.Uninstall(dir, null);
                Assert.False(Directory.Exists(Path.Combine(dir, "BepInEx")));
                Assert.False(File.Exists(Path.Combine(dir, "winhttp.dll")));
                Assert.False(File.Exists(Path.Combine(dir, "doorstop_config.ini")));
                Assert.True(File.Exists(Path.Combine(dir, "GameFile.txt")));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
```

- [ ] **Step 2: Убедиться, что падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: FAIL — типы не существуют.

- [ ] **Step 3: Реализовать**

`src/GK2ModInstaller.Core/ZipExtractor.cs`:
```csharp
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
```

`src/GK2ModInstaller.Core/BepInExInstaller.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GK2ModInstaller.Core
{
    public static class BepInExInstaller
    {
        public const string BepInExDirName = "BepInEx";
        public static readonly string[] BepInExRootFiles =
        {
            "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt"
        };

        public static bool IsGameRunning()
        {
            try { return Process.GetProcessesByName("GraveyardKeeper2").Length > 0; }
            catch { return false; }
        }

        public static IReadOnlyList<string> Verify(string gameDir)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            {
                problems.Add("Папка игры не найдена.");
                return problems;
            }
            if (!File.Exists(Path.Combine(gameDir, "winhttp.dll"))) problems.Add("нет winhttp.dll");
            if (!File.Exists(Path.Combine(gameDir, "doorstop_config.ini"))) problems.Add("нет doorstop_config.ini");
            var core = Path.Combine(gameDir, BepInExDirName, "core");
            if (!Directory.Exists(core) || Directory.GetFiles(core).Length == 0) problems.Add("пусто BepInEx/core");
            if (!File.Exists(Path.Combine(gameDir, BepInExDirName, "plugins", "GK2.Framework.dll")))
                problems.Add("нет GK2.Framework.dll");
            return problems;
        }

        public static void Install(string gameDir, Stream bepinexZip, Stream frameworkZip, bool backupExisting, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
                throw new DirectoryNotFoundException(gameDir);

            if (backupExisting)
            {
                var bep = Path.Combine(gameDir, BepInExDirName);
                if (Directory.Exists(bep))
                {
                    var bak = Path.Combine(gameDir, BepInExDirName + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    log?.Invoke("Бэкап BepInEx -> " + Path.GetFileName(bak));
                    CopyDir(bep, bak);
                }
            }

            if (bepinexZip != null)
            {
                log?.Invoke("Распаковка BepInEx…");
                ZipExtractor.ExtractTo(bepinexZip, gameDir);
            }
            if (frameworkZip != null)
            {
                log?.Invoke("Распаковка GK2 Mod Framework…");
                ZipExtractor.ExtractTo(frameworkZip, gameDir);
            }
        }

        public static void Uninstall(string gameDir, Action<string> log)
        {
            var bep = Path.Combine(gameDir, BepInExDirName);
            if (Directory.Exists(bep)) { Directory.Delete(bep, true); log?.Invoke("Удалено: BepInEx\\"); }
            foreach (var f in BepInExRootFiles)
            {
                var p = Path.Combine(gameDir, f);
                if (File.Exists(p)) { File.Delete(p); log?.Invoke("Удалено: " + f); }
            }
        }

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, dir.Substring(src.Length).TrimStart('\\')));
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(dst, file.Substring(src.Length).TrimStart('\\')), true);
        }
    }
}
```

- [ ] **Step 4: Тесты проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat(core): zip extractor and bepinex installer"
```

---

### Task 4: Вшить ресурсы (BepInEx + фреймворк)

**Files:**
- Create: `src/GK2ModInstaller.App/Resources/BepInEx_win_x64_5.4.23.5.zip`, `src/GK2ModInstaller.App/Resources/GK2.Framework.zip`, `src/GK2ModInstaller.App/Resources/LICENSE.BepInEx.txt`, `src/GK2ModInstaller.App/Resources/LICENSE.GK2Framework.txt`
- Modify: `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj`

**Interfaces:**
- Produces: вшитые ресурсы с логическими именами `BepInEx_win_x64_5.4.23.5.zip` и `GK2.Framework.zip`; хелпер `MainForm.OpenResource(string logicalName) -> Stream` (используется в Task 5).

- [ ] **Step 1: Скачать BepInEx**

```powershell
$res = "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\src\GK2ModInstaller.App\Resources"
New-Item -ItemType Directory -Force -Path $res | Out-Null
Invoke-WebRequest -Uri "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip" -OutFile "$res\BepInEx_win_x64_5.4.23.5.zip"
(Get-Item "$res\BepInEx_win_x64_5.4.23.5.zip").Length   # ожидаемо ~639118
```

- [ ] **Step 2: Положить архив фреймворка**

Готовый архив фреймворка (внутри `BepInEx/plugins/GK2.Framework.dll` + `.../GK2.Framework/Localization/...`, `LICENSE`, `README.md`) — положить как `Resources\GK2.Framework.zip`. Источник (любой из):
- скачать релиз с Nexus (`https://www.nexusmods.com/graveyardkeeper2/mods/42`), **или**
- собрать из исходников: клонировать `https://github.com/SuperMan4eg/GK2-Mod-Framework` и выполнить `build-release.ps1` (см. `release-manifest.txt`).

Проверка структуры:
```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
$a = [System.IO.Compression.ZipFile]::OpenRead("$res\GK2.Framework.zip")
$a.Entries | Where-Object { $_.FullName -like "*GK2.Framework.dll" } | Select-Object FullName
$a.Dispose()
```
Expected: строка `BepInEx/plugins/GK2.Framework.dll`.

- [ ] **Step 3: Лицензии и csproj**

`Resources/LICENSE.BepInEx.txt` — текст LGPL-2.0 из архива BepInEx (`changelog.txt`/репозиторий).
`Resources/LICENSE.GK2Framework.txt` — текст MIT из архива фреймворка (`LICENSE`).

В `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj` добавить в `<ItemGroup>`:
```xml
<EmbeddedResource Include="Resources\BepInEx_win_x64_5.4.23.5.zip" LogicalName="BepInEx_win_x64_5.4.23.5.zip" />
<EmbeddedResource Include="Resources\GK2.Framework.zip" LogicalName="GK2.Framework.zip" />
<None Include="Resources\LICENSE.BepInEx.txt" CopyToOutputDirectory="PreserveNewest" />
<None Include="Resources\LICENSE.GK2Framework.txt" CopyToOutputDirectory="PreserveNewest" />
```

- [ ] **Step 4: Проверить, что ресурсы вшиты**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release`
Expected: 0 ошибок; в `src\GK2ModInstaller.App\bin\GK2ModInstaller.exe` exe весит > 1 МБ.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "chore(app): embed BepInEx and GK2 framework archives"
```

---

### Task 5: WinForms UI (App)

**Files:**
- Modify: `src/GK2ModInstaller.App/Program.cs`
- Create: `src/GK2ModInstaller.App/MainForm.cs`

**Interfaces:**
- Consumes: `GameLocator`, `BepInExInstaller`, `ZipExtractor` (Core); вшитые ресурсы (Task 4).
- Produces: рабочий exe с одной формой.

- [ ] **Step 1: Program.cs**

```csharp
using System;
using System.Windows.Forms;

namespace GK2ModInstaller.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
```

- [ ] **Step 2: MainForm.cs**

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using GK2ModInstaller.Core;
using Microsoft.Win32;

namespace GK2ModInstaller.App
{
    public sealed class MainForm : Form
    {
        private readonly TextBox _gameDir = new TextBox { Width = 420, ReadOnly = true };
        private readonly Label _status = new Label { AutoSize = true };
        private readonly CheckBox _bepinex = new CheckBox { Text = "BepInEx 5.4.23.5", Checked = true, AutoSize = true };
        private readonly CheckBox _framework = new CheckBox { Text = "GK2 Mod Framework", Checked = true, AutoSize = true };
        private readonly CheckBox _backup = new CheckBox { Text = "Бэкап существующего BepInEx", AutoSize = true };
        private readonly TextBox _log = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Height = 160, Width = 520 };
        private readonly ProgressBar _progress = new ProgressBar { Style = ProgressBarStyle.Marquee, Visible = false, Width = 520 };

        public MainForm()
        {
            Text = "Graveyard Keeper 2 — установка модов";
            Width = 560; Height = 420; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

            var browse = new Button { Text = "Обзор…", Width = 80 };
            browse.Click += (s, e) => Browse();

            var install = new Button { Text = "Установить", Width = 140, Height = 32 };
            install.Click += (s, e) => DoInstall();

            var uninstall = new Button { Text = "Удалить BepInEx", Width = 140, Height = 32 };
            uninstall.Click += (s, e) => DoUninstall();

            var l1 = new Label { Text = "Папка игры:", AutoSize = true };
            var row = new FlowLayoutPanel { AutoSize = true };
            row.Controls.AddRange(new Control[] { _gameDir, browse, _status });
            var checks = new FlowLayoutPanel { AutoSize = true };
            checks.Controls.AddRange(new Control[] { _bepinex, _framework, _backup });
            var buttons = new FlowLayoutPanel { AutoSize = true };
            buttons.Controls.AddRange(new Control[] { install, uninstall });

            var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), AutoScroll = true };
            stack.Controls.AddRange(new Control[] { l1, row, checks, buttons, _progress, _log });
            Controls.Add(stack);

            AutoDetect();
        }

        private void AutoDetect()
        {
            try
            {
                string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                string found = GameLocator.FindGameInSteamRoot(steam);
                if (found != null) { _gameDir.Text = found; SetStatus(true); }
                else SetStatus(false);
            }
            catch { SetStatus(false); }
        }

        private void Browse()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _gameDir.Text = dlg.SelectedPath;
                    SetStatus(GameLocator.ValidateGameDir(dlg.SelectedPath));
                }
            }
        }

        private void SetStatus(bool ok)
        {
            _status.Text = ok ? "✓ папка игры найдена" : "✗ укажите папку игры";
            _status.ForeColor = ok ? System.Drawing.Color.Green : System.Drawing.Color.Firebrick;
        }

        private void AppendLog(string s)
        {
            _log.AppendText(s + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private Stream OpenResource(string logicalName)
            => Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);

        private void DoInstall()
        {
            string dir = _gameDir.Text;
            if (!GameLocator.ValidateGameDir(dir)) { MessageBox.Show(this, "Неверная папка игры."); return; }
            if (BepInExInstaller.IsGameRunning()) { MessageBox.Show(this, "Закройте игру перед установкой."); return; }

            try
            {
                _progress.Visible = true; AppendLog("Установка…");
                using (var bep = _bepinex.Checked ? OpenResource("BepInEx_win_x64_5.4.23.5.zip") : null)
                using (var fw = _framework.Checked ? OpenResource("GK2.Framework.zip") : null)
                    BepInExInstaller.Install(dir, bep, fw, _backup.Checked, AppendLog);
                var problems = BepInExInstaller.Verify(dir);
                AppendLog(problems.Count == 0 ? "Готово. Запустите игру 1 раз — появится меню Mods." : "Проблемы: " + string.Join(", ", problems));
            }
            catch (Exception ex) { AppendLog("Ошибка: " + ex.Message); }
            finally { _progress.Visible = false; }
        }

        private void DoUninstall()
        {
            if (MessageBox.Show(this, "Удалить BepInEx и фреймворк?", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { BepInExInstaller.Uninstall(_gameDir.Text, AppendLog); AppendLog("Удалено."); }
            catch (Exception ex) { AppendLog("Ошибка: " + ex.Message); }
        }
    }
}
```

- [ ] **Step 3: Собрать**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release`
Expected: 0 ошибок.

- [ ] **Step 4: Запустить и проверить UI вручную**

Запустить `src\GK2ModInstaller.App\bin\Release\GK2ModInstaller.exe`. Проверить: папка игры определилась (✓), галочки на месте, кнопки работают. Окно закрыть.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat(app): winforms installer UI"
```

---

### Task 6: E2E на реальной игре + упаковка

**Files:**
- Modify: `README.md` (создать), `.gitignore` (проверить)
- Create: релизный архив на выходе.

- [ ] **Step 1: Установить в реальную игру**

Запустить exe; папка выставится `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2`; нажать «Установить».
Expected в логе: `Готово…` без «Проблемы».

```powershell
$g = "E:\SteamLibrary\steamapps\common\Graveyard Keeper 2"
Test-Path "$g\winhttp.dll"; Test-Path "$g\doorstop_config.ini"; Test-Path "$g\BepInEx\plugins\GK2.Framework.dll"
```

- [ ] **Step 2: Проверить в игре**

Запустить GK2 через Steam, зайти в главное меню. Expected: кнопка **Mods**. Проверить лог BepInEx:
```powershell
$log = "E:\SteamLibrary\steamapps\common\Graveyard Keeper 2\BepInEx\LogOutput.log"
Select-String -Path $log -Pattern "GK2_FRAMEWORK_READY","BepInEx"
```
Expected: есть `GK2_FRAMEWORK_READY`.

- [ ] **Step 3: README.md**

Создать `README.md`: что делает, требования, установка/удаление, скриншот (опц.), предупреждение антивируса про `winhttp.dll`, ссылки (GitHub/Nexus), лицензии (BepInEx LGPL-2.0, Framework MIT). На русском + английском.

- [ ] **Step 4: Релизный архив**

```powershell
$out = "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\dist"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Copy-Item "src\GK2ModInstaller.App\bin\Release\GK2ModInstaller.exe" $out
Copy-Item "README.md" $out
Compress-Archive -Path "$out\GK2ModInstaller.exe","$out\README.md" -DestinationPath "$out\GK2ModInstaller_v1.0.0.zip" -Force
```
Expected: `dist\GK2ModInstaller_v1.0.0.zip` создан.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "docs: readme and release packaging"
```

---

## Self-Review (проверка плана)

**Spec coverage:** §3 объём → Task 5/6; §4 технологии → Task 1; §5 UX → Task 5; §6 модули (`GameLocator`→Task 2, `BepInExInstaller`→Task 3, `InstallerManifest`→ учтён как константы версий в `BepInExInstaller`/UI, отдельный файл не нужен — YAGNI); §7 ресурсы/лицензии → Task 4; §8 тесты → Task 2/3/6; §9 распространение → Task 6; §10 риски (закрытая игра → Task 5 `IsGameRunning`; права/антивирус → Task 6 README).
**Placeholders:** нет (везде код/точные пути).
**Type consistency:** `ExtractTo(Stream,string)`, `Install(string,Stream,Stream,bool,Action<string>)`, `Verify(string)->IReadOnlyList<string>`, `FindGameInSteamRoot(string)->string` — едины во всех тасках.

## Фаза 2 (не этот план)

Автозагрузчик Workshop-модов (BepInEx-плагин) — отдельная спека/план.
