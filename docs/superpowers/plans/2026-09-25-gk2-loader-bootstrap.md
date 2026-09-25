# GK2 Loader Bootstrap — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Сделать обновление загрузчика GK2 Workshop Auto-Loader автоматическим: в `BepInEx\patchers` остаётся крошечная замороженная заглушка, а живая логика лежит в нашем Workshop-айтеме (`Loader\GK2.WorkshopLoader.dll`) и подхватывается при каждом старте.

**Architecture:** Заглушка (`GK2ModInstaller.Patcher`, ~80 строк) резолвит пути, выбирает источник загрузчика (айтем → локальный фолбэк, логика выбора тестируется в Core), готовит резолв Mono.Cecil из `BepInEx\core` и загружает загрузчик **из байтов** (`Assembly.Load(File.ReadAllBytes(path))` — файл не блокируется, Steam может обновлять), затем вызывает замороженный контракт `GK2ModInstaller.Loader.Entry.Run(bepInExRoot, gameRoot, workshopRoot, acfPath, source)`. Сборка загрузчика — новый проект `src/GK2ModInstaller.Loader` (netstandard2.0), в неё переезжает всё, что сейчас делает патчер.

**Tech Stack:** C# 9, .NET Standard 2.0 (Core/Patcher/Loader), .NET Framework 4.8 (App), net8.0 + xUnit (тесты), BepInEx 5.4.23.5 (netstandard2.0 core), Mono.Cecil.

**Spec:** `docs/superpowers/specs/2026-09-25-gk2-loader-bootstrap-design.md`

## Global Constraints

- `Core` — netstandard2.0 / `LangVersion 9.0` / **без ссылки на BepInEx**; `Loader` и `Patcher` — netstandard2.0, ссылки `BepInEx.dll` и `Mono.Cecil.dll` из `$(GameDir)\BepInEx\core` c `<Private>false</Private>`; `GameDir` по умолчанию `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2`.
- `Loader` и `Patcher` компилируют исходники Core линками `<Compile Include="..\GK2ModInstaller.Core\<File>.cs" Link="<File>.cs" />` (самодостаточные DLL, без Core.dll).
- **Контракт `GK2ModInstaller.Loader.Entry.Run`** — `public static void Run(string bepInExRoot, string gameRoot, string workshopRoot, string acfPath, string source)`; менять только вместе с заглушкой; защищён тестом.
- Имя файла заглушки — **прежнее**: `BepInEx\patchers\GK2.WorkshopAutoLoader.dll` (существующим достаточно перезаписать). Имя сборки загрузчика — `GK2.WorkshopLoader.dll`.
- Форматы `GK2_WorkshopLoader.trust.txt`, `pending.txt`, `backup\<id>\` и конфига `GK2_WorkshopLoader.txt` (`language=auto|en|ru`) **не меняются**.
- Тесты — `tests\GK2ModInstaller.Tests` (net8.0, xUnit), временные папки вне репозитория; никогда не трогать `steamapps\workshop` и реальную папку игры.
- Сборка/тесты: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release` и `... test -c Release`, workdir = корень репозитория `C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller`.
- Пользовательские тексты — через `LoaderText` (EN/RU); файлы UTF-8 без BOM.
- Не менять логику согласия (кроме переноса), не трогать `GK2BuildCounter`.

## File Structure

**Создать**
- `src/GK2ModInstaller.Core/LoaderSourceSelector.cs` — выбор источника загрузчика (чистая логика, тестируется).
- `src/GK2ModInstaller.Loader/GK2ModInstaller.Loader.csproj` — сборка `GK2.WorkshopLoader`, `Version 1.3.0`, линки Core.
- `src/GK2ModInstaller.Loader/Entry.cs` — замороженный вход.
- `src/GK2ModInstaller.Loader/Win32Dialog.cs` — переезжает из патчера.
- `src/GK2ModInstaller.Patcher/Bootstrap.cs` — логика заглушки.
- `tests/GK2ModInstaller.Tests/LoaderSourceSelectorTests.cs`, `tests/GK2ModInstaller.Tests/EntryContractTests.cs`.

**Изменить**
- `src/GK2ModInstaller.Patcher/WorkshopAutoLoaderPatcher.cs` — тонкий патчер (вызов `Bootstrap.Run()`).
- `src/GK2ModInstaller.Patcher/GK2ModInstaller.Patcher.csproj` — линкуется только `LoaderSourceSelector.cs`.
- `src/GK2ModInstaller.Core/BepInExInstaller.cs` — `Install` принимает поток загрузчика, `Verify` проверяет оба файла, `Uninstall` удаляет оба.
- `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj` — таргет собирает и копирует обе DLL рядом с exe.
- `src/GK2ModInstaller.App/Cli.cs`, `src/GK2ModInstaller.App/MainForm.cs` — передают поток загрузчика.
- `tests/GK2ModInstaller.Tests/BepInExInstallerTests.cs` — расширить под оба файла.
- `README.md`, `dist/RELEASE_NOTES.md`, `AGENTS.md` — документация; описание и раскладка айтема 3807406994.

