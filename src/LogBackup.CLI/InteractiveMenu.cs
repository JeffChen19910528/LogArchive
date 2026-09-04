using System.CommandLine;
using LogBackup.CLI.Localization;
using LogBackup.Core.Models;

namespace LogBackup.CLI;

/// <summary>
/// Arrow-key, highlighted-selection front end for people who launch logbackup.exe by
/// double-clicking it (or by typing the bare command with no arguments) rather than knowing
/// the CLI syntax. Each menu choice is translated into the same argv a command-line invocation
/// would use and run through the existing <see cref="RootCommand"/>, so there is exactly one
/// implementation of every command's behavior - this is only an input/output layer on top of it.
/// Wherever a value can be chosen from existing state (a backup ID, a configured source path,
/// yes/no) the menu offers an arrow-key list instead of asking the user to type it; free-text
/// entry is reserved for values with no enumerable set (an arbitrary filesystem path).
/// </summary>
public static class InteractiveMenu
{
    private enum Action
    {
        Backup, List, Info, Verify, Restore, Delete, Retention,
        ConfigShow, ConfigInit, Audit, Repair, ToggleLanguage, Exit,
    }

    public static async Task<int> RunAsync(RootCommand root)
    {
        var actions = new[]
        {
            Action.Backup, Action.List, Action.Info, Action.Verify, Action.Restore,
            Action.Delete, Action.Retention, Action.ConfigShow, Action.ConfigInit,
            Action.Audit, Action.Repair, Action.ToggleLanguage, Action.Exit,
        };

        var selectedIndex = 0;

        while (true)
        {
            var choice = Select(
                Strings.T("menu.title"),
                new[] { Strings.T("menu.hint"), Strings.T("menu.nav_hint") },
                actions.Select(Label).ToArray(),
                selectedIndex);

            if (choice is null || actions[choice.Value] == Action.Exit)
            {
                return ExitCode.Success;
            }

            selectedIndex = choice.Value;
            var action = actions[selectedIndex];

            if (action == Action.ToggleLanguage)
            {
                Strings.Current = Strings.Current == UiLanguage.En ? UiLanguage.ZhTw : UiLanguage.En;
                continue;
            }

            Console.Clear();
            var argv = await BuildArgv(action);
            if (argv is null)
            {
                Console.WriteLine(Strings.T("menu.prompt.invalid"));
            }
            else
            {
                await root.InvokeAsync(argv);
            }

            Console.WriteLine();
            Console.Write(Strings.T("menu.press_enter_to_continue"));
            Console.ReadLine();
        }
    }

