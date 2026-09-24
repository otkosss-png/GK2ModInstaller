# GK2 Workshop Auto-Loader — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** BepInEx-preloader-патчер, который автоматически подхватывает код-моды из подписанных Workshop-айтемов GK2 (копирует в `BepInEx/plugins/_Workshop/<id>/`), встроен в инсталлятор.

**Architecture:** Логика синка — `WorkshopSync` в `Core` (чистый FS, тестируется). Тонкий патчер — новый проект `GK2ModInstaller.Patcher` (netstandard2.0, ссылки BepInEx + Mono.Cecil). Инсталлятор вшивает патчер-DLL и кладёт в `BepInEx/patchers/`.

**Tech Stack:** C#, .NET Standard 2.0 (Core + Patcher), .NET Framework 4.8 (App), xUnit, BepInEx 5.4.23.5 preloader patcher API, `Mono.Cecil`.

**Spec:** `docs/superpowers/specs/2026-09-24-gk2-workshop-autoloader-design.md`

## Global Constraints

- Core = **netstandard2.0** (без внешних NuGet); Patcher = **netstandard2.0**; App = **net48**; Tests = **net8.0**.
- Сборка: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release` (workdir проекта), тесты — `... test -c Release`.
- Окружение: игра `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2`; BepInEx уже установлен (фаза 1). BepInEx core: `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2\BepInEx\core\{BepInEx.dll, Mono.Cecil.dll}`.
- Workshop-root: `...\steamapps\workshop\content\4358690`; мод = папка с `BepInEx\plugins\*.dll` (+ `BepInEx\config\*.cfg`).
- Стадия плагинов: `BepInEx\plugins\_Workshop\<id>\`; конфиги копируются в `BepInEx\config\` только если файла нет.
- Патчер-DLL в `BepInEx\patchers\GK2.WorkshopAutoLoader.dll`.
- Коммиты после каждого таска (git уже инициализирован; origin = GitHub).

---

## File Structure

- `src/GK2ModInstaller.Core/WorkshopSync.cs` — синк (новый)
- `src/GK2ModInstaller.Patcher/GK2ModInstaller.Patcher.csproj` + `WorkshopAutoLoaderPatcher.cs` (новый проект)
- `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj` — вшивание патчера
- `src/GK2ModInstaller.App/MainForm.cs` — чек-бокс
- `src/GK2ModInstaller.App/Cli.cs` — установка патчера
- `src/GK2ModInstaller.Core/BepInExInstaller.cs` — Install/Verify/Uninstall учитывают патчер
- `tests/GK2ModInstaller.Tests/WorkshopSyncTests.cs` — тесты

---

### Task 1: WorkshopSync (Core, TDD)

**Files:**
- Create: `src/GK2ModInstaller.Core/WorkshopSync.cs`
- Test: `tests/GK2ModInstaller.Tests/WorkshopSyncTests.cs`

**Interfaces:**
- Produces: `WorkshopSync.RunOnce(string workshopRoot, string bepInExRoot, Action<string> log)`,
  `WorkshopSync.Sync(string workshopRoot, string bepInExRoot, Action<string> log)`,
  `WorkshopSync.WorkshopPluginsDirName`.

- [ ] **Step 1: Написать падающие тесты**

`tests/GK2ModInstaller.Tests/WorkshopSyncTests.cs`:
```csharp
using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class WorkshopSyncTests
    {
        private static string Tmp() => Path.Combine(Path.GetTempPath(), "gk2sync_" + Guid.NewGuid().ToString("N"));

        private static void MakeItem(string workshopRoot, string id, string dllContent, string cfgName)
        {
            string p = Path.Combine(workshopRoot, id, "BepInEx", "plugins");
            Directory.CreateDirectory(p);
            File.WriteAllText(Path.Combine(p, "Mod.dll"), dllContent);
            if (cfgName != null)
            {
                string c = Path.Combine(workshopRoot, id, "BepInEx", "config");
                Directory.CreateDirectory(c);
                File.WriteAllText(Path.Combine(c, cfgName), "cfg");
            }
        }

        [Fact]
        public void Copies_plugins_into_workshop_staging()
        {
            string root = Tmp();
            string bep = Path.Combine(root, "BE");
            string ws = Path.Combine(root, "ws");
            Directory.CreateDirectory(bep); Directory.CreateDirectory(ws);
            MakeItem(ws, "111", "MOD111", "mod.cfg");
            try
            {
                WorkshopSync.Sync(ws, bep, null);
                Assert.True(File.Exists(Path.Combine(bep, "plugins", "_Workshop", "111", "Mod.dll")));
                Assert.True(File.Exists(Path.Combine(bep, "config", "mod.cfg")));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Does_not_overwrite_existing_config()
        {
            string root = Tmp();
            string bep = Path.Combine(root, "BE");
            string ws = Path.Combine(root, "ws");
            Directory.CreateDirectory(Path.Combine(bep, "config"));
            File.WriteAllText(Path.Combine(bep, "config", "mod.cfg"), "USER");
            Directory.CreateDirectory(ws);
            MakeItem(ws, "222", "x", "mod.cfg");
            try
            {
                WorkshopSync.Sync(ws, bep, null);
                Assert.Equal("USER", File.ReadAllText(Path.Combine(bep, "config", "mod.cfg")));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Removes_staging_for_unsubscribed_items()
        {
            string root = Tmp();
            string bep = Path.Combine(root, "BE");
            string ws = Path.Combine(root, "ws");
            Directory.CreateDirectory(Path.Combine(bep, "plugins", "_Workshop", "999"));
            File.WriteAllText(Path.Combine(bep, "plugins", "_Workshop", "999", "Old.dll"), "x");
            Directory.CreateDirectory(ws);
            try
            {
                WorkshopSync.Sync(ws, bep, null);
                Assert.False(Directory.Exists(Path.Combine(bep, "plugins", "_Workshop", "999")));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: FAIL — `WorkshopSync` не существует.

- [ ] **Step 3: Реализовать**

`src/GK2ModInstaller.Core/WorkshopSync.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace GK2ModInstaller.Core
{
    public static class WorkshopSync
    {
        public const string WorkshopPluginsDirName = "_Workshop";
        private static bool _ran;

        public static void RunOnce(string workshopRoot, string bepInExRoot, Action<string> log)
        {
            if (_ran) return;
            _ran = true;
            Sync(workshopRoot, bepInExRoot, log);
        }

        public static void Sync(string workshopRoot, string bepInExRoot, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(workshopRoot) || !Directory.Exists(workshopRoot))
            { log?.Invoke("Workshop root не найден: " + workshopRoot); return; }
            if (string.IsNullOrWhiteSpace(bepInExRoot) || !Directory.Exists(bepInExRoot))
            { log?.Invoke("BepInEx root не найден: " + bepInExRoot); return; }

            var pluginsDir = Path.Combine(bepInExRoot, "plugins");
            var configDir = Path.Combine(bepInExRoot, "config");
            var stagingRoot = Path.Combine(pluginsDir, WorkshopPluginsDirName);
            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(configDir);

            var present = new HashSet<string>();
            int items = 0, copied = 0;
            foreach (var itemDir in Directory.GetDirectories(workshopRoot))
            {
                var id = Path.GetFileName(itemDir);
                var srcPlugins = Path.Combine(itemDir, "BepInEx", "plugins");
                if (!Directory.Exists(srcPlugins)) continue;
                if (Directory.GetFiles(srcPlugins, "*.dll", SearchOption.AllDirectories).Length == 0) continue;

                present.Add(id);
                items++;
                copied += CopyDir(srcPlugins, Path.Combine(stagingRoot, id));

                var srcConfig = Path.Combine(itemDir, "BepInEx", "config");
                if (Directory.Exists(srcConfig))
                    foreach (var cfg in Directory.GetFiles(srcConfig, "*.cfg", SearchOption.AllDirectories))
                    {
                        var target = Path.Combine(configDir, Path.GetFileName(cfg));
                        if (!File.Exists(target)) { File.Copy(cfg, target); log?.Invoke("config: " + Path.GetFileName(cfg)); }
                    }
            }

            foreach (var staged in Directory.GetDirectories(stagingRoot))
            {
                var id = Path.GetFileName(staged);
                if (!present.Contains(id)) { Directory.Delete(staged, true); log?.Invoke("Удалён отписанный мод: " + id); }
            }

            log?.Invoke($"Автозагрузка Workshop: айтемов {items}, файлов {copied}.");
        }

        private static int CopyDir(string src, string dst)
        {
            int n = 0;
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, dir.Substring(src.Length).TrimStart('\\')));
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(dst, file.Substring(src.Length).TrimStart('\\')), true);
                n++;
            }
            return n;
        }
    }
}
```

- [ ] **Step 4: Тесты проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: PASS (13 тестов).

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat(core): workshop sync"
```

---

### Task 2: Проект патчера

**Files:**
- Create: `src/GK2ModInstaller.Patcher/GK2ModInstaller.Patcher.csproj`, `src/GK2ModInstaller.Patcher/WorkshopAutoLoaderPatcher.cs`
- Modify: `GK2ModInstaller.sln`

**Interfaces:**
- Consumes: `WorkshopSync.RunOnce` (Core).
- Produces: `GK2ModInstaller.Patcher.dll` в `src\GK2ModInstaller.Patcher\bin\Release\netstandard2.0\`.

- [ ] **Step 1: csproj**

`src/GK2ModInstaller.Patcher/GK2ModInstaller.Patcher.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>GK2.WorkshopAutoLoader</AssemblyName>
    <RootNamespace>GK2ModInstaller.Patcher</RootNamespace>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <GameDir Condition="'$(GameDir)' == ''">E:\SteamLibrary\steamapps\common\Graveyard Keeper 2</GameDir>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\GK2ModInstaller.Core\GK2ModInstaller.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="BepInEx"><HintPath>$(GameDir)\BepInEx\core\BepInEx.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Mono.Cecil"><HintPath>$(GameDir)\BepInEx\core\Mono.Cecil.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Патчер**

`src/GK2ModInstaller.Patcher/WorkshopAutoLoaderPatcher.cs`:
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
            var log = Logger.CreateLogSource("GK2.WorkshopAutoLoader");
            string bep = Paths.BepInExRootPath;
            string gameDir = Directory.GetParent(bep)?.FullName;
            string workshop = FindWorkshopRoot(gameDir);
            log.LogInfo("Workshop root: " + (workshop ?? "<не найден>"));
            WorkshopSync.RunOnce(workshop, bep, log.LogInfo);
        }

        private static string FindWorkshopRoot(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir)) return null;
            var dir = new DirectoryInfo(gameDir);
            while (dir != null && !dir.Name.Equals("steamapps", System.StringComparison.OrdinalIgnoreCase))
                dir = dir.Parent;
            return dir == null ? null : Path.Combine(dir.FullName, "workshop", "content", WorkshopId);
        }
    }
}
```

- [ ] **Step 3: Добавить в sln и собрать**

```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln GK2ModInstaller.sln add src\GK2ModInstaller.Patcher\GK2ModInstaller.Patcher.csproj
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release
```
Expected: 0 ошибок; `src\GK2ModInstaller.Patcher\bin\Release\GK2.WorkshopAutoLoader.dll` существует.
(Если netstandard2.0 не резолвит `BepInEx.dll` — переключить `<TargetFramework>` на `net48`.)

- [ ] **Step 4: Commit**

```powershell
git add -A; git commit -m "feat(patcher): workshop auto-loader patcher"
```

---

### Task 3: Интеграция в инсталлятор

**Files:**
- Modify: `src/GK2ModInstaller.Core/BepInExInstaller.cs`, `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj`, `src/GK2ModInstaller.App/MainForm.cs`, `src/GK2ModInstaller.App/Cli.cs`, `README.md`

**Interfaces:**
- Consumes: `GK2.WorkshopAutoLoader.dll` (embedded resource `GK2.WorkshopAutoLoader.dll`).
- Produces: `BepInExInstaller.Install(..., Stream patcherDll, ...)`, `Verify` учитывает `BepInEx\patchers\GK2.WorkshopAutoLoader.dll`, `Uninstall` удаляет патчер.

- [ ] **Step 1: BepInExInstaller — патчер**

В `src/GK2ModInstaller.Core/BepInExInstaller.cs`:
- сигнатуру `Install` изменить на
  `public static void Install(string gameDir, Stream bepinexZip, Stream frameworkZip, Stream patcherDll, bool backupExisting, Action<string> log)`
  и добавить после распаковки фреймворка:
```csharp
            if (patcherDll != null)
            {
                var patchersDir = Path.Combine(gameDir, BepInExDirName, "patchers");
                Directory.CreateDirectory(patchersDir);
                var dst = Path.Combine(patchersDir, "GK2.WorkshopAutoLoader.dll");
                log?.Invoke("Патчер автозагрузки -> " + dst);
                using (var fs = File.Create(dst)) patcherDll.CopyTo(fs);
            }
```
- в `Verify` добавить проверку (только если патчер установлен — сделать необязательной: не считать проблемой, если нет):
```csharp
            // патчер автозагрузки (необязательный)
```
Hmm — `Verify` не знает, ожидался ли патчер. Добавить параметр `bool expectPatcher = false`:
```csharp
        public static IReadOnlyList<string> Verify(string gameDir, bool expectPatcher = false)
        {
            ... существующие проверки ...
            if (expectPatcher && !File.Exists(Path.Combine(gameDir, BepInExDirName, "patchers", "GK2.WorkshopAutoLoader.dll")))
                problems.Add("нет GK2.WorkshopAutoLoader.dll");
            return problems;
        }
```
- в `Uninstall` добавить удаление патчера:
```csharp
            var patcher = Path.Combine(gameDir, BepInExDirName, "patchers", "GK2.WorkshopAutoLoader.dll");
            if (File.Exists(patcher)) { File.Delete(patcher); log?.Invoke("Удалено: patchers\\GK2.WorkshopAutoLoader.dll"); }
```

- [ ] **Step 2: Вшить патчер в exe**

В `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj` добавить перед `</Project>`:
```xml
  <Target Name="BuildPatcherForEmbedding" BeforeTargets="BeforeCompile">
    <MSBuild Projects="..\GK2ModInstaller.Patcher\GK2ModInstaller.Patcher.csproj" Targets="Build" Properties="Configuration=$(Configuration)" />
    <ItemGroup>
      <EmbeddedResource Include="..\GK2ModInstaller.Patcher\bin\$(Configuration)\GK2.WorkshopAutoLoader.dll" LogicalName="GK2.WorkshopAutoLoader.dll" />
    </ItemGroup>
  </Target>
```
Hmm — если встроить динамически не выйдет, fallback: App читает `GK2.WorkshopAutoLoader.dll` из своей папки (копирует сборка через target), и пакет включает 2 файла.

Hmm — (в плане допускается fallback; основной путь — embedded.)

- [ ] **Step 3: UI + CLI**

`MainForm.cs`: добавить чек-бокс
```csharp
        private readonly CheckBox _autoLoader = new CheckBox { Text = "Автозагрузка Workshop-модов", Checked = true, AutoSize = true };
```
добавить его в `checks.Controls`, и в `DoInstall` передать патчер:
```csharp
                using (var bep = _bepinex.Checked ? OpenResource("BepInEx_win_x64_5.4.23.5.zip") : null)
                using (var fw = _framework.Checked ? OpenResource("GK2.Framework.zip") : null)
                using (var patcher = _autoLoader.Checked ? OpenResource("GK2.WorkshopAutoLoader.dll") : null)
                    BepInExInstaller.Install(dir, bep, fw, patcher, _backup.Checked, AppendLog);
                var problems = BepInExInstaller.Verify(dir, _autoLoader.Checked);
```
`Cli.cs`: аналогично — `Open("GK2.WorkshopAutoLoader.dll")` и `Verify(gameDir, true)`.

- [ ] **Step 4: Собрать и проверить вшивание**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release`
Expected: 0 ошибок; exe вырос (патчер внутри).
Проверка ресурса:
```powershell
$asm = [Reflection.Assembly]::LoadFile("src\GK2ModInstaller.App\bin\Release\GK2ModInstaller.exe")
$asm.GetManifestResourceNames()
```
Expected: среди имён есть `GK2.WorkshopAutoLoader.dll`.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat(app): integrate workshop auto-loader patcher"
```

---

### Task 4: E2E + релиз v1.1.0

**Files:** `README.md` (обновить), релиз.

- [ ] **Step 1: E2E-установка**

Проверить, что подписан код-мод (после фазы 1 в кэше есть `...\workshop\content\4358690\3807023815`). Затем:
```powershell
$exe = "src\GK2ModInstaller.App\bin\Release\GK2ModInstaller.exe"
$dir = "E:\SteamLibrary\steamapps\common\Graveyard Keeper 2"
Remove-Item "$env:TEMP\gk2installer_cli.log" -ErrorAction SilentlyContinue
Start-Process -FilePath $exe -ArgumentList "--install `"$dir`"" -Wait
Get-Content "$env:TEMP\gk2installer_cli.log"
Test-Path "$dir\BepInEx\patchers\GK2.WorkshopAutoLoader.dll"
```

- [ ] **Step 2: Запустить игру и проверить автозагрузку**

```powershell
$dir = "E:\SteamLibrary\steamapps\common\Graveyard Keeper 2"
Remove-Item "$dir\BepInEx\LogOutput.log" -ErrorAction SilentlyContinue
$p = Start-Process -FilePath "$dir\GraveyardKeeper2.exe" -WorkingDirectory $dir -PassThru
for ($i=0; $i -lt 50; $i++) { Start-Sleep 2; if ((Test-Path "$dir\BepInEx\LogOutput.log") -and ((Get-Content "$dir\BepInEx\LogOutput.log" -Raw) -match "BetterAutoCrafting|Автозагрузка Workshop")) { break }; if ($p.HasExited) { break } }
Select-String -Path "$dir\BepInEx\LogOutput.log" -Pattern "GK2.WorkshopAutoLoader","Workshop root","Автозагрузка Workshop","BetterAutoCrafting" | ForEach-Object { $_.Line }
Get-Process GraveyardKeeper2 -ErrorAction SilentlyContinue | Stop-Process -Force
```
Expected: в логе есть строка автозагрузки; `BepInEx\plugins\_Workshop\3807023815\BetterAutoCrafting.dll` существует.

- [ ] **Step 3: README**

Обновить `README.md`: добавить пункт про автозагрузку Workshop-модов (ставится вместе с BepInEx; чек-бокс; как отключить).

- [ ] **Step 4: Релиз v1.1.0**

```powershell
$dist = "dist"; New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item "src\GK2ModInstaller.App\bin\Release\GK2ModInstaller.exe" $dist -Force
Copy-Item "README.md" $dist -Force
Compress-Archive -Path "$dist\GK2ModInstaller.exe","$dist\README.md" -DestinationPath "$dist\GK2ModInstaller_v1.1.0.zip" -Force
git add -A; git commit -m "docs: readme autoloader; release v1.1.0"
& "C:\Program Files\GitHub CLI\gh.exe" release create v1.1.0 "$dist\GK2ModInstaller_v1.1.0.zip" --title "GK2 Mod Installer v1.1.0" --notes "Adds automatic loading of code mods from subscribed Steam Workshop items (preloader patcher)."
```

## Self-Review (плана)

**Spec coverage:** §4 технологии → Task 1/2; §5.1 патчер → Task 2; §5.2 синк → Task 1; §6 интеграция (чек-бокс, вшивание, Verify/Uninstall, CLI/README) → Task 3; §7 тесты → Task 1/4; §8 распространение → Task 4; §9 риски (TargetDLLs/Assembly-CSharp) → Task 4 E2E.
**Placeholders:** нет (код/пути явные; в Task 3 Step 2 допускается задокументированный fallback).
**Type consistency:** `WorkshopSync.RunOnce(string,string,Action<string>)`, `Install(string,Stream,Stream,Stream,bool,Action<string>)`, `Verify(string,bool)`.