**Удалить**
- `src/GK2ModInstaller.Patcher/Win32Dialog.cs` (переехал в Loader).

---

### Task 1: LoaderSourceSelector (Core, чистая логика)

**Files:**
- Create: `src/GK2ModInstaller.Core/LoaderSourceSelector.cs`
- Test: `tests/GK2ModInstaller.Tests/LoaderSourceSelectorTests.cs`

**Interfaces:**
- Consumes: —
- Produces: `static class LoaderSourceSelector { const string ItemSource = "item"; const string LocalSource = "local"; static string Select(string itemLoaderPath, string localLoaderPath); static string KindOf(string chosenPath, string itemLoaderPath); static string Describe(string chosenPath, string itemLoaderPath, string localLoaderPath); }`

- [ ] **Step 1: Написать падающий тест**

```csharp
using System;
using System.IO;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class LoaderSourceSelectorTests
    {
        private static string TmpDll(string name)
        {
            var dir = Path.Combine(Path.GetTempPath(), "gk2sel_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, name);
            File.WriteAllText(f, "x");
            return f;
        }

        [Fact]
        public void Item_copy_wins_when_both_exist()
        {
            string item = TmpDll("item.dll");
            string local = TmpDll("local.dll");
            Assert.Equal(item, LoaderSourceSelector.Select(item, local));
            Assert.Equal("item", LoaderSourceSelector.KindOf(item, item));
        }

        [Fact]
        public void Local_fallback_is_used_when_item_is_missing()
        {
            string local = TmpDll("local.dll");
            string item = Path.Combine(Path.GetDirectoryName(local), "nope.dll");
            Assert.Equal(local, LoaderSourceSelector.Select(item, local));
            Assert.Equal("local", LoaderSourceSelector.KindOf(local, item));
        }

        [Fact]
        public void Null_when_nothing_exists()
        {
            string missing = Path.Combine(Path.GetTempPath(), "gk2none_" + Guid.NewGuid().ToString("N"), "x.dll");
            Assert.Null(LoaderSourceSelector.Select(missing, null));
            Assert.Null(LoaderSourceSelector.Select(null, null));
            Assert.Null(LoaderSourceSelector.Select("", ""));
        }

        [Fact]
        public void Describe_mentions_paths_and_hint_when_nothing_found()
        {
            string local = TmpDll("local.dll");
            string item = Path.Combine(Path.GetDirectoryName(local), "nope.dll");
            string ok = LoaderSourceSelector.Describe(local, item, local);
            Assert.Contains(local, ok);
            Assert.Contains("local", ok);

            string fail = LoaderSourceSelector.Describe(null, item, null);
            Assert.Contains("3807406994", fail);
            Assert.Contains("GK2 Mod Installer", fail);
        }
    }
}
```

- [ ] **Step 2: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~LoaderSourceSelectorTests"`
Expected: ошибка компиляции — нет `LoaderSourceSelector`.

- [ ] **Step 3: Реализовать**

```csharp
using System;
using System.IO;

namespace GK2ModInstaller.Core
{
    // Какую сборку загрузчика грузить: приоритет — копия в Workshop-айтеме (обновляется подпиской),
    // иначе локальная копия, которую положил инсталлятор.
    public static class LoaderSourceSelector
    {
        public const string ItemSource = "item";
        public const string LocalSource = "local";
        public const string LoaderItemId = "3807406994";

        public static string Select(string itemLoaderPath, string localLoaderPath)
        {
            if (!string.IsNullOrEmpty(itemLoaderPath) && File.Exists(itemLoaderPath)) return itemLoaderPath;
            if (!string.IsNullOrEmpty(localLoaderPath) && File.Exists(localLoaderPath)) return localLoaderPath;
            return null;
        }

        public static string KindOf(string chosenPath, string itemLoaderPath)
        {
            return !string.IsNullOrEmpty(itemLoaderPath) && string.Equals(chosenPath, itemLoaderPath, StringComparison.OrdinalIgnoreCase)
                ? ItemSource
                : LocalSource;
        }

        public static string Describe(string chosenPath, string itemLoaderPath, string localLoaderPath)
        {
            if (string.IsNullOrEmpty(chosenPath))
                return "загрузчик не найден: ни в айтеме (" + (itemLoaderPath ?? "нет") + "), ни локально (" +
                       (localLoaderPath ?? "нет") + ") — подпишитесь на айтем " + LoaderItemId + " или запустите GK2 Mod Installer";
            return "загрузчик: " + chosenPath + " (источник: " + KindOf(chosenPath, itemLoaderPath) + ")";
        }
    }
}
```

