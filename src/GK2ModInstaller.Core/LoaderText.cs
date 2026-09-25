using System;
using System.Collections.Generic;
using System.Globalization;

namespace GK2ModInstaller.Core
{
    public enum LoaderLanguage { En, Ru }

    // All user-visible strings of the Workshop loader live here. Language is process-wide,
    // set once at startup (auto-detected or from the config file).
    public static class LoaderText
    {
        public static LoaderLanguage Language { get; set; } = LoaderLanguage.En;

        private static readonly Dictionary<string, (string En, string Ru)> Table =
            new Dictionary<string, (string En, string Ru)>(StringComparer.Ordinal)
        {
            // Consent prompt (ModPrompt.Text)
            ["FilesPrefix"] = ("Files: ", "Файлы: "),
            ["TargetPrefix"] = ("Target: ", "Назначение: "),
            ["CodeCheckPrefix"] = ("Code check: ", "Проверка кода: "),
            ["CodeCheckClean"] = ("nothing suspicious found", "ничего подозрительного не найдено"),
            ["DuplicateLine"] = ("The same mod is already in BepInEx\\plugins (installed manually): ", "Тот же мод уже стоит вручную в BepInEx\\plugins: "),
            ["UpdateHint"] = ("If you answer Cancel, the previous approved version will keep working.", "Если ответить Cancel — прежняя одобренная версия продолжит работать."),
            ["ApproveQuestion"] = ("The mod runs with the game's privileges (files, internet). Approve?", "Мод запускается с правами игры (файлы, интернет). Одобрить?"),
            ["ButtonHint"] = ("Yes — approve · No — block forever · Cancel — ask later", "Yes — одобрить · No — заблокировать навсегда · Cancel — спросить позже"),

            // Findings categories
            ["CategoryNetwork"] = ("network", "сеть"),
            ["CategoryProcess"] = ("running programs", "запуск программ"),
            ["CategoryFileDelete"] = ("file deletion/moving", "удаление/перемещение файлов"),
            ["CategoryCodeLoad"] = ("code loading", "загрузка кода"),
            ["CategoryRegistry"] = ("registry", "реестр"),
            ["CategoryNative"] = ("native code (DllImport)", "нативный код (DllImport)"),

            // Loader summary + logs
            ["SummaryFormat"] = ("Workshop: items {0}, approved {1}, updates {2}, blocked {3}, postponed {4}, removed {5}, migrated {6}, warnings {7}.", "Workshop: айтемов {0}, одобрено {1}, обновлений {2}, заблокировано {3}, отложено {4}, удалено {5}, миграция {6}, предупреждений {7}."),
            ["NoOptions"] = ("Workshop: no options — skipping", "Workshop: нет опций — пропуск"),
            ["WorkshopFolderMissing"] = ("Workshop: folder not found — ", "Workshop: папка не найдена — "),
            ["BepInExMissing"] = ("Workshop: BepInEx not found — ", "Workshop: BepInEx не найден — "),
            ["GameFolderUnsupported"] = ("Workshop: game folder not determined — game-folder mods are not supported", "Workshop: папка игры не определена — моды в папку игры не поддерживаются"),
            ["BulkDialogUnavailable"] = ("Workshop: bulk dialog unavailable (", "Workshop: сводный диалог недоступен ("),
            ["NoteMigration"] = ("migration ", "миграция "),
            ["NoteMigrationPostponed"] = ("migration postponed", "миграция отложена"),
            ["MigrationLog"] = ("Workshop: migration of previous installs — {0} item(s), answer: {1}", "Workshop: миграция прежних установок — {0} шт., ответ: {1}"),
            ["ItemError"] = ("Workshop: item {0} — error: {1}", "Workshop: айтем {0} — ошибка: {1}"),
            ["TrustNotSavedFormat"] = ("trust: not saved ({0}) — this run's decisions will be lost", "trust: не сохранён ({0}) — решения этого запуска будут потеряны"),
            ["DialogUnavailablePostponed"] = ("Workshop: dialog unavailable ({0}) — mod postponed: {1}", "Workshop: диалог недоступен ({0}) — мод отложен: {1}"),
            ["GameFolderNotDeterminedSkip"] = ("Workshop: game-folder mod {0} — game folder not determined, skipping", "Workshop: game-folder мод {0} — папка игры не определена, пропуск"),
            ["GameFolderInstalledFiles"] = ("Workshop: game-folder mod {0} — files installed: {1}", "Workshop: game-folder мод {0} — установлено файлов {1}"),
            ["GameFolderInstallFailed"] = ("Workshop: game-folder mod {0} — install failed, previous state kept", "Workshop: game-folder мод {0} — установка не удалась, прежнее состояние сохранено"),
            ["NoteApproved"] = ("approved ", "одобрено "),
            ["NoteUpdated"] = ("updated ", "обновление "),
            ["ApprovedLog"] = ("Workshop: approved mod {0} ({1})", "Workshop: одобрен мод {0} ({1})"),
            ["NoteBlocked"] = ("blocked ", "заблокирован "),
            ["BlockedLog"] = ("Workshop: blocked mod {0}", "Workshop: заблокирован мод {0}"),
            ["NoteUpdatePostponed"] = ("update postponed ", "обновление отложено "),
            ["NotePostponed"] = ("postponed ", "отложено "),
            ["PostponedLog"] = ("Workshop: postponed mod {0}", "Workshop: отложен мод {0}"),
            ["GameFolderRestoredInstall"] = ("Workshop: restored game-folder mod install {0} — files: {1}", "Workshop: восстановлена установка game-folder мода {0} — файлов {1}"),
            ["RestoredApprovedCopy"] = ("Workshop: restored approved mod copy {0}", "Workshop: восстановлена копия одобренного мода {0}"),
            ["BlockedNotLoaded"] = ("Workshop: blocked mod not loaded — {0}", "Workshop: заблокированный мод не грузим — {0}"),
            ["GameFolderUnsubscribed"] = ("Workshop: game-folder mod unsubscribed, files restored — {0}", "Workshop: game-folder мод отписан, файлы возвращены — {0}"),
            ["UnsubscribedCopyDeleted"] = ("Workshop: mod unsubscribed, copy deleted — {0}", "Workshop: мод отписан, копия удалена — {0}"),
            // Game-folder DLLs are loaded in-process, not copied into the game folder.
            ["GameFolderDllLoaded"] = ("game-folder: loaded DLL {0}", "game-folder: загружен DLL {0}"),
            ["GameFolderDllAlreadyLoaded"] = ("game-folder: DLL already loaded, skipping {0}", "game-folder: DLL уже загружен, пропуск {0}"),
            ["GameFolderDllFailed"] = ("game-folder: DLL not loaded — {0} ({1})", "game-folder: DLL не загружен — {0} ({1})"),
            ["GameFolderDllSkipped"] = ("game-folder: DLL stays in the item (loaded in-process) — {0}", "game-folder: DLL оставлен в айтеме (грузится в процессе) — {0}"),
            ["NoteInstallFailed"] = ("install failed ", "установка не удалась "),
            ["TargetGameFolder"] = ("game folder (GraveyardKeeper2_Data\\Managed etc.)", "папка игры (GraveyardKeeper2_Data\\Managed и т.п.)"),
            ["TargetPlugins"] = ("BepInEx\\plugins", "BepInEx\\plugins"),
            ["AcfTimeupdated"] = ("Workshop: ACF — mod {0} timeupdated={1}", "Workshop: ACF — у мода {0} timeupdated={1}"),
            ["AcfNotParsed"] = ("Workshop: ACF not parsed ({0})", "Workshop: ACF не разобран ({0})"),

            // Pending list
            ["PendingHeader"] = ("# GK2 Workshop Loader: mods awaiting a decision. Rules are in GK2_WorkshopLoader.trust.txt.", "# GK2 Workshop Loader: моды, ожидающие решения. Правила — в GK2_WorkshopLoader.trust.txt."),
            ["PendingNotWrittenFormat"] = ("pending: file not written ({0})", "pending: файл не записан ({0})"),

            // Deferred game-folder operations (applied by the installer when the game is closed)
            ["PendingGameHeader"] = ("# GK2 Workshop Loader: game-folder actions applied when the game is closed.", "# GK2 Workshop Loader: действия с папкой игры, применяются при закрытой игре."),
            ["PendingGameSkippedLine"] = ("pending-game: skipped line: ", "pending-game: пропущена строка: "),
            ["PendingGameUnknownKind"] = ("pending-game: unknown action: ", "pending-game: неизвестное действие: "),
            ["PendingGameNotReadFormat"] = ("pending-game: file not read ({0})", "pending-game: файл не прочитан ({0})"),
            ["PendingGameNotWrittenFormat"] = ("pending-game: file not written ({0})", "pending-game: файл не записан ({0})"),
            ["PendingGameQueuedDelete"] = ("pending-game: queued delete — {0}", "pending-game: отложено удаление — {0}"),
            ["PendingGameQueuedRestore"] = ("pending-game: queued restore — {0} (mod {1})", "pending-game: отложен откат — {0} (мод {1})"),
            ["PendingGameApplyStart"] = ("pending-game: applying {0} action(s)", "pending-game: применяю действий {0}"),
            ["PendingGameDeleted"] = ("pending-game: deleted {0}", "pending-game: удалён {0}"),
            ["PendingGameRestored"] = ("pending-game: restored {0} (mod {1})", "pending-game: восстановлен {0} (мод {1})"),
            ["PendingGameDeleteFailed"] = ("pending-game: cannot delete {0} — kept for next time", "pending-game: не удалось удалить {0} — оставлено до следующего раза"),
            ["PendingGameRestoreFailed"] = ("pending-game: cannot restore {0} — kept for next time", "pending-game: не удалось восстановить {0} — оставлено до следующего раза"),
            ["PendingGameDone"] = ("pending-game: done — applied {0}, still pending {1}", "pending-game: готово — применено {0}, осталось {1}"),

            // Trust store
            ["TrustHeader"] = ("# GK2 Workshop Loader: decisions on mods. Delete a line to be asked again.", "# GK2 Workshop Loader: решения по модам. Удалите строку — спросят снова."),
            ["TrustSkippedLine"] = ("trust: skipped line: ", "trust: пропущена строка: "),
            ["TrustSkippedNoId"] = ("trust: skipped line without id: ", "trust: пропущена строка без id: "),
            ["TrustNotReadFormat"] = ("trust: file not read ({0}), starting over", "trust: файл не прочитан ({0}), начинаю заново"),

            // Items
            ["NotAnAssembly"] = ("mod: not an assembly, metadata not read — ", "мод: не сборка, метаданные не прочитаны — "),

            // Win32 dialog
            ["NewModHeader"] = ("New Workshop mod", "Новый мод из Workshop"),
            ["UpdateModHeader"] = ("Update of a Workshop mod", "Обновление мода из Workshop"),
            ["Caption"] = ("GK2 Workshop Loader", "GK2 Workshop Loader"),
            ["BulkIntroFormat"] = ("Found {0} mod(s) installed by a previous version of the auto-loader — without your consent.", "Найдено {0} мод(ов), установленных прежней версией автозагрузчика — без вашего согласия."),
            ["BulkIntro2"] = ("They are already in BepInEx\\plugins\\_Workshop and running. Trust all of them?", "Они уже стоят в BepInEx\\plugins\\_Workshop и работают. Доверять им всем?"),
            ["BulkButtonHint"] = ("Yes — trust all · No — ask about each separately · Cancel — leave as is and ask later", "Yes — доверять всем · No — спросить про каждый отдельно · Cancel — оставить как есть и спросить позже"),

            // Loader startup
            ["StartupFormat"] = ("loader {0} (source: {1})", "загрузчик {0} (источник: {1})")
        };

