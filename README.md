# GK2 Mod Installer

Установщик модов для **Graveyard Keeper 2**: в пару кликов ставит **BepInEx 5.4.23.5 x64** и
**GK2 Mod Framework** в папку игры (офлайн, архивы вшиты в exe).

## Что делает

- Находит папку игры автоматически (через Steam) или даёт выбрать вручную.
- Ставит BepInEx (загрузчик модов) и GK2 Mod Framework (внутриигровое меню **Mods**).
- **Автозагрузка Workshop-модов:** патчер `GK2.WorkshopAutoLoader` сам подхватывает код-моды из
  подписанных Steam Workshop-айтемов (раскладывает их в `BepInEx\plugins\_Workshop\`) — без
  ручного копирования. Можно отключить галочкой в установщике.
- **Согласие на моды (v1.2.0):** ничего не запускается без вашего подтверждения — диалог
  Yes/No/Cancel для новых модов и обновлений, предупреждения код-чека и дубликатов.
- **Моды в папку игры (v1.2.0):** если в айтеме лежит `CopyToGameFolder` (или `GraveyardKeeper2_Data`
  / `Languages` в корне), файлы кладутся в папку игры под тем же согласием — с бэкапом оригинала
  и манифестом, поэтому при отписке/запрете всё аккуратно откатывается.
- Умеет удалять BepInEx (только свои файлы, игру не трогает).
- Опционально делает бэкап существующей папки `BepInEx`.

## Согласие на моды

Автозагрузчик не запускает мод без вашего решения:

- **Новый мод** — диалог: `Yes` одобрить, `No` заблокировать навсегда, `Cancel` спросить позже.
- **Обновление мода** — тоже спрашивает; при `Cancel` продолжает работать прежняя одобренная версия.
- **Миграция** — моды, поставленные прежними версиями автозагрузчика, один раз спросят сводно.
- **Проверка кода** — перед решением показываем, использует ли мод сеть, запуск процессов,
  удаление файлов, загрузку кода, реестр или Win32. Это подсказка, а не гарантия.
- **Дубликаты** — предупреждаем, если та же сборка стоит ещё и вручную в `BepInEx\plugins`.

Решения хранятся в `BepInEx\config\GK2_WorkshopLoader.trust.txt` (строки `id|sha256|yes|no|ask|...`);
удалите строку — мод спросят заново. Моды, ожидающие решения, перечислены в
`BepInEx\config\GK2_WorkshopLoader.pending.txt`. Если диалога не видно — нажмите Alt+Tab
(окно могло уйти за игру).

Моды «в папку игры» дополнительно держат бэкап перезаписанных файлов в
`BepInEx\config\GK2_WorkshopLoader.backup\<id>\` (там же `manifest.txt`); при отписке или запрете
оригиналы возвращаются на место, а созданные нами файлы удаляются.

## Требования

- Windows x64, Graveyard Keeper 2 (Steam).
- .NET Framework 4.8 (входит в Windows 10/11).

## Установка

1. Закройте Graveyard Keeper 2.
2. Запустите `GK2ModInstaller.exe`.
3. Проверьте папку игры (обычно определяется сама) и нажмите **Установить**.
4. Запустите игру один раз — в главном меню появится кнопка **Mods**.

## Удаление

- Кнопка **Удалить BepInEx** в установщике, **или**
- вручную удалить из папки игры: `BepInEx\`, `winhttp.dll`, `doorstop_config.ini`,
  `.doorstop_version`, `changelog.txt`.

## Headless-режим (для скриптов)

```
GK2ModInstaller.exe --install   "<путь к игре>"
GK2ModInstaller.exe --uninstall "<путь к игре>"
```

## Антивирус

BepInEx использует `winhttp.dll` (Doorstop) для загрузки в игру — некоторые антивирусы могут
дать ложное срабатывание. Это стандартный механизм BepInEx, а не вирус.

## Лицензии

- BepInEx — LGPL-2.0 (распространяется без изменений).
- GK2 Mod Framework — MIT.

---

# GK2 Mod Installer (EN)

One-click installer for **Graveyard Keeper 2** mods: installs **BepInEx 5.4.23.5 x64** and
**GK2 Mod Framework** into the game folder (offline; archives are embedded in the exe).

- Auto-detects the game folder (via Steam) or lets you pick it manually.
- Installs BepInEx (mod loader) and GK2 Mod Framework (in-game **Mods** menu).
- **Workshop mod auto-loading:** the `GK2.WorkshopAutoLoader` patcher picks up code mods from
  subscribed Steam Workshop items automatically (stages them into `BepInEx\plugins\_Workshop\`),
  no manual copying. Can be disabled with a checkbox in the installer.
- **Mod consent (v1.2.0):** nothing runs without your approval — a Yes/No/Cancel dialog for new
  mods and updates, plus code-check and duplicate warnings. Decisions live in
  `BepInEx\config\GK2_WorkshopLoader.trust.txt` (delete a line to be asked again); postponed mods
  are listed in `GK2_WorkshopLoader.pending.txt`. If no dialog appears, press Alt+Tab.
- **Game-folder mods (v1.2.0):** items shipping `CopyToGameFolder` (or `GraveyardKeeper2_Data` /
  `Languages` at the item root) are copied into the game folder under the same consent, with a
  backup of overwritten files and a manifest (`BepInEx\config\GK2_WorkshopLoader.backup\<id>\`),
  so unsubscribing or denying rolls the game folder back.
- Can uninstall BepInEx (its own files only).
- Optional backup of the existing `BepInEx` folder.

Requirements: Windows x64, Graveyard Keeper 2 (Steam), .NET Framework 4.8.

Install: close the game, run `GK2ModInstaller.exe`, click **Install**, launch the game once — a
**Mods** button appears in the main menu.

Headless: `GK2ModInstaller.exe --install "<game path>"` / `--uninstall "<game path>"`.

Licenses: BepInEx (LGPL-2.0), GK2 Mod Framework (MIT).