- [ ] **Step 4: Запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~LoaderSourceSelectorTests"`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 5: Коммит**

```bash
git add src/GK2ModInstaller.Core/LoaderSourceSelector.cs tests/GK2ModInstaller.Tests/LoaderSourceSelectorTests.cs
git commit -m "feat(core): loader source selector (item copy wins, local fallback)"
```

---

### Task 2: Новый проект загрузчика + Entry + тест контракта

> **Примечание (preflight):** задачи 2 и 3 выполняются **одним диспатчем** — после переноса `Win32Dialog.cs` проект патчера не собирается до его переписывания в заглушку (Task 3). Коммиты — по одному на задачу, но дерево должно собираться уже после Task 3.

**Files:**
- Create: `src/GK2ModInstaller.Loader/GK2ModInstaller.Loader.csproj`, `src/GK2ModInstaller.Loader/Entry.cs`
- Move: `src/GK2ModInstaller.Patcher/Win32Dialog.cs` → `src/GK2ModInstaller.Loader/Win32Dialog.cs` (namespace `GK2ModInstaller.Loader`)
- Modify: `tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj` (ссылка на BepInEx copy-local)
- Create: `tests/GK2ModInstaller.Tests/EntryContractTests.cs`
- Modify: `GK2ModInstaller.sln` (добавить проект)

**Interfaces:**
- Consumes: `LoaderSourceSelector` (Task 1), `WorkshopLoader`/`LoaderOptions`/`Win32Dialog` (существующие).
- Produces: сборка `GK2.WorkshopLoader.dll` (`AssemblyName GK2.WorkshopLoader`, `Version 1.3.0`) с типом `GK2ModInstaller.Loader.Entry` и методом `Run(string,string,string,string,string)`.

- [ ] **Step 1: Перенести Win32Dialog и создать проект**

```powershell
New-Item -ItemType Directory -Path "src\GK2ModInstaller.Loader" -Force | Out-Null
git mv src/GK2ModInstaller.Patcher/Win32Dialog.cs src/GK2ModInstaller.Loader/Win32Dialog.cs
```
В `src/GK2ModInstaller.Loader/Win32Dialog.cs` заменить `namespace GK2ModInstaller.Patcher` на `namespace GK2ModInstaller.Loader`, остальной код — без изменений.

`src/GK2ModInstaller.Loader/GK2ModInstaller.Loader.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>GK2.WorkshopLoader</AssemblyName>
    <RootNamespace>GK2ModInstaller.Loader</RootNamespace>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <Version>1.3.0</Version>
    <GameDir Condition="'$(GameDir)' == ''">E:\SteamLibrary\steamapps\common\Graveyard Keeper 2</GameDir>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\GK2ModInstaller.Core\WorkshopSync.cs" Link="Core\WorkshopSync.cs" />
    <Compile Include="..\GK2ModInstaller.Core\WorkshopLoader.cs" Link="Core\WorkshopLoader.cs" />
    <Compile Include="..\GK2ModInstaller.Core\WorkshopItems.cs" Link="Core\WorkshopItems.cs" />
    <Compile Include="..\GK2ModInstaller.Core\TrustStore.cs" Link="Core\TrustStore.cs" />
    <Compile Include="..\GK2ModInstaller.Core\ModFingerprint.cs" Link="Core\ModFingerprint.cs" />
    <Compile Include="..\GK2ModInstaller.Core\CodeScan.cs" Link="Core\CodeScan.cs" />
    <Compile Include="..\GK2ModInstaller.Core\ConsentPlanner.cs" Link="Core\ConsentPlanner.cs" />
    <Compile Include="..\GK2ModInstaller.Core\AcfTimes.cs" Link="Core\AcfTimes.cs" />
    <Compile Include="..\GK2ModInstaller.Core\LoaderDialog.cs" Link="Core\LoaderDialog.cs" />
    <Compile Include="..\GK2ModInstaller.Core\PendingList.cs" Link="Core\PendingList.cs" />
    <Compile Include="..\GK2ModInstaller.Core\GameFolderInstaller.cs" Link="Core\GameFolderInstaller.cs" />
    <Compile Include="..\GK2ModInstaller.Core\LoaderText.cs" Link="Core\LoaderText.cs" />
    <Compile Include="..\GK2ModInstaller.Core\LoaderConfig.cs" Link="Core\LoaderConfig.cs" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="BepInEx"><HintPath>$(GameDir)\BepInEx\core\BepInEx.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Mono.Cecil"><HintPath>$(GameDir)\BepInEx\core\Mono.Cecil.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
```
(Список линков сверен с фактическим содержимым `src\GK2ModInstaller.Core`: `WorkshopSync`, `WorkshopLoader`, `WorkshopItems`, `TrustStore`, `ModFingerprint`, `CodeScan`, `ConsentPlanner`, `AcfTimes`, `LoaderDialog`, `PendingList`, `GameFolderInstaller`, `LoaderText`, `LoaderConfig`. Файлы `BepInExInstaller`, `GameLocator`, `VdfParser`, `ZipExtractor`, `Placeholder` загрузчику не нужны — не линковать.)

