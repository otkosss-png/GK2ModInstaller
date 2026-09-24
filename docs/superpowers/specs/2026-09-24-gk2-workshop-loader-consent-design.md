# GK2 Workshop Loader — согласие на моды (consent) — дизайн (спека)

- **Дата:** 2026-09-24
- **Статус:** на ревью
- **Проект:** `C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\`
- **База:** фаза 2 автозагрузчика (`docs/superpowers/specs/2026-09-24-gk2-workshop-autoloader-design.md`)

## 1. Проблема и цель

Сейчас автозагрузчик (фаза 2) **молча** копирует плагины из подписанных Workshop-айтемов в
`BepInEx\plugins\_Workshop\<id>\` и запускает их. Это удобно, но опасно: любой обновлённый
(или взломанный/подменённый) мод запускается без ведома игрока.

**Цель:** паритет с конкурентом «GK2 Workshop Loader» по безопасности:
1. **новый мод не запускается без одобрения** (Yes / No / Cancel);
2. **обновление тоже требует одобрения**; отложенное (`Cancel`) обновление **не мешает
   работать прежней одобренной версии**;
3. **статический код-чек** — предупреждаем, если мод использует сеть, запускает процессы,
   удаляет файлы, грузит дополнительный код, лезет в реестр или в Win32;
4. **детект дубликатов** — мод стоит и из Workshop, и вручную в `BepInEx\plugins`;
5. состояние одобрения — **человекочитаемый trust-файл**, строку можно удалить, чтобы
   спросили заново.

## 2. Контекст (проверено)

- Патчер `GK2.WorkshopAutoLoader.dll` (netstandard2.0) лежит в `BepInEx\patchers`, исполняется
  в preloader-процессе до загрузки плагинов. Только файловые операции, без Unity API.
- `TargetDLLs` → `Assembly-CSharp.dll`; при недоступности патча `Patch` не вызовется.
- Патчер компилирует `WorkshopSync.cs` из Core **линком** (`Compile Include`), поэтому DLL
  самодостаточна. Новую логику в Core добавляем так же линком; она должна быть netstandard2.0
  и без BepInEx-зависимостей (BepInEx берётся только в патчерной части).
- `Mono.Cecil` уже подключён к патчеру (ссылка из `BepInEx\core`) — используем и для метаданных
  сборки (имя/версия/автор), и для статического скана IL.
- **`System.Windows.Forms.dll` в игре НЕТ** (в `GraveyardKeeper2_Data\Managed` только
  `System.Drawing.dll`), поэтому WinForms-диалог невозможен. Диалог делаем через
  `user32.MessageBoxW` (P/Invoke) — это работает в preloader.
- Резолв путей: `BepInEx.Paths.BepInExRootPath` → папка игры → `<...>\steamapps\workshop`.
  Рядом с `content\` лежит `appworkshop_4358690.acf`; `VdfParser` в Core уже умеет VDF-разбор.

## 3. Объём

**В объёме:** согласие (новые моды + обновления), trust-файл, отпечаток версии, миграция уже
установленных модов, детект дубликатов, статический код-чек (только предупреждения),
интеграция в инсталлятор и в Workshop-айтем патчера.

**Вне объёма:** GUI-список модов внутри игры; удаление/отключение модов из UI; подпись модов;
запрет опасного (блокировка) — по решению пользователя только предупреждаем.

## 4. Компоненты

Всё, кроме диалога, — чистая логика в Core (линкуется в патчер), тестируется без игры.

### 4.1 `WorkshopItems` (Core)
```csharp
public sealed class WorkshopItem {
    public string Id;                 // имя папки айтема
    public string Dir;                // steamapps\workshop\content\4358690\<id>
    public IReadOnlyList<string> PluginFiles; // относительные пути внутри BepInEx\plugins
    public string Title;              // из метаданных сборки, иначе id
    public string Version;            // AssemblyFileVersion/InformationalVersion, иначе ""
}
public static class WorkshopItemsScanner {
    public static List<WorkshopItem> Scan(string workshopRoot);
}
```
Правила: айтем учитывается, только если под `BepInEx\plugins` есть хотя бы один `*.dll`
(айтемы-переводы и прочее пропускаем **тихо**). `Title` берём из метаданных первой DLL
(`AssemblyTitle`/`Product`, иначе имя файла); `Version` — из `AssemblyFileVersion` или
`AssemblyInformationalVersion`.

### 4.2 `ModFingerprint` (Core)
```csharp
// pluginsDir — корень BepInEx\plugins айтема; обход только *.dll и файлов рядом с ними
public static string Compute(string pluginsDir);
```
Отпечаток = SHA-256 от склеенных строк `"<относительный путь в нижнем регистре>\n<sha256 файла>\n"`
в порядке сортировки (OrdinalIgnoreCase). Файлы `BepInEx\config\*.cfg` **исключаются** из
отпечатка: они не перезаписываются при синке и правятся игроком, иначе любой правки конфига
хватало бы, чтобы «версия изменилась».

### 4.3 `TrustStore` (Core)
Файл `BepInEx\config\GK2_WorkshopLoader.trust.txt`, кодировка UTF-8, один айтем в строке:
```
# id | sha256 | state | title | note
3807023815|6f1c...|yes|Better Auto Crafting|одобрено 24.09
3807111111|a0b2...|no|Suspicious Mod|заблокирован
```
- `state` ∈ `yes` (грузить), `no` (заблокирован навсегда), `ask` (спрашивать каждый раз).
- Строки, начинающиеся с `#`, и пустые — игнорируются и **сохраняются** при записи (шапка).
- Нет строки для id → `ask`. Есть строка, но `sha256` другой → `ask` (это обновление).
- Правка вручную (удалить/поменять строку) — штатный способ сбросить решение.
- API: `Load(path)`, `Save(path, entries)`, `Get(id)`, `Set(entry)`, `Remove(id)`.