        public static IReadOnlyDictionary<string, (string En, string Ru)> All => Table;

        public static string Get(string key)
        {
            if (key == null || !Table.TryGetValue(key, out var value))
                throw new KeyNotFoundException("LoaderText key not found: " + (key ?? "<null>"));
            return Language == LoaderLanguage.Ru ? value.Ru : value.En;
        }

        public static LoaderLanguage DetectFor(string twoLetterName)
            => string.Equals(twoLetterName, "ru", StringComparison.OrdinalIgnoreCase)
                ? LoaderLanguage.Ru
                : LoaderLanguage.En;

        public static void AutoDetect()
            => Language = DetectFor(CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName);

        public static string FilesPrefix => Get(nameof(FilesPrefix));
        public static string TargetPrefix => Get(nameof(TargetPrefix));
        public static string CodeCheckPrefix => Get(nameof(CodeCheckPrefix));
        public static string CodeCheckClean => Get(nameof(CodeCheckClean));
        public static string DuplicateLine => Get(nameof(DuplicateLine));
        public static string UpdateHint => Get(nameof(UpdateHint));
        public static string ApproveQuestion => Get(nameof(ApproveQuestion));
        public static string ButtonHint => Get(nameof(ButtonHint));
        public static string CategoryNetwork => Get(nameof(CategoryNetwork));
        public static string CategoryProcess => Get(nameof(CategoryProcess));
        public static string CategoryFileDelete => Get(nameof(CategoryFileDelete));
        public static string CategoryCodeLoad => Get(nameof(CategoryCodeLoad));
        public static string CategoryRegistry => Get(nameof(CategoryRegistry));
        public static string CategoryNative => Get(nameof(CategoryNative));
        public static string SummaryFormat => Get(nameof(SummaryFormat));
        public static string NoOptions => Get(nameof(NoOptions));
        public static string WorkshopFolderMissing => Get(nameof(WorkshopFolderMissing));
        public static string BepInExMissing => Get(nameof(BepInExMissing));
        public static string GameFolderUnsupported => Get(nameof(GameFolderUnsupported));
        public static string BulkDialogUnavailable => Get(nameof(BulkDialogUnavailable));
        public static string NoteMigration => Get(nameof(NoteMigration));
        public static string NoteMigrationPostponed => Get(nameof(NoteMigrationPostponed));
        public static string MigrationLog => Get(nameof(MigrationLog));
        public static string ItemError => Get(nameof(ItemError));
        public static string TrustNotSavedFormat => Get(nameof(TrustNotSavedFormat));
        public static string DialogUnavailablePostponed => Get(nameof(DialogUnavailablePostponed));
        public static string GameFolderNotDeterminedSkip => Get(nameof(GameFolderNotDeterminedSkip));
        public static string GameFolderInstalledFiles => Get(nameof(GameFolderInstalledFiles));
        public static string GameFolderInstallFailed => Get(nameof(GameFolderInstallFailed));
        public static string NoteApproved => Get(nameof(NoteApproved));
        public static string NoteUpdated => Get(nameof(NoteUpdated));
        public static string ApprovedLog => Get(nameof(ApprovedLog));
        public static string NoteBlocked => Get(nameof(NoteBlocked));
        public static string BlockedLog => Get(nameof(BlockedLog));
        public static string NoteUpdatePostponed => Get(nameof(NoteUpdatePostponed));
        public static string NotePostponed => Get(nameof(NotePostponed));
        public static string PostponedLog => Get(nameof(PostponedLog));
        public static string GameFolderRestoredInstall => Get(nameof(GameFolderRestoredInstall));
        public static string RestoredApprovedCopy => Get(nameof(RestoredApprovedCopy));
        public static string BlockedNotLoaded => Get(nameof(BlockedNotLoaded));
        public static string GameFolderUnsubscribed => Get(nameof(GameFolderUnsubscribed));
        public static string UnsubscribedCopyDeleted => Get(nameof(UnsubscribedCopyDeleted));
        public static string GameFolderDllLoaded => Get(nameof(GameFolderDllLoaded));
        public static string GameFolderDllAlreadyLoaded => Get(nameof(GameFolderDllAlreadyLoaded));
        public static string GameFolderDllFailed => Get(nameof(GameFolderDllFailed));
        public static string GameFolderDllSkipped => Get(nameof(GameFolderDllSkipped));
        public static string NoteInstallFailed => Get(nameof(NoteInstallFailed));
        public static string TargetGameFolder => Get(nameof(TargetGameFolder));
        public static string TargetPlugins => Get(nameof(TargetPlugins));
        public static string AcfTimeupdated => Get(nameof(AcfTimeupdated));
        public static string AcfNotParsed => Get(nameof(AcfNotParsed));
        public static string PendingHeader => Get(nameof(PendingHeader));
        public static string PendingNotWrittenFormat => Get(nameof(PendingNotWrittenFormat));
        public static string PendingGameHeader => Get(nameof(PendingGameHeader));
        public static string PendingGameSkippedLine => Get(nameof(PendingGameSkippedLine));
        public static string PendingGameUnknownKind => Get(nameof(PendingGameUnknownKind));
        public static string PendingGameNotReadFormat => Get(nameof(PendingGameNotReadFormat));
        public static string PendingGameNotWrittenFormat => Get(nameof(PendingGameNotWrittenFormat));
        public static string PendingGameQueuedDelete => Get(nameof(PendingGameQueuedDelete));
        public static string PendingGameQueuedRestore => Get(nameof(PendingGameQueuedRestore));
        public static string PendingGameApplyStart => Get(nameof(PendingGameApplyStart));
        public static string PendingGameDeleted => Get(nameof(PendingGameDeleted));
        public static string PendingGameRestored => Get(nameof(PendingGameRestored));
        public static string PendingGameDeleteFailed => Get(nameof(PendingGameDeleteFailed));
        public static string PendingGameRestoreFailed => Get(nameof(PendingGameRestoreFailed));
        public static string PendingGameDone => Get(nameof(PendingGameDone));
        public static string TrustHeader => Get(nameof(TrustHeader));
        public static string TrustSkippedLine => Get(nameof(TrustSkippedLine));
        public static string TrustSkippedNoId => Get(nameof(TrustSkippedNoId));
        public static string TrustNotReadFormat => Get(nameof(TrustNotReadFormat));
        public static string NotAnAssembly => Get(nameof(NotAnAssembly));
        public static string NewModHeader => Get(nameof(NewModHeader));
        public static string UpdateModHeader => Get(nameof(UpdateModHeader));
        public static string Caption => Get(nameof(Caption));
        public static string BulkIntroFormat => Get(nameof(BulkIntroFormat));
        public static string BulkIntro2 => Get(nameof(BulkIntro2));
        public static string BulkButtonHint => Get(nameof(BulkButtonHint));
        public static string StartupFormat => Get(nameof(StartupFormat));
    }
}