`src/GK2ModInstaller.Loader/Entry.cs`:
```csharp
using BepInEx.Logging;
using GK2ModInstaller.Core;

namespace GK2ModInstaller.Loader
{
    // ЗАМОРОЖЕННЫЙ контракт: заглушка в BepInEx\patchers вызывает именно этот метод.
    // Менять сигнатуру можно только вместе с заглушкой (защищено тестом EntryContractTests).
    public static class Entry
    {
        public static void Run(string bepInExRoot, string gameRoot, string workshopRoot, string acfPath, string source)
        {
            var log = Logger.CreateLogSource("GK2.WorkshopLoader");
            log.LogInfo($"загрузчик {typeof(Entry).Assembly.GetName().Version} (источник: {source})");
            WorkshopLoader.Run(
                new LoaderOptions { WorkshopRoot = workshopRoot, WorkshopAcfPath = acfPath, BepInExRoot = bepInExRoot },
                new Win32Dialog(), log.LogInfo);
        }
    }
}
```

- [ ] **Step 2: Подключить проект к решению и тестам**

```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\GK2ModInstaller.sln" add "src\GK2ModInstaller.Loader\GK2ModInstaller.Loader.csproj"
```
В `tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj` добавить (нужно, чтобы рефлексия по сборке загрузчика не спотыкалась о BepInEx):
```xml
    <Reference Include="BepInEx"><HintPath>$(GameDir)\BepInEx\core\BepInEx.dll</HintPath><Private>true</Private></Reference>
```
(в существующий `ItemGroup` с `Mono.Cecil`).

- [ ] **Step 3: Написать падающий тест контракта**

`tests/GK2ModInstaller.Tests/EntryContractTests.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class EntryContractTests
    {
        private static Assembly LoadLoader()
        {
            // ищем сборку загрузчика рядом с тестовой (копируется project reference'ом решения)
            var dir = AppContext.BaseDirectory;
            var path = Path.Combine(dir, "GK2.WorkshopLoader.dll");
            Assert.True(File.Exists(path), "GK2.WorkshopLoader.dll не найден рядом с тестами: " + path);
            return Assembly.LoadFrom(path);
        }

        [Fact]
        public void Entry_run_has_the_frozen_signature()
        {
            var asm = LoadLoader();
            var type = asm.GetType("GK2ModInstaller.Loader.Entry", throwOnError: false);
            Assert.NotNull(type);
            var run = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(run);
            Assert.Equal(typeof(void), run.ReturnType);
            var pars = run.GetParameters();
            Assert.Equal(new[] { "bepInExRoot", "gameRoot", "workshopRoot", "acfPath", "source" }, pars.Select(p => p.Name).ToArray());
            Assert.All(pars, p => Assert.Equal(typeof(string), p.ParameterType));
        }

        [Fact]
        public void Loader_assembly_is_named_as_expected()
        {
            var asm = LoadLoader();
            Assert.Equal("GK2.WorkshopLoader", asm.GetName().Name);
        }
    }
}
```
Чтобы сборка попадала в выход тестов, добавить в `tests/GK2ModInstaller.Tests/GK2ModInstaller.Tests.csproj` ProjectReference на загрузчик:
```xml
    <ProjectReference Include="..\..\src\GK2ModInstaller.Loader\GK2ModInstaller.Loader.csproj" />
```
(ссылка нужна только для копирования файла в выход; типы не используются).

- [ ] **Step 4: Запустить — падает**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~EntryContractTests"`
Expected: FAIL (`GK2.WorkshopLoader.dll не найден…` или `Entry` не найден).