### 4.4 `CodeScan` (Core, Cecil)
```csharp
public enum FindingCategory { Network, Process, FileDelete, CodeLoad, Registry, Native }
public sealed class Finding { public FindingCategory Category; public string Detail; }
public static class CodeScan { public static IReadOnlyList<Finding> Scan(string dllPath); }
```
Признаки (по ссылкам на члены и по `DllImport`):
- **Network:** `System.Net.Http.HttpClient`, `System.Net.WebClient`, `HttpWebRequest`,
  `WebRequest`, `System.Net.Sockets.Socket`, `System.Net.Dns`;
- **Process:** `System.Diagnostics.Process.Start`, `ProcessStartInfo`;
- **FileDelete:** `System.IO.File.Delete`, `Directory.Delete`, `File.Move`, `File.Replace`;
- **CodeLoad:** `Assembly.Load/LoadFrom/LoadFile/UnsafeLoadFrom`, `AppDomain.Load`,
  `System.Runtime.Loader.AssemblyLoadContext`, `System.Reflection.Emit.*`;
- **Registry:** `Microsoft.Win32.Registry`, `RegistryKey`;
- **Native:** методы с `[DllImport]`.
Результат — список категорий с примерами членов; в диалоге показываем категории
(«сеть, запуск процессов»). Скан — справочный, не блокирующий.

### 4.5 `ConsentPlan` (Core)
```csharp
public enum DecisionKind { New, Update, Known, Blocked, Removed }
public sealed class PlanEntry {
    public DecisionKind Kind;
    public WorkshopItem Item;
    public TrustEntry Trust;                       // null для New
    public string Fingerprint;                     // отпечаток текущих файлов айтема
    public bool Staged;                            // файлы уже лежат в _Workshop\<id>
    public IReadOnlyList<Finding> Findings;        // из CodeScan
    public IReadOnlyList<string> Duplicates;       // имена сборок, найденных и вручную
}
public static class ConsentPlanner {
    public static List<PlanEntry> Build(
        IReadOnlyList<WorkshopItem> items,
        TrustStore trust,
        Func<WorkshopItem, string> fingerprint,
        Func<WorkshopItem, IReadOnlyList<Finding>> scanFindings,
        IReadOnlyList<string> manuallyInstalledAssemblies,
        IReadOnlyList<string> stagedIds);
}
```
Классификация айтема: `New` (нет строки в trust или `state=ask`), `Blocked` (`state=no`),
`Update` (`yes`, отпечаток другой), `Known` (`yes`, отпечаток совпал); `Removed` — для id,
которые есть в `_Workshop` (`stagedIds`), но пропали из Workshop.
`scanFindings` вызывается **только** для `New`/`Update` (не сканируем уже одобренное на
каждом старте). `Staged` = id есть в `stagedIds` (нужно для миграции).

