# GK2 Loader Bootstrap — авто-обновление загрузчика без ручных действий (спека)

- **Дата:** 2026-09-25
- **Статус:** на ревью
- **Проект:** `C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\`
- **База:** фича согласия v1.2.0 (`docs/superpowers/specs/2026-09-24-gk2-workshop-loader-consent-design.md`)

## 1. Проблема и цель

Загрузчик — это **BepInEx-patcher**: BepInEx грузит его до старта игры и держит файл занятым, поэтому
обновиться сам он не может. Сейчас после каждого обновления айтема 3807406994 пользователь должен
вручную перезаписать `BepInEx\patchers\GK2.WorkshopAutoLoader.dll` (пере-скопировать из папки айтема).

**Цель:** один раз поставить «заглушку» — дальше все обновления загрузчика приезжают подпиской Steam
автоматически, файл в `patchers` больше не трогается.

## 2. Контекст (проверено)

- Текущий патчер — `src/GK2ModInstaller.Patcher` (netstandard2.0, линкует исходники Core). Он делает
  всю работу: диалоги согласия (`Win32Dialog` + `user32.MessageBoxW`), план, копирование, game-folder
  моды, trust/pending, код-скан (Mono.Cecil), локализация EN/RU (коммит `b86988a`, уже в айтеме;
  отдельного релиза с этим номером нет — версия видна только в самом DLL).
- Линковка Core-файлов в патчер — через `<Compile Include="..\GK2ModInstaller.Core\<File>.cs" Link="..."/>`
  (DLL самодостаточна, без Core.dll).
- BepInEx 5.4.23.5 preloader: `TargetDLLs` + `Patch(AssemblyDefinition)`; `BepInEx\core` содержит
  `Mono.Cecil.dll`, который резолвится для обычных патчеров из `patchers`.
- Инсталлятор (`src/GK2ModInstaller.App`, net48) вшивает патчер и кладёт его в `patchers`
  (MSBuild-таргет `BuildAndCopyPatcher`); релиз v1.2.0 опубликован.
- Айтем 3807406994 сейчас содержит `BepInEx\patchers\GK2.WorkshopAutoLoader.dll` (полная логика) +
  `README.txt` + `Thumbnail.png`; установка и согласие проверены на живых модах (`Recipe Pin`,
  `Better Auto Crafting`). Число подписчиков — на странице айтема.

## 3. Объём

**В объёме:** вынести логику в отдельную сборку загрузчика; заглушка в `patchers`; выбор источника
(айтем → локальный фолбэк); офлайн-фолбэк в инсталляторе; тесты на выбор источника и контракт входа;
правки раскладки айтема и документации; релизы.

**Вне объёма:** изменения самой логики согласия (кроме переноса), поддержка других лоадеров/источников,
само-замена заглушки (её обновление остаётся ручным — но она заморожена и меняться не должна).

## 4. Раскладка артефактов

| Место | Что | Обновляется |
|---|---|---|
| `BepInEx\patchers\GK2.WorkshopAutoLoader.dll` | **заглушка** (~80 строк, заморожена) | вручную/инсталлятором, редко |
| `<workshop>\content\4358690\3807406994\Loader\GK2.WorkshopLoader.dll` | **логика загрузчика** | автоматически подпиской |
| `BepInEx\GK2.WorkshopLoader.dll` | тот же загрузчик, **офлайн-фолбэк** | инсталлятором |
| айтем 3807406994 | `Loader\…`, `BepInEx\patchers\GK2.WorkshopAutoLoader.dll` (для ручной установки), `README.txt`, `Thumbnail.png` | публикацией |

Имя файла заглушки оставляем прежним (`GK2.WorkshopAutoLoader.dll`) — существующим пользователям
достаточно один раз перезаписать его; двух патчеров не появляется.
В айтеме **нет** `BepInEx\plugins` → наш собственный айтем никогда не стейджится сам.

## 5. Компоненты

### 5.1 Заглушка (`src/GK2ModInstaller.Patcher`, тонкая)
```csharp
public static class WorkshopAutoLoaderPatcher
{
    public static IEnumerable<string> TargetDLLs { get { yield return "Assembly-CSharp.dll"; } }
    public static void Patch(AssemblyDefinition assembly) { Bootstrap.Run(); }
}
```
`Bootstrap.Run()` (тестируемая часть — в Core, см. 5.3):
1. `bepInExRoot = Paths.BepInExRootPath`; `gameRoot = Directory.GetParent(bepInExRoot)`;
   `workshopRoot = <steamapps>\workshop\content\4358690`; `acf = <steamapps>\workshop\appworkshop_4358690.acf`.
2. Выбор источника: `itemLoader = <workshopRoot>\3807406994\Loader\GK2.WorkshopLoader.dll`,
   `localLoader = <bepInExRoot>\GK2.WorkshopLoader.dll` → `LoaderSourceSelector.Select(itemLoader, localLoader)`
   (айтем приоритетнее локального; иначе null → понятный лог с подсказкой «подпишитесь или запустите инсталлятор»).
3. Резолв зависимостей: загрузить `BepInEx\core\Mono.Cecil.dll` (если ещё не загружен) и повесить
   `AppDomain.CurrentDomain.AssemblyResolve`, резолвящий из `BepInEx\core`.
4. `var asm = Assembly.Load(File.ReadAllBytes(path));` (байты — файл не блокируется, Steam может обновлять)
   → тип `GK2ModInstaller.Loader.Entry` → `Run(string bepInExRoot, string gameRoot, string workshopRoot, string acfPath, string source)`
   (`source` = `"item"` | `"local"`; **сигнатура заморожена**).
5. Всё в try/catch: preloader не падает; в лог — «загрузчик: <путь> (версия X, источник item|local)».

### 5.2 Загрузчик (`src/GK2ModInstaller.Loader`, новый проект netstandard2.0)
- Сюда **переезжают** файлы из `GK2ModInstaller.Patcher`: `Win32Dialog.cs`, весь вызов
  `WorkshopLoader.Run(...)`, плюс тот же набор `Compile Include` линков Core.
- Новый `Entry.cs`:
```csharp
namespace GK2ModInstaller.Loader
{
    public static class Entry
    {
        public static void Run(string bepInExRoot, string gameRoot, string workshopRoot, string acfPath, string source)
        {
            var log = BepInEx.Logging.Logger.CreateLogSource("GK2.WorkshopLoader");
            log.LogInfo($"загрузчик {typeof(Entry).Assembly.GetName().Version} (источник: {source})");
            WorkshopLoader.Run(new LoaderOptions { WorkshopRoot = workshopRoot, WorkshopAcfPath = acfPath, BepInExRoot = bepInExRoot },
                               new Win32Dialog(), log.LogInfo);
        }
    }
}
```

### 5.3 Core-добавление: `LoaderSourceSelector`
```csharp
public static class LoaderSourceSelector
{
    // приоритет: айтем, затем локальный фолбэк; null — ничего нет
    public static string Select(string itemLoaderPath, string localLoaderPath);
    public static string Describe(string chosenPath, string itemLoaderPath, string localLoaderPath); // для лога
}
```
Чистая логика путей (проверка существования файла), без I/O кроме `File.Exists` — тестируется.

Заглушка (`GK2ModInstaller.Patcher`) линкует из Core **только** `LoaderSourceSelector.cs`; остальные
линки Core переезжают в проект загрузчика. Версию сборки загрузчика (`AssemblyVersion`, сейчас
`1.0.0.0`) поднимаем на каждом релизе — она попадает в лог, по ней в E2E видно подхват обновления.

## 6. Изменения инсталлятора (`GK2ModInstaller.App`)
- Вшивание и раскладка: заглушка → `BepInEx\patchers\GK2.WorkshopAutoLoader.dll`,
  загрузчик → `BepInEx\GK2.WorkshopLoader.dll` (офлайн-фолбэк).
- `BepInExInstaller.Verify` проверяет оба файла; `Uninstall` удаляет оба.
- MSBuild-таргет копирует обе сборки в выход инсталлятора; чек-бокс автозагрузки — как раньше.

## 7. Тестирование

- Юнит-тесты (net8.0): `LoaderSourceSelectorTests` (айтем есть → айтем; только локальный → локальный;
  ничего → null; пустые/несуществующие пути), `EntryContractTests` (рефлексией: `Entry.Run` — `public static`,
  ровно 5 параметров типа `string`, namespace/имя совпадают — защита от «случайно поменяли контракт»),
  проверка что сборка загрузчика не ссылается на BepInEx-типы в публичном API `Entry`.
- Все 97 существующих тестов остаются зелёными.
- E2E (вручную, с пользователем):
  1. перезаписать `patchers`-файл заглушкой → запуск: в логе «загрузчик … (источник: item)», диалоги работают;
  2. подменить `<workshop>\…\3807406994\Loader\GK2.WorkshopLoader.dll` на свежую сборку → запуск:
     в логе новая версия, **файл в `patchers` не трогали** (главная проверка);
  3. переименовать папку айтема → запуск: «источник: local» (фолбэк);
  4. удалить оба файла → запуск без падения + понятная подсказка в логе.

## 8. Риски / ограничения

- **Резолв зависимостей** у сборки, загруженной из байтов: закрываем предзагрузкой `Mono.Cecil` из
  `BepInEx\core` + `AssemblyResolve`. Если Cecil всё же не найдётся — загрузчик не стартует; в логе будет видно.
- **Жёсткий контракт** `Entry.Run` (5 строк): меняем только вместе с заглушкой; защищён тестом.
- Один раз заменить старый патчер всё равно нужно (у кого он от v1.2.0/1.2.1) — пишем в описании/README.
- Steam может обновить файлы айтема при запущенной игре — не мешает (читаем байты на старте).
- Заглушка **не** само-обновляется (осознанно: она заморожена). Если когда-нибудь понадобится её менять —
  это снова «одно ручное обновление», и версию заглушки пишем в лог.
- Антивирус/Doorstop и прочее — без изменений.

## 9. Распространение

- Айтем 3807406994 → **v1.3.0**: новая раскладка (`Loader\`), обновлённые README/описание
  («обновления приезжают сами; заглушку в `patchers` заменить один раз»), превью.
- GitHub Release **v1.2.1** инсталлятора (вшивает заглушку + загрузчик) + обновление описания айтема.
- AGENTS.md: новая архитектура (заглушка/загрузчик, контракт `Entry`, где что лежит).