- [ ] **Step 5: Собрать и запустить — проходят**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release` затем тесты.
Expected: сборка решения чистая; `EntryContractTests` — 2/2; остальные тесты зелёные.

- [ ] **Step 6: Коммит**

```bash
git add -A
git commit -m "refactor: extract loader logic into GK2.WorkshopLoader.dll with frozen Entry contract"
```

---

### Task 3: Заглушка в патчере

**Files:**
- Create: `src/GK2ModInstaller.Patcher/Bootstrap.cs`
- Modify: `src/GK2ModInstaller.Patcher/WorkshopAutoLoaderPatcher.cs`
- Modify: `src/GK2ModInstaller.Patcher/GK2ModInstaller.Patcher.csproj`

**Interfaces:**
- Consumes: `LoaderSourceSelector` (Task 1), `Entry` из загрузчика (Task 2) — по рефлексии, без ссылки на проект.
- Produces: `GK2.WorkshopAutoLoader.dll` (заглушка) в `BepInEx\patchers`.

- [ ] **Step 1: Написать Bootstrap**

```csharp
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using GK2ModInstaller.Core;

namespace GK2ModInstaller.Patcher
{
    // Заглушка: загружает настоящий загрузчик из Workshop-айтема (или локального фолбэка) и вызывает Entry.Run.
    // Заморожена — вся логика живёт в GK2.WorkshopLoader.dll, обновляемом подпиской.
    internal static class Bootstrap
    {
        private const string WorkshopAppId = "4358690";
        private const string LoaderFileName = "GK2.WorkshopLoader.dll";

        internal static void Run()
        {
            var log = Logger.CreateLogSource("GK2.WorkshopLoader.Bootstrap");
            try
            {
                string bepInExRoot = Paths.BepInExRootPath;
                string gameRoot = Directory.GetParent(bepInExRoot)?.FullName;
                string steamapps = FindSteamAppsRoot(gameRoot);
                string workshopRoot = steamapps == null ? null : Path.Combine(steamapps, "workshop", "content", WorkshopAppId);
                string acfPath = steamapps == null ? null : Path.Combine(steamapps, "workshop", "appworkshop_" + WorkshopAppId + ".acf");

                string itemLoader = string.IsNullOrEmpty(workshopRoot)
                    ? null
                    : Path.Combine(workshopRoot, LoaderSourceSelector.LoaderItemId, "Loader", LoaderFileName);
                string localLoader = Path.Combine(bepInExRoot, LoaderFileName);

                string chosen = LoaderSourceSelector.Select(itemLoader, localLoader);
                log.LogInfo(LoaderSourceSelector.Describe(chosen, itemLoader, localLoader));
                if (chosen == null) return;

                ResolveDependencies(bepInExRoot);

                var assembly = Assembly.Load(File.ReadAllBytes(chosen));
                var entryType = assembly.GetType("GK2ModInstaller.Loader.Entry", throwOnError: false);
                var run = entryType?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                if (run == null)
                {
                    log.LogError("загрузчик: в " + chosen + " нет GK2ModInstaller.Loader.Entry.Run — обновите айтем");
                    return;
                }

                run.Invoke(null, new object[]
                {
                    bepInExRoot, gameRoot, workshopRoot, acfPath, LoaderSourceSelector.KindOf(chosen, itemLoader)
                });
            }
            catch (Exception ex)
            {
                log.LogError("загрузчик: ошибка запуска — " + ex);
            }
        }

        // Загрузчик грузится из байтов, поэтому его зависимости (Mono.Cecil) надо сделать резолвимыми.
        private static void ResolveDependencies(string bepInExRoot)
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    try
                    {
                        var simpleName = new AssemblyName(args.Name).Name + ".dll";
                        var path = Path.Combine(bepInExRoot, "core", simpleName);
                        return File.Exists(path) ? Assembly.Load(File.ReadAllBytes(path)) : null;
                    }
                    catch { return null; }
                };
                var cecil = Path.Combine(bepInExRoot, "core", "Mono.Cecil.dll");
                if (File.Exists(cecil)) Assembly.Load(File.ReadAllBytes(cecil));
            }
            catch { }
        }

        private static string FindSteamAppsRoot(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir)) return null;
            var dir = new DirectoryInfo(gameDir);
            while (dir != null && !dir.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase)) dir = dir.Parent;
            return dir == null ? null : dir.FullName;
        }
    }
}
```

- [ ] **Step 2: Сделать патчер тонким**

`src/GK2ModInstaller.Patcher/WorkshopAutoLoaderPatcher.cs` — заменить целиком:
```csharp
using System.Collections.Generic;
using Mono.Cecil;

namespace GK2ModInstaller.Patcher
{
    public static class WorkshopAutoLoaderPatcher
    {
        public static IEnumerable<string> TargetDLLs { get { yield return "Assembly-CSharp.dll"; } }

