# GK2 Mod Installer — дизайн (спека)

- **Дата:** 2026-09-24
- **Статус:** на ревью
- **Проект:** `C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\`

## 1. Проблема и цель

Код-моды Graveyard Keeper 2 требуют **BepInEx 5.4.23.5 x64**, который сейчас надо ставить
вручную (скачать, распаковать в папку игры, запустить игру, потом руками копировать папку мода
из воркшопа). Это барьер для обычных игроков.

**Цель:** один GUI-установщик, который в пару кликов ставит **BepInEx + GK2 Mod Framework**,
после чего игрок может ставить моды из Steam Workshop.

## 2. Контекст (проверено на машине)

- Игра: `Graveyard Keeper 2`, Steam appid **4358690**, путь `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2`.
- Движок: **Unity 6000.3.9f1**, **Mono x64** (есть `MonoBleedingEdge`, нет `il2cpp_data`).
  Код игры: `GraveyardKeeper2_Data\Managed\Assembly-CSharp.dll`.
- Загрузчик: **BepInEx 5.4.23.5 x64** (для Mono). Сейчас BepInEx НЕ установлен.
- Фреймворк: **GK2 Mod Framework** — BepInEx-плагин (`BepInEx/plugins/GK2.Framework.dll`),
  даёт меню Mods, настройки, зависимости, lifecycle. Лицензия MIT.
- Встроенная система модов игры (языки, озвучка) работает **без BepInEx** (папка
  `%LocalLow%\Lazy Bear Games\Graveyard Keeper 2\Mods\`), но код-моды — нет.
- В игре есть встроенный **Workshop Creator** (Shift+F11, флаг `workshopCreatorMode`),
  умеющий публиковать произвольные папки — используется в фазе 2 (вне этой спеки).

## 3. Объём

**Фаза 1 (эта спека): GUI-инсталлятор** BepInEx + GK2 Mod Framework, офлайн (архивы вшиты).

**Вне объёма (фаза 2 и позже):** автозагрузчик Workshop-модов, авто-обновление, менеджер/список
модов, установка конкретных код-модов, публикация в Workshop.

## 4. Технологии

- C# **.NET Framework 4.8**, WinForms, SDK-style проект:
  `<TargetFramework>net48</TargetFramework>`, `<UseWindowsForms>true</UseWindowsForms>`,
  `<OutputType>WinExe</OutputType>`.
- Сборка локальным SDK: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release`.
- Один `.exe`, внешний рантайм не нужен (4.8 входит в Win10/11).
- UI строится кодом (без визуального дизайнера).
- Вшитые ресурсы (`EmbeddedResource`): `BepInEx_win_x64_5.4.23.5.zip`, `GK2.Framework.zip`.
- Без внешних NuGet: мини-парсер `libraryfolders.vdf` пишем сами.

## 5. UX / поток

Один экран:

- Заголовок: «Graveyard Keeper 2 — установка модов».
- «Папка игры:» — текстовое поле (read-only) + кнопка **«Обзор…»** + статус (✓/✗).
- Галочки: ☑ **BepInEx 5.4.23.5**, ☑ **GK2 Mod Framework**, ☐ **Бэкап существующего BepInEx**.
- Кнопка **«Установить»** (главная), кнопка **«Удалить BepInEx»**, ссылка на страницу проекта
  (GitHub) с гайдом.
- Область лога (многострочный текст) + `ProgressBar`.
- Статус-строка внизу.

