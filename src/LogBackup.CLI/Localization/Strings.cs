namespace LogBackup.CLI.Localization;

/// <summary>
/// Minimal UI localization: a flat key -> (en, zh-TW) table. Language is resolved once at
/// startup (see Program.cs) from --lang, LOGBACKUP_LANG, or config.ui.language, and stored in
/// Current for the rest of the process. Use T("key") for fixed labels/messages, and
/// string.Format(T("key"), value) for lines with one substituted value.
/// </summary>
public static class Strings
{
    public static UiLanguage Current { get; set; } = UiLanguage.En;

    public static UiLanguage? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "en" or "en-us" or "english" => UiLanguage.En,
            "zh-tw" or "zh_tw" or "zh-hant" or "zhtw" or "zh" or "chinese" or "繁體中文" or "中文" => UiLanguage.ZhTw,
            _ => null,
        };
    }

    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var pair))
        {
            return key;
        }
        return Current == UiLanguage.ZhTw ? pair.ZhTw : pair.En;
    }

    private static readonly Dictionary<string, (string En, string ZhTw)> Table = new()
    {
        // Root / global
        ["root.description"] = ("Cross-platform log backup, encryption, integrity, and restoration tool.", "跨平台日誌備份、加密、完整性驗證與還原工具。"),
        ["option.config"] = ("Path to the logbackup.yaml configuration file (default: ./config/logbackup.yaml)", "logbackup.yaml 設定檔路徑（預設：./config/logbackup.yaml）"),
        ["option.lang"] = ("UI language: en or zh-TW (also LOGBACKUP_LANG env var, or ui.language in config)", "介面語言：en 或 zh-TW（也可用 LOGBACKUP_LANG 環境變數，或設定檔中的 ui.language）"),
        ["root.press_any_key"] = ("Press any key to close this window...", "請按任意鍵關閉此視窗..."),

        // interactive menu
        ["menu.title"] = ("LogBackup - Interactive Menu", "LogBackup 互動選單"),
        ["menu.hint"] = ("You can also close this window and run logbackup.exe with command-line arguments instead.", "你也可以改用命令列參數執行 logbackup.exe。"),
        ["menu.nav_hint"] = ("↑↓ move   Enter select   Esc exit", "↑↓ 移動   Enter 選擇   Esc 離開"),
        ["menu.item.backup"] = ("Back up configured log sources", "備份已設定的日誌來源"),
        ["menu.item.list"] = ("List known backups", "列出所有已知的備份"),
        ["menu.item.info"] = ("Show details for one backup", "查看單一備份的詳細資訊"),
        ["menu.item.verify"] = ("Verify a backup's integrity", "驗證備份的完整性"),
        ["menu.item.restore"] = ("Restore a backup", "還原備份"),
        ["menu.item.delete"] = ("Delete a backup", "刪除備份"),
        ["menu.item.retention"] = ("Apply retention policy", "套用保留策略"),
        ["menu.item.config_show"] = ("Show current configuration", "顯示目前設定"),
        ["menu.item.config_init"] = ("Initialize configuration file", "初始化設定檔"),
        ["menu.item.audit"] = ("Read audit records", "查看稽核紀錄"),
        ["menu.item.repair"] = ("Repair (clean up incomplete backups)", "修復（清理未完成的備份）"),
        ["menu.item.language"] = ("Language: {0}", "語言：{0}"),
        ["menu.lang.en"] = ("English", "English"),
        ["menu.lang.zhtw"] = ("繁體中文", "繁體中文"),
        ["menu.item.exit"] = ("Exit", "離開"),
        ["menu.prompt.invalid"] = ("Invalid choice, please try again.", "選項無效，請重新輸入。"),
        ["menu.prompt.backup_source"] = ("Source directory to back up (leave blank to use the config file): ", "要備份的來源目錄（留空使用設定檔）："),
        ["menu.prompt.backup_incremental"] = ("Incremental backup (only changed files)? [y/N]: ", "是否增量備份（只備份變更過的檔案）？[y/N]："),
        ["menu.prompt.id"] = ("Backup ID: ", "備份 ID："),
        ["menu.prompt.restore_output"] = ("Restore destination directory (leave blank for default): ", "還原目的地目錄（留空使用預設）："),
        ["menu.prompt.restore_overwrite"] = ("Overwrite existing files at the destination? [y/N]: ", "是否覆蓋目的地已存在的檔案？[y/N]："),
        ["menu.prompt.restore_force"] = ("Restore even if hash verification fails? [y/N]: ", "即使雜湊驗證失敗仍要還原嗎？[y/N]："),
        ["menu.prompt.delete_force"] = ("Delete even if the backup is locked? [y/N]: ", "即使備份被鎖定仍要刪除嗎？[y/N]："),
        ["menu.prompt.retention_dry_run"] = ("Dry run only (don't actually delete anything)? [y/N]: ", "僅試跑（不會實際刪除任何項目）嗎？[y/N]："),
        ["menu.prompt.audit_id"] = ("Filter by backup ID (leave blank to show all): ", "依備份 ID 篩選（留空顯示全部）："),
        ["menu.prompt.repair_delete"] = ("Delete incomplete backup artifacts? [y/N]: ", "要刪除未完成的備份檔嗎？[y/N]："),
        ["menu.error.id_required"] = ("A backup ID is required.", "必須輸入備份 ID。"),
        ["menu.press_enter_to_continue"] = ("Press Enter to return to the menu...", "按 Enter 鍵返回選單..."),

        // backup
        ["backup.description"] = ("Back up configured log sources.", "備份已設定的日誌來源。"),
        ["backup.option.source"] = ("Override the configured source directory for this run.", "本次執行覆蓋設定檔中的來源目錄。"),
        ["backup.option.incremental"] = ("Only back up files changed since the last verified backup.", "只備份自上次已驗證備份以來變更過的檔案。"),
        ["backup.error.no_sources"] = ("No backup sources configured. Add one under backup.sources in the config file, or pass --source.", "未設定任何備份來源。請在設定檔的 backup.sources 加入來源，或使用 --source 指定。"),
        ["backup.label.id"] = ("Backup ID:        {0}", "備份 ID：          {0}"),
        ["backup.label.source"] = ("Source:            {0}", "來源：              {0}"),
        ["backup.label.mode"] = ("Mode:              {0}", "模式：              {0}"),
        ["backup.label.files_backed_up"] = ("Files backed up:   {0}", "已備份檔案數：      {0}"),
        ["backup.label.files_skipped"] = ("Files skipped:     {0} (see audit log)", "已跳過檔案數：      {0}（詳見稽核日誌）"),
        ["backup.label.original_size"] = ("Original size:     {0:N0} bytes", "原始大小：          {0:N0} 位元組"),
        ["backup.label.encrypted_size"] = ("Encrypted size:    {0:N0} bytes", "加密後大小：        {0:N0} 位元組"),
        ["backup.label.encryption"] = ("Encryption:        {0}", "加密演算法：        {0}"),
        ["backup.label.hash"] = ("Hash ({0}): {1}", "雜湊值（{0}）： {1}"),
        ["backup.label.status"] = ("Status:            {0}", "狀態：              {0}"),
        ["backup.error.key_access"] = ("Key access error: {0}", "金鑰存取錯誤：{0}"),
        ["backup.error.failed"] = ("Backup failed for '{0}': {1}", "備份失敗（來源：'{0}'）：{1}"),

        // list
        ["list.description"] = ("List known backups.", "列出所有已知的備份。"),
        ["list.empty"] = ("No backups found.", "找不到任何備份。"),
        ["list.header.id"] = ("ID", "備份 ID"),
        ["list.header.date"] = ("Date", "日期"),
        ["list.header.size"] = ("Size", "大小"),
        ["list.header.status"] = ("Status", "狀態"),

        // info
        ["info.description"] = ("Show detailed metadata for one backup.", "顯示單一備份的詳細中繼資料。"),
        ["info.option.id"] = ("Backup ID to show details for.", "要查詢的備份 ID。"),
        ["info.error.not_found"] = ("Backup not found: {0}", "找不到備份：{0}"),
        ["info.label.id"] = ("Backup ID:          {0}", "備份 ID：            {0}"),
        ["info.label.source"] = ("Source:              {0}", "來源：                {0}"),
        ["info.label.created"] = ("Created (UTC):       {0:O}", "建立時間 (UTC)：     {0:O}"),
        ["info.label.platform"] = ("Platform:            {0}", "平台：                {0}"),
        ["info.label.hostname"] = ("Hostname:            {0}", "主機名稱：            {0}"),
        ["info.label.mode"] = ("Mode:                {0}", "模式：                {0}"),
        ["info.label.file_count"] = ("File count:          {0}", "檔案數：              {0}"),
        ["info.label.original_size"] = ("Original size:       {0:N0} bytes", "原始大小：            {0:N0} 位元組"),
        ["info.label.compressed_size"] = ("Compressed size:     {0:N0} bytes", "壓縮後大小：          {0:N0} 位元組"),
        ["info.label.encrypted_size"] = ("Encrypted size:      {0:N0} bytes", "加密後大小：          {0:N0} 位元組"),
        ["info.label.encryption"] = ("Encryption:          {0}", "加密演算法：          {0}"),
        ["info.label.key_id"] = ("Key ID:              {0}", "金鑰 ID：             {0}"),
        ["info.label.hash_algorithm"] = ("Hash algorithm:      {0}", "雜湊演算法：          {0}"),
        ["info.label.hash"] = ("Hash:                {0}", "雜湊值：              {0}"),
        ["info.label.status"] = ("Status:              {0}", "狀態：                {0}"),
        ["info.label.artifact"] = ("Artifact:            {0}", "備份檔案：            {0}"),
        ["info.label.previous"] = ("Previous backup:     {0}", "前次備份：            {0}"),

        // verify
        ["verify.description"] = ("Recompute and compare the hash of a stored backup artifact.", "重新計算並比對已存備份檔的雜湊值。"),
        ["verify.option.id"] = ("Backup ID to verify.", "要驗證的備份 ID。"),
        ["verify.error.not_found"] = ("Backup not found: {0}", "找不到備份：{0}"),
        ["verify.error.missing_artifact"] = ("Backup artifact is missing on disk: {0}", "磁碟上找不到備份檔：{0}"),
        ["verify.label.backup"] = ("Backup:            {0}", "備份：              {0}"),
        ["verify.label.hash_algorithm"] = ("Hash Algorithm:    {0}", "雜湊演算法：        {0}"),
        ["verify.label.expected"] = ("Expected:          {0}", "預期值：            {0}"),
        ["verify.label.actual"] = ("Actual:            {0}", "實際值：            {0}"),
        ["verify.label.integrity"] = ("Integrity:         {0}", "完整性：            {0}"),
        ["verify.pass"] = ("PASS", "通過"),
        ["verify.fail"] = ("FAIL", "失敗"),

        // restore
        ["restore.description"] = ("Verify integrity, decrypt, decompress, and restore a backup to plaintext.", "驗證完整性、解密、解壓縮，並將備份還原為明文。"),
        ["restore.option.id"] = ("Backup ID to restore.", "要還原的備份 ID。"),
        ["restore.option.output"] = ("Destination directory (default: restore.default_directory/<id>).", "還原目的地目錄（預設：restore.default_directory/<id>）。"),
        ["restore.option.overwrite"] = ("Allow overwriting existing files at the restore destination.", "允許覆蓋還原目的地已存在的檔案。"),
        ["restore.option.force"] = ("Restore even if hash verification fails. Generates a high-priority audit record.", "即使雜湊驗證失敗仍強制還原。此操作會產生高優先權稽核紀錄。"),
        ["restore.completed"] = ("Restore completed.", "還原完成。"),
        ["restore.label.id"] = ("Backup ID:         {0}", "備份 ID：           {0}"),
        ["restore.label.integrity"] = ("Integrity:         {0}", "完整性：            {0}"),
        ["restore.integrity.pass"] = ("PASS", "通過"),
        ["restore.integrity.fail_forced"] = ("FAIL (forced)", "失敗（已強制執行）"),
        ["restore.label.encryption"] = ("Encryption:        {0}", "加密演算法：        {0}"),
        ["restore.label.files_restored"] = ("Files restored:    {0}", "已還原檔案數：      {0}"),
        ["restore.label.directory"] = ("Restore directory: {0}", "還原目錄：          {0}"),

        // delete
        ["delete.description"] = ("Permanently delete a backup.", "永久刪除一筆備份。"),
        ["delete.option.id"] = ("Backup ID to delete.", "要刪除的備份 ID。"),
        ["delete.option.yes"] = ("Skip the confirmation prompt (for automated/non-interactive use).", "略過確認提示（供自動化／非互動情境使用）。"),
        ["delete.option.force"] = ("Delete even if the backup is locked.", "即使備份被鎖定也強制刪除。"),
        ["delete.warning.title"] = ("WARNING:", "警告："),
        ["delete.warning.body"] = ("This operation permanently deletes backup:", "此操作將永久刪除以下備份："),
        ["delete.prompt.continue"] = ("Continue? [y/N] ", "是否繼續？[y/N] "),
        ["delete.cancelled"] = ("Cancelled.", "已取消。"),
        ["delete.success"] = ("Deleted backup {0}.", "已刪除備份 {0}。"),
        ["delete.error.locked"] = (" Pass --force to override.", "請加上 --force 以強制執行。"),

        // retention
        ["retention.description"] = ("Apply the configured retention policy (deletes expired/excess backups).", "套用設定檔中的保留策略（刪除過期或超額的備份）。"),
        ["retention.option.dry_run"] = ("Show what would be deleted without deleting anything.", "僅顯示將被刪除的項目，不實際刪除。"),
        ["retention.error.no_policy"] = ("No retention policy configured (set retention.keep_days and/or retention.keep_count).", "未設定保留策略（請設定 retention.keep_days 及／或 retention.keep_count）。"),
        ["retention.dry_run_notice"] = ("(dry run - no changes will be made)", "（僅預覽，不會實際變更）"),
        ["retention.would_delete"] = ("Would delete", "將刪除"),
        ["retention.deleted"] = ("Deleted", "已刪除"),
        ["retention.skipped_locked"] = ("Skipped (locked): {0}", "已跳過（鎖定中）：{0}"),
        ["retention.preserved"] = ("Preserved: {0}", "已保留：{0}"),

        // config
        ["config.description"] = ("Show or initialize the configuration file.", "顯示或初始化設定檔。"),
        ["config.init.description"] = ("Write a default configuration file if one does not already exist.", "若尚未存在設定檔，則寫入預設設定檔。"),
        ["config.show.description"] = ("Print the effective, resolved configuration.", "印出目前生效的完整設定內容。"),
        ["config.init.exists"] = ("Configuration already exists at {0}. Not overwriting.", "設定檔已存在於 {0}，不會覆蓋。"),
        ["config.init.written"] = ("Wrote default configuration to {0}", "已將預設設定寫入 {0}"),
        ["config.show.file"] = ("Config file:       {0}", "設定檔路徑：        {0}"),
        ["config.show.destination"] = ("Destination:       {0}", "備份存放位置：      {0}"),
        ["config.show.sources"] = ("Sources:", "備份來源："),
        ["config.show.source_item"] = ("  - {0} (recursive={1})", "  - {0}（遞迴={1}）"),
        ["config.show.compression"] = ("Compression:       {0} (enabled={1})", "壓縮方式：          {0}（啟用={1}）"),
        ["config.show.encryption"] = ("Encryption:        {0} (key_id={1})", "加密演算法：        {0}（金鑰 ID={1}）"),
        ["config.show.hash"] = ("Hash algorithm:    {0}", "雜湊演算法：        {0}"),
        ["config.show.retention"] = ("Retention:         keep_days={0}, keep_count={1}", "保留策略：          keep_days={0}，keep_count={1}"),
        ["config.show.restore_dir"] = ("Restore directory: {0}", "還原目錄：          {0}"),
        ["config.show.audit_dir"] = ("Audit directory:   {0} (enabled={1})", "稽核目錄：          {0}（啟用={1}）"),
        ["config.show.sanitization"] = ("Sanitization:      enabled={0}", "日誌脫敏：          啟用={0}"),
        ["config.show.language"] = ("UI language:       {0}", "介面語言：          {0}"),

        // audit
        ["audit.description"] = ("Read audit records.", "查看稽核紀錄。"),
        ["audit.option.id"] = ("Filter to a single backup id.", "只顯示指定備份 ID 的紀錄。"),
        ["audit.record_count"] = ("{0} record(s).", "共 {0} 筆紀錄。"),

        // repair
        ["repair.description"] = ("Detect and clean up incomplete backups left behind by a crash or interruption.", "偵測並清理因中斷或崩潰而遺留的未完成備份。"),
        ["repair.option.delete"] = ("Delete incomplete artifacts instead of just listing them.", "刪除未完成的備份檔，而不只是列出。"),
        ["repair.none_found"] = ("No incomplete backups found.", "沒有找到任何未完成的備份。"),
        ["repair.found"] = ("Found {0} incomplete backup artifact(s):", "找到 {0} 個未完成的備份檔："),
        ["repair.deleted"] = ("Deleted.", "已刪除。"),
        ["repair.hint"] = ("Re-run with --delete to remove them. They are never treated as valid backups.", "請加上 --delete 重新執行以移除這些檔案。它們永遠不會被視為有效備份。"),
    };
}