        public static void Patch(AssemblyDefinition assembly)
        {
            Bootstrap.Run();
        }
    }
}
```

- [ ] **Step 3: Урезать линки Core в проекте патчера**

В `src/GK2ModInstaller.Patcher/GK2ModInstaller.Patcher.csproj` из `<ItemGroup>` с `Compile Include` оставить **только**:
```xml
    <Compile Include="..\GK2ModInstaller.Core\LoaderSourceSelector.cs" Link="LoaderSourceSelector.cs" />
```
(остальные линки Core переехали в проект загрузчика в Task 2).

- [ ] **Step 4: Собрать всё решение**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release`
Expected: `Сборка успешно завершена`, 0 ошибок; `src\GK2ModInstaller.Patcher\bin\Release\GK2.WorkshopAutoLoader.dll` стал заметно меньше (десятки КБ вместо ~55 КБ), `src\GK2ModInstaller.Loader\bin\Release\GK2.WorkshopLoader.dll` существует.

- [ ] **Step 5: Прогнать тесты**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: все зелёные (97 + новые из Task 1 и 2).

- [ ] **Step 6: Коммит**

```bash
git add -A
git commit -m "feat(patcher): bootstrap stub that loads the loader from the workshop item"
```

---

### Task 4: Инсталлятор кладёт и заглушку, и загрузчик

**Files:**
- Modify: `src/GK2ModInstaller.Core/BepInExInstaller.cs`
- Modify: `src/GK2ModInstaller.App/GK2ModInstaller.App.csproj`, `src/GK2ModInstaller.App/Cli.cs`, `src/GK2ModInstaller.App/MainForm.cs`
- Test: `tests/GK2ModInstaller.Tests/BepInExInstallerTests.cs`

**Interfaces:**
- Consumes: сборку загрузчика из Task 2.
- Produces: `BepInExInstaller.Install(string gameDir, Stream bepinexZip, Stream frameworkZip, Stream patcherDll, Stream loaderDll, bool backupExisting, Action<string> log)`; `Verify(gameDir, expectPatcher)` дополнительно проверяет `BepInEx\GK2.WorkshopLoader.dll`; `Uninstall` удаляет оба файла.

- [ ] **Step 1: Написать падающие тесты**

Добавить в `tests/GK2ModInstaller.Tests/BepInExInstallerTests.cs` (в файле уже есть помощник `private static MemoryStream Zip(params (string name, string content)[] files)` — использовать его):
```csharp
        [Fact]
        public void Install_writes_loader_fallback_next_to_bepinex()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2inst_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var bep = Zip(("BepInEx/core/BepInEx.dll", "x"), ("winhttp.dll", "x"));
                var fw = Zip(("BepInEx/plugins/GK2.Framework/GK2.Framework.dll", "x"));
                using (var patcher = new MemoryStream(new byte[] { 1 }))
                using (var loader = new MemoryStream(new byte[] { 2, 3 }))
                    BepInExInstaller.Install(dir, bep, fw, patcher, loader, false, null);

                var loaderPath = Path.Combine(dir, "BepInEx", "GK2.WorkshopLoader.dll");
                Assert.True(File.Exists(loaderPath));
                Assert.Equal(new byte[] { 2, 3 }, File.ReadAllBytes(loaderPath));
                Assert.True(File.Exists(Path.Combine(dir, "BepInEx", "patchers", "GK2.WorkshopAutoLoader.dll")));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Verify_reports_missing_loader_fallback()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2inst_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "BepInEx", "patchers"));
                Directory.CreateDirectory(Path.Combine(dir, "BepInEx", "core", "BepInEx"));
                File.WriteAllText(Path.Combine(dir, "BepInEx", "patchers", "GK2.WorkshopAutoLoader.dll"), "x");
                File.WriteAllText(Path.Combine(dir, "winhttp.dll"), "x");
                File.WriteAllText(Path.Combine(dir, "BepInEx", "core", "BepInEx.dll"), "x");
                var problems = BepInExInstaller.Verify(dir, expectPatcher: true);
                Assert.Contains(problems, p => p.Contains("GK2.WorkshopLoader.dll"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Uninstall_removes_loader_fallback()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gk2inst_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "BepInEx", "patchers"));
                File.WriteAllText(Path.Combine(dir, "BepInEx", "patchers", "GK2.WorkshopAutoLoader.dll"), "x");
                File.WriteAllText(Path.Combine(dir, "BepInEx", "GK2.WorkshopLoader.dll"), "x");
                BepInExInstaller.Uninstall(dir, null);
                Assert.False(File.Exists(Path.Combine(dir, "BepInEx", "GK2.WorkshopLoader.dll")));
                Assert.False(File.Exists(Path.Combine(dir, "BepInEx", "patchers", "GK2.WorkshopAutoLoader.dll")));
            }
            finally { Directory.Delete(dir, true); }
        }
```
Существующий вызов в этом же файле (`BepInExInstaller.Install(dir, bep, fw, null, false, null)`) обновить под новую сигнатуру: `BepInExInstaller.Install(dir, bep, fw, null, null, false, null)`, и добавить `using System;`/`using System.IO;` если их нет.