    /// <summary>
    /// Renders a titled list with the current item highlighted (inverted colors) and reads
    /// Up/Down/Enter/Esc directly via ReadKey - no typing a number, no pressing Enter to submit
    /// a line. Redraws only the two changed rows on each arrow press to avoid flicker/scrolling.
    /// This is the one selection primitive every menu, yes/no, and pick-from-list prompt below
    /// is built on, so there is a single place that owns keyboard handling and rendering.
    /// </summary>
    private static int? Select(string title, string[] hintLines, string[] labels, int initialIndex)
    {
        Console.Clear();
        Console.WriteLine(title);
        Console.WriteLine(new string('=', VisualWidth(title)));
        foreach (var hint in hintLines)
        {
            Console.WriteLine(hint);
        }
        Console.WriteLine();

        var top = Console.CursorTop;
        var selected = Math.Clamp(initialIndex, 0, labels.Length - 1);

        for (var i = 0; i < labels.Length; i++)
        {
            DrawRow(labels, i, top + i, highlighted: i == selected);
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            var previous = selected;

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    selected = (selected - 1 + labels.Length) % labels.Length;
                    break;
                case ConsoleKey.DownArrow:
                    selected = (selected + 1) % labels.Length;
                    break;
                case ConsoleKey.Enter:
                    return selected;
                case ConsoleKey.Escape:
                    return null;
                default:
                    continue;
            }

            DrawRow(labels, previous, top + previous, highlighted: false);
            DrawRow(labels, selected, top + selected, highlighted: true);
        }
    }

    private static void DrawRow(string[] labels, int index, int row, bool highlighted)
    {
        Console.SetCursorPosition(0, row);
        var text = $" {labels[index],-50}";

        if (highlighted)
        {
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = ConsoleColor.Black;
        }

        Console.Write(text);
        Console.ResetColor();
    }

    private static string Label(Action action) => action switch
    {
        Action.Backup => Strings.T("menu.item.backup"),
        Action.List => Strings.T("menu.item.list"),
        Action.Info => Strings.T("menu.item.info"),
        Action.Verify => Strings.T("menu.item.verify"),
        Action.Restore => Strings.T("menu.item.restore"),
        Action.Delete => Strings.T("menu.item.delete"),
        Action.Retention => Strings.T("menu.item.retention"),
        Action.ConfigShow => Strings.T("menu.item.config_show"),
        Action.ConfigInit => Strings.T("menu.item.config_init"),
        Action.Audit => Strings.T("menu.item.audit"),
        Action.Repair => Strings.T("menu.item.repair"),
        Action.ToggleLanguage => string.Format(
            Strings.T("menu.item.language"),
            Strings.Current == UiLanguage.En ? Strings.T("menu.lang.en") : Strings.T("menu.lang.zhtw")),
        Action.Exit => Strings.T("menu.item.exit"),
        _ => action.ToString(),
    };

    // Console columns count CJK characters as 2 cells; Strings.Current is fixed per app run
    // by the time this renders, so a flat per-character estimate is enough for the title rule.
    private static int VisualWidth(string text) =>
        text.Sum(c => c > 0x2E80 ? 2 : 1);

    private static async Task<string[]?> BuildArgv(Action action) => action switch
    {
        Action.Backup => await BuildBackup(),
        Action.List => new[] { "list" },
        Action.Info => await BuildWithId("info"),
        Action.Verify => await BuildWithId("verify"),
        Action.Restore => await BuildRestore(),
        Action.Delete => await BuildDelete(),
        Action.Retention => BuildRetention(),
        Action.ConfigShow => new[] { "config", "show" },
        Action.ConfigInit => new[] { "config", "init" },
        Action.Audit => await BuildAudit(),
        Action.Repair => BuildRepair(),
        _ => null,
    };

    /// <summary>
    /// Backups known to the metadata store, newest first, for picking a --id by arrow keys
    /// instead of typing one. Returns empty (never throws) if the config/database aren't set
    /// up yet, so callers can fall back to manual entry.
    /// </summary>
    private static async Task<IReadOnlyList<BackupMetadata>> LoadBackupsAsync()
    {
        try
        {
            var app = AppServices.Create(null);
            var backups = await app.MetadataStore.ListAsync();
            return backups.OrderByDescending(b => b.CreatedAtUtc).ToList();
        }
        catch
        {
            return Array.Empty<BackupMetadata>();
        }
    }

    private static List<BackupSourceConfig> LoadConfiguredSources()
    {
        try
        {
            return AppServices.Create(null).Config.Backup.Sources;
        }
        catch
        {
            return new List<BackupSourceConfig>();
        }
    }

    private static string BackupRowLabel(BackupMetadata b) =>
        $"{b.BackupId}  {b.CreatedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm}  {b.Status}";

    /// <summary>
    /// Lets the user pick a backup ID from the known list (arrow keys) instead of typing it;
    /// always keeps a manual-entry option at the end for an ID the local index doesn't have yet.
    /// Returns null if the user cancels.
    /// </summary>
    private static async Task<string?> SelectBackupId(string titleKey)
    {
        var backups = await LoadBackupsAsync();
        var manualLabel = Strings.T("menu.item.manual_entry");

        if (backups.Count == 0)
        {
            Console.WriteLine(Strings.T("menu.info.no_backups"));
            return PromptOrNull("menu.prompt.id");
        }

        var labels = backups.Select(BackupRowLabel).Append(manualLabel).ToArray();
        var choice = Select(Strings.T(titleKey), Array.Empty<string>(), labels, 0);
        if (choice is null)
        {
            return null;
        }

        return choice.Value < backups.Count
            ? backups[choice.Value].BackupId
            : PromptOrNull("menu.prompt.id");
    }

    private static string? PromptOrNull(string key)
    {
        var value = Prompt(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static async Task<string[]> BuildBackup()
    {
        var args = new List<string> { "backup" };

        var source = await SelectBackupSource();
        if (!string.IsNullOrWhiteSpace(source))
        {
            args.Add("--source");
            args.Add(source);
        }

        if (SelectYesNo("menu.prompt.backup_incremental"))
        {
            args.Add("--incremental");
        }

        return args.ToArray();
    }

    /// <summary>
    /// Offers the source directories already listed in the config file as pickable options
    /// (so the common case needs no typing at all), plus "use every configured source" and
    /// a manual-entry fallback for a one-off path that isn't in the config.
    /// </summary>
    private static async Task<string?> SelectBackupSource()
    {
        var configured = LoadConfiguredSources();
        var useConfigLabel = Strings.T("menu.item.use_config_sources");
        var manualLabel = Strings.T("menu.item.manual_entry");

        var labels = new List<string> { useConfigLabel };
        labels.AddRange(configured.Select(s => s.Path));
        labels.Add(manualLabel);

        var choice = Select(Strings.T("menu.prompt.backup_source_title"), Array.Empty<string>(), labels.ToArray(), 0);
        if (choice is null || choice.Value == 0)
        {
            return null;
        }

        if (choice.Value == labels.Count - 1)
        {
            return await Task.FromResult(Prompt("menu.prompt.backup_source"));
        }

        return configured[choice.Value - 1].Path;
    }

    private static async Task<string[]?> BuildWithId(string command)
    {
        var id = await SelectBackupId("menu.prompt.id_title");
        if (string.IsNullOrWhiteSpace(id))
        {
            Console.WriteLine(Strings.T("menu.error.id_required"));
            return null;
        }

        return new[] { command, "--id", id };
    }

    private static async Task<string[]?> BuildRestore()
    {
        var id = await SelectBackupId("menu.prompt.id_title");
        if (string.IsNullOrWhiteSpace(id))
        {
            Console.WriteLine(Strings.T("menu.error.id_required"));
            return null;
        }

        var args = new List<string> { "restore", "--id", id };

        var output = Prompt("menu.prompt.restore_output");
        if (!string.IsNullOrWhiteSpace(output))
        {
            args.Add("--output");
            args.Add(output);
        }

        if (SelectYesNo("menu.prompt.restore_overwrite")) args.Add("--overwrite");
        if (SelectYesNo("menu.prompt.restore_force")) args.Add("--force");

        return args.ToArray();
    }

    private static async Task<string[]?> BuildDelete()
    {
        var id = await SelectBackupId("menu.prompt.id_title");
        if (string.IsNullOrWhiteSpace(id))
        {
            Console.WriteLine(Strings.T("menu.error.id_required"));
            return null;
        }

        var args = new List<string> { "delete", "--id", id };
        if (SelectYesNo("menu.prompt.delete_force")) args.Add("--force");
        // Deliberately never pass --yes: the delete command's own interactive
        // y/N confirmation prompt is exactly what a menu user expects here.
        return args.ToArray();
    }

    private static string[] BuildRetention()
    {
        var args = new List<string> { "retention" };
        if (SelectYesNo("menu.prompt.retention_dry_run")) args.Add("--dry-run");
        return args.ToArray();
    }

    private static async Task<string[]> BuildAudit()
    {
        var args = new List<string> { "audit" };

        var backups = await LoadBackupsAsync();
        if (backups.Count > 0)
        {
            var allLabel = Strings.T("menu.item.audit_all");
            var manualLabel = Strings.T("menu.item.manual_entry");
            var labels = new List<string> { allLabel };
            labels.AddRange(backups.Select(BackupRowLabel));
            labels.Add(manualLabel);

            var choice = Select(Strings.T("menu.prompt.audit_id_title"), Array.Empty<string>(), labels.ToArray(), 0);
            if (choice is not null && choice.Value != 0)
            {
                var id = choice.Value == labels.Count - 1
                    ? Prompt("menu.prompt.audit_id")
                    : backups[choice.Value - 1].BackupId;

                if (!string.IsNullOrWhiteSpace(id))
                {
                    args.Add("--id");
                    args.Add(id);
                }
            }
        }
        else
        {
            var id = Prompt("menu.prompt.audit_id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                args.Add("--id");
                args.Add(id);
            }
        }

        return args.ToArray();
    }

    private static string[] BuildRepair()
    {
        var args = new List<string> { "repair" };
        if (SelectYesNo("menu.prompt.repair_delete")) args.Add("--delete");
        return args.ToArray();
    }

    private static string Prompt(string key)
    {
        Console.Write(Strings.T(key));
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Arrow-key Yes/No picker replacing the old "type y/N and press Enter" prompts.
    /// "No" is pre-selected to match the [y/N] default every one of these prompts documented.
    /// </summary>
    private static bool SelectYesNo(string key)
    {
        var labels = new[] { Strings.T("menu.yes"), Strings.T("menu.no") };
        var choice = Select(Strings.T(key), Array.Empty<string>(), labels, initialIndex: 1);
        return choice == 0;
    }
}
