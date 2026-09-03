using System.CommandLine;
using LogBackup.CLI.Localization;

namespace LogBackup.CLI;

/// <summary>
/// Arrow-key, highlighted-selection front end for people who launch logbackup.exe by
/// double-clicking it (or by typing the bare command with no arguments) rather than knowing
/// the CLI syntax. Each menu choice is translated into the same argv a command-line invocation
/// would use and run through the existing <see cref="RootCommand"/>, so there is exactly one
/// implementation of every command's behavior - this is only an input/output layer on top of it.
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
            var choice = SelectFromMenu(actions, selectedIndex);
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
            var argv = BuildArgv(action);
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
    /// Renders the menu with the current item highlighted (inverted colors) and reads
    /// Up/Down/Enter/Esc directly via ReadKey - no typing a number, no pressing Enter to submit
    /// a line. Redraws only the two changed rows on each arrow press to avoid flicker/scrolling.
    /// </summary>
    private static int? SelectFromMenu(Action[] actions, int initialIndex)
    {
        Console.Clear();
        Console.WriteLine(Strings.T("menu.title"));
        Console.WriteLine(new string('=', VisualWidth(Strings.T("menu.title"))));
        Console.WriteLine(Strings.T("menu.hint"));
        Console.WriteLine(Strings.T("menu.nav_hint"));
        Console.WriteLine();

        var top = Console.CursorTop;
        var selected = Math.Clamp(initialIndex, 0, actions.Length - 1);

        for (var i = 0; i < actions.Length; i++)
        {
            DrawRow(actions, i, top + i, highlighted: i == selected);
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            var previous = selected;

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    selected = (selected - 1 + actions.Length) % actions.Length;
                    break;
                case ConsoleKey.DownArrow:
                    selected = (selected + 1) % actions.Length;
                    break;
                case ConsoleKey.Enter:
                    return selected;
                case ConsoleKey.Escape:
                    return null;
                default:
                    continue;
            }

            DrawRow(actions, previous, top + previous, highlighted: false);
            DrawRow(actions, selected, top + selected, highlighted: true);
        }
    }

    private static void DrawRow(Action[] actions, int index, int row, bool highlighted)
    {
        Console.SetCursorPosition(0, row);
        var label = Label(actions[index]);
        var text = $" {label,-50}";

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

    private static string[]? BuildArgv(Action action) => action switch
    {
        Action.Backup => BuildBackup(),
        Action.List => new[] { "list" },
        Action.Info => BuildWithId("info"),
        Action.Verify => BuildWithId("verify"),
        Action.Restore => BuildRestore(),
        Action.Delete => BuildDelete(),
        Action.Retention => BuildRetention(),
        Action.ConfigShow => new[] { "config", "show" },
        Action.ConfigInit => new[] { "config", "init" },
        Action.Audit => BuildAudit(),
        Action.Repair => BuildRepair(),
        _ => null,
    };

    private static string[] BuildBackup()
    {
        var args = new List<string> { "backup" };

        var source = Prompt("menu.prompt.backup_source");
        if (!string.IsNullOrWhiteSpace(source))
        {
            args.Add("--source");
            args.Add(source);
        }

        if (PromptYesNo("menu.prompt.backup_incremental"))
        {
            args.Add("--incremental");
        }

        return args.ToArray();
    }

    private static string[]? BuildWithId(string command)
    {
        var id = Prompt("menu.prompt.id");
        if (string.IsNullOrWhiteSpace(id))
        {
            Console.WriteLine(Strings.T("menu.error.id_required"));
            return null;
        }

        return new[] { command, "--id", id };
    }

    private static string[]? BuildRestore()
    {
        var id = Prompt("menu.prompt.id");
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

        if (PromptYesNo("menu.prompt.restore_overwrite")) args.Add("--overwrite");
        if (PromptYesNo("menu.prompt.restore_force")) args.Add("--force");

        return args.ToArray();
    }

    private static string[]? BuildDelete()
    {
        var id = Prompt("menu.prompt.id");
        if (string.IsNullOrWhiteSpace(id))
        {
            Console.WriteLine(Strings.T("menu.error.id_required"));
            return null;
        }

        var args = new List<string> { "delete", "--id", id };
        if (PromptYesNo("menu.prompt.delete_force")) args.Add("--force");
        // Deliberately never pass --yes: the delete command's own interactive
        // y/N confirmation prompt is exactly what a menu user expects here.
        return args.ToArray();
    }

    private static string[] BuildRetention()
    {
        var args = new List<string> { "retention" };
        if (PromptYesNo("menu.prompt.retention_dry_run")) args.Add("--dry-run");
        return args.ToArray();
    }

    private static string[] BuildAudit()
    {
        var args = new List<string> { "audit" };
        var id = Prompt("menu.prompt.audit_id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            args.Add("--id");
            args.Add(id);
        }
        return args.ToArray();
    }

    private static string[] BuildRepair()
    {
        var args = new List<string> { "repair" };
        if (PromptYesNo("menu.prompt.repair_delete")) args.Add("--delete");
        return args.ToArray();
    }

    private static string Prompt(string key)
    {
        Console.Write(Strings.T(key));
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    private static bool PromptYesNo(string key)
    {
        Console.Write(Strings.T(key));
        var answer = Console.ReadLine()?.Trim();
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