- [ ] **Step 2: Запустить — падают**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release --filter "FullyQualifiedName~BepInExInstallerTests"`
Expected: ошибки компиляции (нет перегрузки `Install` с 5 потоками / нет проверки загрузчика).

- [ ] **Step 3: Реализовать**

В `BepInExInstaller.cs`:
- `public const string LoaderFileName = "GK2.WorkshopLoader.dll";`
- `Verify`: после проверки патчера добавить
```csharp
                if (expectPatcher && !File.Exists(Path.Combine(gameDir, BepInExDirName, LoaderFileName)))
                    problems.Add("Нет " + Path.Combine(BepInExDirName, LoaderFileName) + " (офлайн-копия загрузчика)");
```
- `Install`: добавить параметр `Stream loaderDll` и после раскладки патчера:
```csharp
                if (loaderDll != null)
                {
                    var loaderPath = Path.Combine(gameDir, BepInExDirName, LoaderFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(loaderPath));
                    using (var fs = File.Create(loaderPath)) loaderDll.CopyTo(fs);
                    log?.Invoke("Загрузчик: " + Path.Combine(BepInExDirName, LoaderFileName));
                }
```
- `Uninstall`: удалять и `Path.Combine(gameDir, BepInExDirName, LoaderFileName)` (с логом в том же стиле, что и патчер).

- [ ] **Step 4: Обновить вызовы в App**

- `Cli.cs`: `using (var patcher = Open("GK2.WorkshopAutoLoader.dll")) using (var loader = Open("GK2.WorkshopLoader.dll")) BepInExInstaller.Install(gameDir, bep, fw, patcher, loader, false, log);`
- `MainForm.cs`: то же — `using (var loader = _autoLoader.Checked ? OpenResource("GK2.WorkshopLoader.dll") : null)`.
- `GK2ModInstaller.App.csproj`: в таргете `BuildAndCopyPatcher` дополнительно собрать проект загрузчика и скопировать из него `GK2.WorkshopLoader.dll` в `$(OutputPath)`:
```xml
  <Target Name="BuildAndCopyPatcher" AfterTargets="Build">
    <MSBuild Projects="..\GK2ModInstaller.Patcher\GK2ModInstaller.Patcher.csproj" Targets="Build" Properties="Configuration=$(Configuration)" />
    <MSBuild Projects="..\GK2ModInstaller.Loader\GK2ModInstaller.Loader.csproj" Targets="Build" Properties="Configuration=$(Configuration)" />
    <Copy SourceFiles="..\GK2ModInstaller.Patcher\bin\$(Configuration)\GK2.WorkshopAutoLoader.dll" DestinationFolder="$(OutputPath)" SkipUnchangedFiles="true" />
    <Copy SourceFiles="..\GK2ModInstaller.Loader\bin\$(Configuration)\GK2.WorkshopLoader.dll" DestinationFolder="$(OutputPath)" SkipUnchangedFiles="true" />
  </Target>