`Duplicate` — **не вид решения, а список** имён сборок, которые есть и в айтеме, и в ручной
установке: `manuallyInstalledAssemblies` = имена сборок (`AssemblyDefinition.Name.Name`) всех
`*.dll` из `BepInEx\plugins` **кроме** подпапки `_Workshop`; пересечение с именами сборок
айтема и попадает в `PlanEntry.Duplicates`. Показывается в диалоге и в логе, файлы не трогаем.

### 4.6 Диалог (патчер)
```csharp
public enum ConsentAnswer { Approve, Deny, Later }
public enum BulkAnswer { All, AskEach, Later }
public interface IDialog {
    ConsentAnswer Ask(ModPrompt prompt);              // Yes / No / Cancel
    BulkAnswer AskBulkTrust(IReadOnlyList<ModPrompt> mods); // миграция
    void Warn(string text);                           // информационное окно (OK)
}
```
Реализация — `Win32Dialog : IDialog` через `user32.MessageBoxW`:
- кнопки `MB_YESNOCANCEL (0x3)`, иконка `MB_ICONWARNING (0x30)`, `MB_TOPMOST (0x40000)`,
  `MB_SETFOREGROUND (0x10000)`; результат `6 = Yes`, `7 = No`, `2 = Cancel`;
- текст диалога (пример):
  ```
  Новый мод из Workshop

  Better Auto Crafting v1.2 (id 3807023815)
  Файлы: BetterAutoCrafting.dll

  Проверка кода: сеть, запуск процессов
  Дубликат: та же сборка уже стоит вручную (BepInEx\plugins\BetterAutoCrafting.dll)

  Мод запускается с правами игры (файлы, интернет).
  Одобрить этот мод?

  Yes — одобрить · No — заблокировать навсегда · Cancel — спросить в следующий раз
  ```
- `null`/`0` (не удалось показать окно) трактуем как `Later`.

### 4.7 Поток одного запуска (патчер)
1. Резолв путей (`WorkshopRoot`, `ACF`, `BepInExRoot`). Нет Workshop — выход с логом.
2. `WorkshopItemsScanner.Scan` → айтемы с плагинами.
3. Для каждого — отпечаток; `TrustStore.Load`; `ConsentPlanner.Build`.
4. **Миграция:** если есть айтемы, чьи файлы уже лежат в `_Workshop\<id>`, но trust-записи нет
   — один **сводный** диалог «Найдено N модов, поставленных прежней версией без проверки.
   Доверять им всем?»: `Yes` → всем `yes` с текущими хешами; `No` → спросить каждый по
   отдельности; `Cancel` → оставить как есть (не трогаем, работает) и спросить снова.
5. Для каждого `New`/`Update` (кроме выданных сводным `Yes`) — диалог: `Approve` → `yes`,
   `Deny` → `no` + удалить `_Workshop\<id>`, `Later` → `ask`, файлы не трогаем.
6. Применить: `Known` — ничего, если копия уже есть; если копии нет (её удалили вручную) —
   восстановить из Workshop; `Approved` — копировать `BepInEx\plugins\**` в `_Workshop\<id>`
   (+ `config\*.cfg` только если файла нет), `Removed` — удалить `_Workshop\<id>`;
   `Blocked` (`no`) — молча удалить `_Workshop\<id>`, не спрашивая.
7. `TrustStore.Save` (если менялся), `appworkshop_4358690.acf` → лог «доступно обновление»
   для айтемов, где `timeupdated` новее trust-записи, но файлы совпали (справочно).
