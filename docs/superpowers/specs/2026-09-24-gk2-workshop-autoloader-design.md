# GK2 Workshop Auto-Loader — дизайн (спека)

- **Дата:** 2026-09-24
- **Статус:** на ревью
- **Проект:** `C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\`

## 1. Проблема и цель

Код-моды GK2 из Steam Workshop лежат как папки с `BepInEx\plugins\*.dll` внутри
`steamapps\workshop\content\4358690\<id>\`, и сейчас их надо **руками копировать** в
`BepInEx\plugins`. BepInEx 5.4.23.5 не умеет дополнительные папки плагинов (в `BepInEx.cfg`
нет plugin search paths).

**Цель:** автоматически подхватывать код-моды из подписанных Workshop-айтемов, без ручного
копирования. Реализация — **BepInEx preloader-патчер**, который до загрузки плагинов
раскладывает их из Workshop в `BepInEx\plugins\_Workshop\<id>\`.

## 2. Контекст (проверено)

- Контракт патчера BepInEx 5.4.23.5 (`BepInEx.Preloader`): тип в `BepInEx/patchers` с
  `public static IEnumerable<string> TargetDLLs` и
  `public static void Patch(AssemblyDefinition)` (by value или ref). `Patch` вызывается для
  объявленных DLL до запуска игры.
- Пример Workshop-мода (id 3807023815, Better Auto Crafting):
  `BepInEx\plugins\BetterAutoCrafting.dll` + `BepInEx\config\nizy.gk2.betterautocrafting.cfg`.
- Инсталлятор уже ставит BepInEx + GK2 Mod Framework в папку игры (фаза 1, релиз v1.0.0).

## 3. Объём

**Фаза 2 (эта спека):** патчер-автозагрузчик + интеграция в инсталлятор.
**Вне объёма:** автозагрузка скриптовых/локальных (не-Workshop) модов, GUI-список модов,
обновление самих модов.

## 4. Технологии

- Новый проект `src/GK2ModInstaller.Patcher` — **netstandard2.0**.
- Ссылки: `GK2ModInstaller.Core`; `BepInEx.dll` и `Mono.Cecil.dll` из `BepInEx/core`
  (через свойство `GameDir`, как у фреймворка).
- Логика синка — в `Core` (`WorkshopSync`), без BepInEx-зависимостей (тестируемо).
- Fallback-резолв Steam/Workshop — из `Core` (`GameLocator`/`VdfParser`).

## 5. Компоненты

### 5.1 Патчер (`GK2ModInstaller.Patcher`)
```csharp
public static class WorkshopAutoLoaderPatcher
{
    public static IEnumerable<string> TargetDLLs { get { yield return "Assembly-CSharp.dll"; } }
    public static void Patch(AssemblyDefinition assembly)
    {
        // резолв путей (см. ниже), затем:
        WorkshopSync.RunOnce(workshopRoot, bepInExRoot, log);
    }
}
```
- `TargetDLLs` — `Assembly-CSharp.dll` (всегда есть среди managed DLL игры).
- Резолв путей: `BepInEx.Paths.BepInExRootPath` → папка игры = родитель `BepInEx` →
  `<...>\steamapps\workshop\content\4358690`; fallback — реестр Steam + `libraryfolders.vdf`.
- Лог: `BepInEx.Logging.Logger` → `BepInEx\LogOutput.log`.

### 5.2 Синк (`WorkshopSync` в Core)
`RunOnce(string workshopRoot, string bepInExRoot, Action<string> log)`, идемпотентно (один прогон за старт):
1. Если `WorkshopRoot` не найден — выйти с записью в лог.
2. Собрать id-папки из `WorkshopRoot`.
3. Для каждого айтема с `BepInEx\plugins\*.dll`:
   - копировать `BepInEx\plugins\**` → `BepInEx\plugins\_Workshop\<id>\`;
   - копировать `BepInEx\config\*.cfg` → `BepInEx\config\` (только если файла нет).
4. Для `_Workshop\<id>\`, чьих id больше нет в Workshop — удалить.
5. Записать в лог сводку (сколько айтемов/файлов, что удалено).

## 6. Интеграция в инсталлятор

- Новый чек-бокс **«Автозагрузка Workshop-модов»** (по умолчанию вкл.).
- Патчер-DLL вшивается в exe (EmbeddedResource) и кладётся в `BepInEx\patchers\`.
- `BepInExInstaller.Verify` дополнительно проверяет `BepInEx\patchers\GK2.WorkshopAutoLoader.dll`.
- `Uninstall` удаляет и патчер.
- CLI (`--install/--uninstall`) учитывает патчер; README обновляется.

## 7. Тестирование

- Юнит-тесты `WorkshopSync` (net8.0, временные папки): копирование DLL; копирование config
  только если отсутствует; удаление `_Workshop\<id>` для пропавших айтемов; идемпотентность.
- Ручной E2E: поставить обновлённый инсталлятор; подписаться на код-мод (Better Auto Crafting);
  запустить игру → в `LogOutput.log` есть загрузка мода; в `BepInEx\plugins\_Workshop\<id>\` —
  DLL; отписаться → папка исчезает.

## 8. Распространение

- Через инсталлятор: **GitHub Release v1.1.0** (exe + README).
- Позже (опц.): отдельный Workshop-айтем с патчером для ручной установки.

## 9. Риски / ограничения

- Патчер исполняется в preloader-процессе — только файловые операции (без Unity-API).
- Если `Assembly-CSharp.dll` не попадёт в `TargetDLLs`-обработку, `Patch` не вызовется —
  проверить в E2E.
- Моды, кладущие файлы не по схеме `BepInEx/plugins` (например, в корень игры), не
  поддержаны.
- Steam-путь Workshop на другом томе — резолв через реестр/vdf.