```

- [ ] **Step 5: Собрать, прогнать тесты**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release` затем `... test -c Release`
Expected: 0 ошибок; `GK2.WorkshopAutoLoader.dll` и `GK2.WorkshopLoader.dll` лежат в `src\GK2ModInstaller.App\bin\Release\`; все тесты зелёные.

- [ ] **Step 6: Коммит**

```bash
git add -A
git commit -m "feat(installer): deploy loader fallback copy and verify/uninstall it"
```

---

### Task 5: E2E в игре (с пользователем)

**Files:** —
**Interfaces:** Consumes всё предыдущее.

- [ ] **Step 1: Подготовить и разложить артефакты**

```powershell
Copy-Item "src\GK2ModInstaller.App\bin\Release\GK2.WorkshopAutoLoader.dll" "E:\GK2Upload\GK2WorkshopAutoLoader\BepInEx\patchers\GK2.WorkshopAutoLoader.dll" -Force
Copy-Item "src\GK2ModInstaller.App\bin\Release\GK2.WorkshopLoader.dll" "E:\GK2Upload\GK2WorkshopAutoLoader\Loader\GK2.WorkshopLoader.dll" -Force
Copy-Item "src\GK2ModInstaller.Loader\bin\Release\GK2.WorkshopLoader.dll" "E:\SteamLibrary\steamapps\common\Graveyard Keeper 2\BepInEx\GK2.WorkshopLoader.dll" -Force
```
(папку `E:\GK2Upload\GK2WorkshopAutoLoader\Loader\` создать). Игра должна быть закрыта; после раскладки — очистить `BepInEx\cache`.

- [ ] **Step 2: Проверить основную механику (пользователь запускает игру)**

Ожидается в `BepInEx\LogOutput.log`: `загрузчик: ...\Loader\GK2.WorkshopLoader.dll (источник: item)` и `загрузчик 1.3.0.0 (источник: item)`; диалоги работают (EN/RU).

- [ ] **Step 3: Главная проверка — обновление без правки `patchers`**

Подменить `E:\GK2Upload\GK2WorkshopAutoLoader\Loader\GK2.WorkshopLoader.dll` на сборку с другой `Version` (например, временно `<Version>1.3.1</Version>`, собрать), скопировать её в **папку айтема** (`steamapps\workshop\content\4358690\3807406994\Loader\`), запустить игру → в логе должна быть версия `1.3.1`, а файл в `BepInEx\patchers` не тронут.

- [ ] **Step 4: Проверить фолбэк и «ничего нет»**

- убрать `Loader\` из папки айтема → в логе `(источник: local)`;
- убрать и локальную копию `BepInEx\GK2.WorkshopLoader.dll` → в логе подсказка про айтем 3807406994 / GK2 Mod Installer, игра стартует нормально.

- [ ] **Step 5: Зафиксировать результат в леджере**

Записать в `.superpowers/sdd/<workspace>/progress.md` строку с результатом E2E и версиями, которые наблюдались.

---

### Task 6: Документация и релизы

**Files:**
- Modify: `README.md`, `dist/RELEASE_NOTES.md`, `E:\GK2Upload\description.txt`, `E:\GK2Upload\GK2WorkshopAutoLoader\README.txt`, `AGENTS.md`
- Publish: айтем 3807406994 (v1.3.0, новая раскладка), GitHub Release v1.2.1

**Interfaces:** Consumes всё предыдущее.

- [ ] **Step 1: README/описание — про авто-обновление**

Добавить в `README.md` (RU и EN) и в `E:\GK2Upload\description.txt` (EN-блок первым, как сейчас):
```
UPDATES ARE AUTOMATIC (v1.3.0)
The file in BepInEx\patchers is a tiny frozen bootstrap: it loads the real loader directly from this
Workshop item each time the game starts. So new versions (dialogs, languages, checks) arrive with your
subscription — no need to re-copy anything after the one-time install.
```
и RU-аналогично («обновления приезжают сами; заглушку в patchers заменить нужно один раз — если ставил вручную»).

- [ ] **Step 2: Пересобрать папку айтема и опубликовать**

Скопировать в `E:\GK2Upload\GK2WorkshopAutoLoader\`: `Loader\GK2.WorkshopLoader.dll` (свежая), `BepInEx\patchers\GK2.WorkshopAutoLoader.dll` (заглушка), обновить `README.txt`. Проверить, что описания < 8000 байт. Затем:
```powershell
& "tools\GK2Publisher\bin\Release\GK2Publisher.exe" --update 3807406994 --folder "E:\GK2Upload\GK2WorkshopAutoLoader" --desc-file "E:\GK2Upload\description.txt" --preview "E:\GK2Upload\GK2WorkshopAutoLoader_preview.png" --public
```
Expected: `SetItemContent=True`, `SubmitItemUpdate: k_EResultOK`. Публиковать только после «ок» пользователя.

- [ ] **Step 3: GitHub Release v1.2.1**

Собрать zip (`GK2ModInstaller.exe` + `GK2.WorkshopAutoLoader.dll` + `GK2.WorkshopLoader.dll` + README + RELEASE_NOTES), затем:
```powershell
git tag v1.2.1; git push origin master; git push origin v1.2.1
& "C:\Program Files\GitHub CLI\gh.exe" release create v1.2.1 "dist\GK2ModInstaller_v1.2.1.zip" --title "GK2 Mod Installer v1.2.1" --notes-file "dist\RELEASE_NOTES.md" -R otkosss-png/GK2ModInstaller
```

- [ ] **Step 4: AGENTS.md + леджер**

Дописать в раздел GK2: новая архитектура (заглушка/загрузчик, контракт `Entry.Run`, где что лежит, как обновляется), и строку о результатах E2E из Task 5.
