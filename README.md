# GK2 Mod Installer

Установщик модов для **Graveyard Keeper 2**: в пару кликов ставит **BepInEx 5.4.23.5 x64** и
**GK2 Mod Framework** в папку игры (офлайн, архивы вшиты в exe).

## Что делает

- Находит папку игры автоматически (через Steam) или даёт выбрать вручную.
- Ставит BepInEx (загрузчик модов) и GK2 Mod Framework (внутриигровое меню **Mods**).
- Умеет удалять BepInEx (только свои файлы, игру не трогает).
- Опционально делает бэкап существующей папки `BepInEx`.

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
- Can uninstall BepInEx (its own files only).
- Optional backup of the existing `BepInEx` folder.

Requirements: Windows x64, Graveyard Keeper 2 (Steam), .NET Framework 4.8.

Install: close the game, run `GK2ModInstaller.exe`, click **Install**, launch the game once — a
**Mods** button appears in the main menu.

Headless: `GK2ModInstaller.exe --install "<game path>"` / `--uninstall "<game path>"`.

Licenses: BepInEx (LGPL-2.0), GK2 Mod Framework (MIT).
