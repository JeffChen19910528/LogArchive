namespace LogBackup.CLI.Localization;

public static class LanguageResolution
{
    /// <summary>
    /// Applies the --lang value parsed for this invocation. Falls back to whatever
    /// Program.ResolveStartupLanguage already set (from LOGBACKUP_LANG or config ui.language)
    /// when --lang was not passed on this command.
    /// </summary>
    public static void Apply(string? langOptionValue)
    {
        if (Strings.Parse(langOptionValue) is { } lang)
        {
            Strings.Current = lang;
        }
    }
}