8. Сводка в `LogOutput.log`: сколько айтемов, сколько одобрено/заблокировано/отложено,
   что удалено, по каждому — найденные предупреждения.

## 5. Ошибки и безопасность (fail-closed)

- Диалог не показался → `Later`: **ничего нового не копируем**, неодобренные не грузятся,
  пишем в лог и в `BepInEx\config\GK2_WorkshopLoader.pending.txt` (id + название) для ручного
  разбора.
- Trust-файл битый/нечитаемый → считаем пустым (не падаем), битые строки в лог, исходный файл
  переименовываем в `*.bak-<дата>`.
- Ошибка при копировании одного айтема не срывает остальные (try/catch на айтем, лог).
- `Deny` и `Removed` удаляют только `_Workshop\<id>` — папку Steam не трогаем никогда.
- Патчер по-прежнему не использует Unity API и не требует сети.

## 6. Интеграция

- Патчер-DLL вшивается в exe инсталлятора (как сейчас) → **GitHub Release v1.2.0**.
- Workshop-айтем патчера **3807406994**: обновить `BepInEx\patchers\GK2.WorkshopAutoLoader.dll`,
  `README.txt`, описание (RU+EN) — через `GK2Publisher --update 3807406994 --folder ... --desc-file ...`.
- `BepInExInstaller.Verify`/`Uninstall` — без изменений (тот же путь патчера).
- Сброс решений — **вручную**: удалить `BepInEx\config\GK2_WorkshopLoader.trust.txt` (или строку
  конкретного мода). Отдельная CLI-команда не делается. Совместимость: старый `--install`
  работает как раньше.

## 7. Тестирование

Юнит-тесты (net8.0, `tests\GK2ModInstaller.Tests`, временные папки):
1. `ModFingerprint`: стабилен при разном порядке обхода; меняется при правке DLL; **не**
   меняется при правке `BepInEx\config\*.cfg`.
2. `TrustStore`: парсинг строк, сохранение комментариев и порядка, `Set`/`Remove`, битый файл →
   пустой стор + `*.bak`.
3. `ConsentPlanner`: `New`/`Update`/`Known`/`Removed`; `Duplicate` флаг при совпадении имени
   сборки с ручной установкой; айтемы без плагинов не попадают в план.
4. `CodeScan`: на тестовой сборке, ссылающейся на `Process.Start` и `HttpClient`, находит
   `Process` и `Network`; на «чистой» сборке находок нет.
5. Миграция: файлы уже в `_Workshop\<id>` без trust-записи → помечаются как миграция.

E2E (вручную, игра + Steam):
- подписка на тестовый код-мод → при старте диалог → `Yes` → мод загрузился
  (`LogOutput.log`), файлы в `_Workshop\<id>`, строка `yes` в trust;
- `No` → мод не загрузился, папки `_Workshop\<id>` нет, строка `no`;
- `Cancel` → мод не загрузился, при следующем старте спросит снова;
- правка мода автором (новый хеш) → диалог «обновление» → `Cancel` → работает прежняя версия;
- удаление строки из trust → снова спросит;
- ручная копия той же DLL в `BepInEx\plugins` → в диалоге есть предупреждение о дубликате.

## 8. Риски / ограничения

- `MessageBoxW` из preloader блокирует главный поток до ответа — при большом числе модов
  пользователь отвечает N раз (смягчено сводным диалогом миграции и `Cancel`).
- Диалог может оказаться за другим окном — в тексте пишем «нажмите Alt+Tab» (как у конкурента).
- Не различаем моды, поставляющие данные (переводы), и код: айтемы без `plugins\*.dll`
  пропускаем тихо, но айтем с одним `.dll`, который на самом деле не плагин, будет одобряться
  как код.
- Статический скан — эвристика (reflection/`Type.GetType` по строке не ловим); это
  предупреждение, а не гарантия (так и написано в описании мода).
- Отпечаток по содержимому DLL не защищает от мода, который **догружает данные из своих же
  файлов** вне `BepInEx\plugins` (их мы в отпечаток не берём) — ограничение принимаем,
  в отпечаток входит всё содержимое `BepInEx\plugins\**`, включая ресурсы рядом с DLL.