Поток «Установить»:
1. Определить/подтвердить папку игры.
2. Проверки (§6).
3. Бэкап (если включён и есть существующий `BepInEx\`).
4. Распаковать BepInEx zip → корень игры.
5. Распаковать фреймворк в корень игры (архив раскладывает `BepInEx\plugins\GK2.Framework.dll`
   + `BepInEx\plugins\GK2.Framework\`).
6. Верификация (§6).
7. Успех + инструкция: запустить игру один раз → в главном меню появится **Mods** → затем
   ставить моды из Workshop.

## 6. Логика (модули)

### GameLocator
- Поиск через реестр: `HKCU\Software\Valve\Steam\SteamPath` → `steamapps\libraryfolders.vdf`
  (все библиотеки) → `<lib>\steamapps\common\Graveyard Keeper 2`.
- Fallback: ручной выбор (`GraveyardKeeper2.exe` либо папка).
- Валидация папки: есть `GraveyardKeeper2.exe`, `UnityPlayer.dll`,
  `GraveyardKeeper2_Data\Managed\Assembly-CSharp.dll`.

### BepInExInstaller
- `Install(gameDir, options)`:
  - Проверки: игра не запущена (`Process.GetProcessesByName("GraveyardKeeper2").Length == 0`);
    папка валидна; право на запись (создать/удалить пробный файл).
  - Бэкап (опц.): `BepInEx\` → `BepInEx_backup_<yyyyMMdd_HHmmss>\`.
  - Распаковка вшитого BepInEx zip в `gameDir` (перезапись).
  - Распаковка фреймворка в `gameDir` (архив содержит путь `BepInEx/plugins/...`).
- `Verify(gameDir)` → `bool` + список проблем. Проверяет: `winhttp.dll`,
  `doorstop_config.ini`, `BepInEx\core\` (непустая), `BepInEx\plugins\GK2.Framework.dll`.
- `Uninstall(gameDir)` → удаляет ТОЛЬКО: папку `BepInEx\`, `winhttp.dll`,
  `doorstop_config.ini`, `.doorstop_version`, `changelog.txt`. Перед удалением — подтверждение в UI.

### Manifest
- `InstallerManifest` — версии BepInEx/фреймворка (для отображения и верификации).

## 7. Вшитые ресурсы и лицензии

- **BepInEx 5.4.23.5 x64** — LGPL-2.0, распространяется без изменений; в комплект — `LICENSE.BepInEx.txt`.
- **GK2 Mod Framework** — MIT; в комплект — `LICENSE.GK2Framework.txt`.
- Источники: официальные GitHub-релизы BepInEx и GK2 Mod Framework.

## 8. Тестирование

- **Ручной E2E** на локальной установке: поставить в `E:\SteamLibrary\...\Graveyard Keeper 2`;
  проверить файлы; запустить игру; в `BepInEx\LogOutput.log` найти старт BepInEx и
  `GK2_FRAMEWORK_READY`; в главном меню — кнопка **Mods**.
- **Идемпотентность:** повторный запуск не ломает установку.
- **Удаление** и **бэкап** — отдельные проверки.
- **Юнит-тесты** (xUnit, net48): `GameLocator` (парсинг vdf на фикстурах), `BepInExInstaller.Verify`
  (на временных папках) — без реальной игры.

## 9. Распространение

- **GitHub Releases:** `GK2ModInstaller_vX.Y.Z.zip` (exe + README + лицензии).
- **Nexus Mods** — зеркало.
- Steam Workshop — позже, вместе с автозагрузчиком (фаза 2).

## 10. Риски / ограничения

- Папка игры в `Program Files` → может понадобиться админ (UAC-манифест). Steam Library обычно вне него.
- Антивирусы могут ложно срабатывать на `winhttp.dll`/Doorstop-инъекцию — предупредить в README.
- Игра должна быть закрыта во время установки.
- Обновление BepInEx/фреймворка — перевыпуском инсталлятора с новой вшитой версией.
- net48 WinForms, UI кодом (без дизайнера).

## 11. Фаза 2 (для контекста, не эта спека)

Автозагрузчик: BepInEx-плагин, сканирует `steamapps\workshop\content\4358690\*`, подхватывает
DLL код-модов (в `BepInEx\plugins` или напрямую), публикуется в Steam Workshop через встроенный
Creator. Спека — отдельным документом.
