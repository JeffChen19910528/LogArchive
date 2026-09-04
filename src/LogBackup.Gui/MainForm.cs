using LogBackup.Core.Backup;
using LogBackup.Core.Exceptions;
using LogBackup.Core.Models;
using LogBackup.Core.Restoration;
using LogBackup.Infrastructure.Configuration;

namespace LogBackup.Gui;

/// <summary>
/// A real Windows GUI front end over the same LogBackup.Core/Infrastructure engines the CLI
/// uses (see GuiServices) - no typing commands, no typing paths where a native folder-browse
/// dialog can do it instead. Every action ends by appending a status line to the shared log box
/// at the bottom of the window, and the backup list refreshes automatically after anything that
/// changes it (backup/restore/delete/retention).
/// </summary>
public sealed class MainForm : Form
{
    private readonly string _configPath = Path.Combine(".", "config", "logbackup.yaml");
    private readonly TextBox _logBox;
    private readonly TabControl _tabs;

    // Backup tab
    private RadioButton _useConfigSourcesRadio = null!;
    private RadioButton _useCustomSourceRadio = null!;
    private TextBox _customSourceBox = null!;
    private CheckBox _incrementalCheck = null!;
    private Button _runBackupButton = null!;

    // Manage tab
    private DataGridView _backupGrid = null!;

    // Retention tab
    private CheckBox _dryRunCheck = null!;
    private TextBox _retentionOutputBox = null!;

    // Audit tab
    private ComboBox _auditFilterCombo = null!;
    private DataGridView _auditGrid = null!;

    // Repair tab
    private ListBox _repairList = null!;
    private CheckBox _repairDeleteCheck = null!;

    // Config tab
    private TextBox _configPathBox = null!;
    private ListBox _sourcesList = null!;
    private TextBox _destinationBox = null!;
    private TextBox _restoreDirBox = null!;
    private CheckBox _keepDaysEnabledCheck = null!;
    private NumericUpDown _keepDaysNumeric = null!;
    private CheckBox _keepCountEnabledCheck = null!;
    private NumericUpDown _keepCountNumeric = null!;

    public MainForm()
    {
        Text = "LogBackup";
        Width = 900;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        _logBox = new TextBox
        {
            Dock = DockStyle.Bottom,
            Multiline = true,
            ReadOnly = true,
            Height = 110,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font(FontFamily.GenericMonospace, 9),
        };

        _tabs = new TabControl { Dock = DockStyle.Fill };
        _tabs.TabPages.Add(BuildBackupTab());
        _tabs.TabPages.Add(BuildManageTab());
        _tabs.TabPages.Add(BuildRetentionTab());
        _tabs.TabPages.Add(BuildAuditTab());
        _tabs.TabPages.Add(BuildRepairTab());
        _tabs.TabPages.Add(BuildConfigTab());

        Controls.Add(_tabs);
        Controls.Add(_logBox);

        Load += async (_, _) =>
        {
            LoadConfigIntoForm();
            await RefreshBackupGridAsync();
        };
    }

    private GuiServices Services => GuiServices.Create(_configPath);

    private void Log(string message) =>
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

    /// <summary>Disables the whole window while an async engine call runs, so a second click
    /// can't start a second backup/restore on top of one already in flight.</summary>
    private async Task RunGuardedAsync(Func<Task> action)
    {
        Enabled = false;
        try
        {
            await action();
        }
        catch (LogBackupException ex)
        {
            Log($"錯誤：{ex.Message}");
            MessageBox.Show(ex.Message, "發生錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            Log($"未預期的錯誤：{ex.Message}");
            MessageBox.Show(ex.Message, "發生錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Enabled = true;
        }
    }

    // ----------------------------------------------------------------- 備份 -----

    private TabPage BuildBackupTab()
    {
        var page = new TabPage("備份");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _useConfigSourcesRadio = new RadioButton { Text = "使用設定檔中的全部來源", Checked = true, AutoSize = true };
        _useCustomSourceRadio = new RadioButton { Text = "指定單一來源目錄：", AutoSize = true };
        _customSourceBox = new TextBox { Dock = DockStyle.Fill, Enabled = false };
        var browseSourceButton = new Button { Text = "瀏覽資料夾...", AutoSize = true, Enabled = false };

        _useCustomSourceRadio.CheckedChanged += (_, _) =>
        {
            _customSourceBox.Enabled = _useCustomSourceRadio.Checked;
            browseSourceButton.Enabled = _useCustomSourceRadio.Checked;
        };

        browseSourceButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "選擇要備份的來源目錄" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _customSourceBox.Text = dialog.SelectedPath;
            }
        };

        _incrementalCheck = new CheckBox { Text = "增量備份（只備份異動過的檔案）", AutoSize = true };

        _runBackupButton = new Button { Text = "開始備份", AutoSize = true };
        _runBackupButton.Click += async (_, _) => await RunGuardedAsync(RunBackupAsync);

        layout.Controls.Add(_useConfigSourcesRadio, 0, 0);
        layout.SetColumnSpan(_useConfigSourcesRadio, 3);
        layout.Controls.Add(_useCustomSourceRadio, 0, 1);
        layout.Controls.Add(_customSourceBox, 1, 1);
        layout.Controls.Add(browseSourceButton, 2, 1);
        layout.Controls.Add(_incrementalCheck, 0, 2);
        layout.SetColumnSpan(_incrementalCheck, 3);
        layout.Controls.Add(_runBackupButton, 0, 3);

        page.Controls.Add(layout);
        return page;
    }

    private async Task RunBackupAsync()
    {
        var services = Services;
        List<BackupSourceConfig> sources;

        if (_useCustomSourceRadio.Checked)
        {
            if (string.IsNullOrWhiteSpace(_customSourceBox.Text))
            {
                Log("請先選擇來源目錄。");
                return;
            }
            sources = new List<BackupSourceConfig> { new() { Path = _customSourceBox.Text, Recursive = true } };
        }
        else
        {
            sources = services.Config.Backup.Sources;
        }

        if (sources.Count == 0)
        {
            Log("沒有可備份的來源目錄，請先在「設定」分頁新增來源，或指定單一來源目錄。");
            return;
        }

        var mode = _incrementalCheck.Checked ? BackupMode.Incremental : BackupMode.Full;

        foreach (var source in sources)
        {
            Log($"開始備份：{source.Path} ...");
            var result = await services.BackupEngine.CreateBackupAsync(source, services.Config.Backup.Encryption.KeyId, mode);
            var m = result.Metadata;
            Log($"完成：{m.BackupId}  檔案數={m.FileCount}  加密後大小={m.EncryptedSize / 1024.0 / 1024.0:F1} MB  狀態={m.Status}");
        }

        await RefreshBackupGridAsync();
    }

    // ----------------------------------------------------------------- 備份管理 -----

    private TabPage BuildManageTab()
    {
        var page = new TabPage("備份管理");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _backupGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        _backupGrid.Columns.Add("Id", "備份 ID");
        _backupGrid.Columns.Add("Created", "建立時間");
        _backupGrid.Columns.Add("Size", "加密後大小 (MB)");
        _backupGrid.Columns.Add("Status", "狀態");
        _backupGrid.Columns.Add("Source", "來源");

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        var refreshButton = new Button { Text = "重新整理", AutoSize = true };
        var infoButton = new Button { Text = "詳細資訊", AutoSize = true };
        var verifyButton = new Button { Text = "驗證完整性", AutoSize = true };
        var restoreButton = new Button { Text = "還原...", AutoSize = true };
        var deleteButton = new Button { Text = "刪除...", AutoSize = true };

        refreshButton.Click += async (_, _) => await RunGuardedAsync(RefreshBackupGridAsync);
        infoButton.Click += async (_, _) => await RunGuardedAsync(ShowSelectedBackupInfoAsync);
        verifyButton.Click += async (_, _) => await RunGuardedAsync(VerifySelectedBackupAsync);
        restoreButton.Click += async (_, _) => await RunGuardedAsync(RestoreSelectedBackupAsync);
        deleteButton.Click += async (_, _) => await RunGuardedAsync(DeleteSelectedBackupAsync);

        buttonPanel.Controls.Add(refreshButton);
        buttonPanel.Controls.Add(infoButton);
        buttonPanel.Controls.Add(verifyButton);
        buttonPanel.Controls.Add(restoreButton);
        buttonPanel.Controls.Add(deleteButton);

        layout.Controls.Add(_backupGrid, 0, 0);
        layout.Controls.Add(buttonPanel, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private async Task RefreshBackupGridAsync()
    {
        var services = Services;
        var backups = (await services.MetadataStore.ListAsync()).OrderByDescending(b => b.CreatedAtUtc).ToList();

        _backupGrid.Rows.Clear();
        foreach (var b in backups)
        {
            var rowIndex = _backupGrid.Rows.Add(
                b.BackupId,
                b.CreatedAtUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                (b.EncryptedSize / 1024.0 / 1024.0).ToString("F1"),
                b.Status.ToString(),
                b.Source);
            _backupGrid.Rows[rowIndex].Tag = b.BackupId;
        }

        RefreshAuditFilterOptions(backups);
    }

    private string? SelectedBackupId()
    {
        if (_backupGrid.SelectedRows.Count == 0)
        {
            MessageBox.Show("請先在清單中選擇一筆備份。", "尚未選擇", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        return (string)_backupGrid.SelectedRows[0].Tag!;
    }

    private async Task ShowSelectedBackupInfoAsync()
    {
        var id = SelectedBackupId();
        if (id is null) return;

        var m = await Services.MetadataStore.GetAsync(id);
        if (m is null)
        {
            Log($"找不到備份：{id}");
            return;
        }

        var text = string.Join(Environment.NewLine, new[]
        {
            $"ID：{m.BackupId}",
            $"來源：{m.Source}",
            $"建立時間：{m.CreatedAtUtc}",
            $"平台：{m.Platform}",
            $"主機名稱：{m.Hostname}",
            $"模式：{m.BackupMode}",
            $"檔案數：{m.FileCount}",
            $"原始大小：{m.OriginalSize}",
            $"壓縮後大小：{m.CompressedSize}",
            $"加密後大小：{m.EncryptedSize}",
            $"加密演算法：{m.EncryptionAlgorithm}",
            $"金鑰 ID：{m.KeyId}",
            $"雜湊演算法：{m.HashAlgorithm}",
            $"雜湊值：{m.Hash}",
            $"狀態：{m.Status}",
            $"封存檔名：{m.ArtifactFileName}",
        });
        MessageBox.Show(text, $"備份詳細資訊 - {id}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task VerifySelectedBackupAsync()
    {
        var id = SelectedBackupId();
        if (id is null) return;

        var services = Services;
        var m = await services.MetadataStore.GetAsync(id);
        if (m is null)
        {
            Log($"找不到備份：{id}");
            return;
        }

        if (!await services.Storage.ExistsAsync(m.ArtifactFileName))
        {
            m.Status = BackupStatus.Missing;
            await services.MetadataStore.SaveAsync(m);
            Log($"驗證失敗：找不到封存檔 {m.ArtifactFileName}");
            await RefreshBackupGridAsync();
            return;
        }

        string actual;
        await using (var stream = await services.Storage.OpenReadAsync(m.ArtifactFileName))
        {
            actual = await services.Hash.ComputeHashAsync(stream);
        }

        var pass = actual == m.Hash;
        m.Status = pass ? BackupStatus.Verified : BackupStatus.IntegrityFailed;
        await services.MetadataStore.SaveAsync(m);

        Log($"驗證 {id}：{(pass ? "通過" : "失敗")}（{m.HashAlgorithm} 預期={m.Hash} 實際={actual}）");
        await RefreshBackupGridAsync();
    }

    private async Task RestoreSelectedBackupAsync()
    {
        var id = SelectedBackupId();
        if (id is null) return;

        using var dialog = new RestoreOptionsDialog(Services.Config.Restore.DefaultDirectory);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var services = Services;
        var result = await services.RestoreEngine.RestoreAsync(new RestoreOptions
        {
            BackupId = id,
            OutputDirectory = string.IsNullOrWhiteSpace(dialog.OutputDirectory) ? null : dialog.OutputDirectory,
            Overwrite = dialog.Overwrite,
            Force = dialog.Force,
        });

        Log($"還原 {id} 完成：還原至 {result.RestoreDirectory}，共 {result.FilesRestored} 個檔案，完整性={(result.IntegrityPassed ? "通過" : "未通過")}");
    }

    private async Task DeleteSelectedBackupAsync()
    {
        var id = SelectedBackupId();
        if (id is null) return;

        var confirm = MessageBox.Show(
            $"確定要永久刪除備份 {id} 嗎？此動作無法復原。",
            "確認刪除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        var force = MessageBox.Show(
            "若此備份已被鎖定，是否仍要強制刪除？",
            "是否強制刪除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        try
        {
            await Services.RetentionEngine.DeleteAsync(id, force);
            Log($"已刪除備份：{id}");
        }
        catch (BackupLockedException ex)
        {
            Log($"刪除失敗（備份已鎖定）：{ex.Message}");
        }

        await RefreshBackupGridAsync();
    }

    // ----------------------------------------------------------------- 保留原則 -----

    private TabPage BuildRetentionTab()
    {
        var page = new TabPage("保留原則");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _dryRunCheck = new CheckBox { Text = "僅模擬執行（不會實際刪除任何項目）", AutoSize = true, Checked = true };
        var runButton = new Button { Text = "套用保留原則", AutoSize = true };
        runButton.Click += async (_, _) => await RunGuardedAsync(RunRetentionAsync);

        _retentionOutputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
        };

        layout.Controls.Add(_dryRunCheck, 0, 0);
        layout.Controls.Add(runButton, 0, 1);
        layout.Controls.Add(_retentionOutputBox, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private async Task RunRetentionAsync()
    {
        var services = Services;
        var report = await services.RetentionEngine.ApplyAsync(services.Config.Retention, _dryRunCheck.Checked);

        var lines = new List<string> { $"模式：{(_dryRunCheck.Checked ? "模擬執行" : "實際執行")}" };
        lines.Add($"已刪除（{report.Deleted.Count}）：{string.Join(", ", report.Deleted)}");
        lines.Add($"保留（{report.Preserved.Count}）：{string.Join(", ", report.Preserved)}");
        lines.Add($"因鎖定而略過（{report.SkippedLocked.Count}）：{string.Join(", ", report.SkippedLocked)}");
        _retentionOutputBox.Text = string.Join(Environment.NewLine, lines);

        Log($"保留原則執行完畢：刪除 {report.Deleted.Count} 筆，保留 {report.Preserved.Count} 筆。");
        await RefreshBackupGridAsync();
    }

    // ----------------------------------------------------------------- 稽核紀錄 -----

    private TabPage BuildAuditTab()
    {
        var page = new TabPage("稽核紀錄");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        filterPanel.Controls.Add(new Label { Text = "篩選備份：", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
        _auditFilterCombo = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
        var auditRefreshButton = new Button { Text = "重新整理", AutoSize = true };
        auditRefreshButton.Click += async (_, _) => await RunGuardedAsync(RefreshAuditGridAsync);
        filterPanel.Controls.Add(_auditFilterCombo);
        filterPanel.Controls.Add(auditRefreshButton);

        _auditGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        _auditGrid.Columns.Add("Time", "時間");
        _auditGrid.Columns.Add("Event", "事件");
        _auditGrid.Columns.Add("BackupId", "備份 ID");
        _auditGrid.Columns.Add("Operator", "操作者");
        _auditGrid.Columns.Add("Result", "結果");
        _auditGrid.Columns.Add("Detail", "詳情");

        layout.Controls.Add(filterPanel, 0, 0);
        layout.Controls.Add(_auditGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private void RefreshAuditFilterOptions(List<BackupMetadata> backups)
    {
        var previous = _auditFilterCombo.SelectedItem as string;
        _auditFilterCombo.Items.Clear();
        _auditFilterCombo.Items.Add("全部（不篩選）");
        foreach (var b in backups)
        {
            _auditFilterCombo.Items.Add(b.BackupId);
        }
        _auditFilterCombo.SelectedIndex = previous is not null && _auditFilterCombo.Items.Contains(previous)
            ? _auditFilterCombo.Items.IndexOf(previous)
            : 0;
    }

    private async Task RefreshAuditGridAsync()
    {
        var services = Services;
        var records = (await services.Audit.ReadAllAsync()).OrderByDescending(r => r.TimestampUtc).ToList();

        var filter = _auditFilterCombo.SelectedItem as string;
        if (!string.IsNullOrEmpty(filter) && filter != "全部（不篩選）")
        {
            records = records.Where(r => r.BackupId == filter).ToList();
        }

        _auditGrid.Rows.Clear();
        foreach (var r in records)
        {
            _auditGrid.Rows.Add(r.TimestampUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"), r.Event, r.BackupId, r.Operator, r.Result, r.Detail);
        }

        Log($"已載入 {records.Count} 筆稽核紀錄。");
    }

    // ----------------------------------------------------------------- 修復 -----

    private TabPage BuildRepairTab()
    {
        var page = new TabPage("修復");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _repairList = new ListBox { Dock = DockStyle.Fill };

        var scanButton = new Button { Text = "掃描未完成的備份檔（*.tmp）", AutoSize = true };
        scanButton.Click += async (_, _) => await RunGuardedAsync(ScanRepairAsync);

        _repairDeleteCheck = new CheckBox { Text = "掃描的同時刪除找到的檔案", AutoSize = true };

        layout.Controls.Add(_repairList, 0, 0);
        layout.Controls.Add(_repairDeleteCheck, 0, 1);
        layout.Controls.Add(scanButton, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private async Task ScanRepairAsync()
    {
        var services = Services;
        var incomplete = await services.Storage.ListFilesAsync(".", "*.tmp");

        _repairList.Items.Clear();
        foreach (var f in incomplete)
        {
            _repairList.Items.Add(f);
            if (_repairDeleteCheck.Checked)
            {
                await services.Storage.DeleteAsync(f);
            }
        }

        Log(incomplete.Count == 0
            ? "未發現未完成的備份檔。"
            : $"發現 {incomplete.Count} 個未完成的備份檔{(_repairDeleteCheck.Checked ? "，已刪除" : "")}。");
    }

    // ----------------------------------------------------------------- 設定 -----

    private TabPage BuildConfigTab()
    {
        var page = new TabPage("設定");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 3 };
        for (var i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var row = 0;

        layout.Controls.Add(new Label { Text = "設定檔路徑：", AutoSize = true }, 0, row);
        _configPathBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        layout.Controls.Add(_configPathBox, 1, row);
        var createConfigButton = new Button { Text = "建立預設設定檔", AutoSize = true };
        createConfigButton.Click += (_, _) => CreateDefaultConfigIfMissing();
        layout.Controls.Add(createConfigButton, 2, row);
        row++;

        layout.Controls.Add(new Label { Text = "備份目的地：", AutoSize = true }, 0, row);
        _destinationBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_destinationBox, 1, row);
        var browseDestButton = new Button { Text = "瀏覽資料夾...", AutoSize = true };
        browseDestButton.Click += (_, _) => BrowseInto(_destinationBox, "選擇備份目的地目錄");
        layout.Controls.Add(browseDestButton, 2, row);
        row++;

        layout.Controls.Add(new Label { Text = "還原預設目錄：", AutoSize = true }, 0, row);
        _restoreDirBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_restoreDirBox, 1, row);
        var browseRestoreButton = new Button { Text = "瀏覽資料夾...", AutoSize = true };
        browseRestoreButton.Click += (_, _) => BrowseInto(_restoreDirBox, "選擇還原預設目錄");
        layout.Controls.Add(browseRestoreButton, 2, row);
        row++;

        _keepDaysEnabledCheck = new CheckBox { Text = "依天數保留（keep_days）：", AutoSize = true };
        layout.Controls.Add(_keepDaysEnabledCheck, 0, row);
        _keepDaysNumeric = new NumericUpDown { Minimum = 1, Maximum = 3650, Width = 80, Enabled = false };
        _keepDaysEnabledCheck.CheckedChanged += (_, _) => _keepDaysNumeric.Enabled = _keepDaysEnabledCheck.Checked;
        layout.Controls.Add(_keepDaysNumeric, 1, row);
        row++;

        _keepCountEnabledCheck = new CheckBox { Text = "依份數保留（keep_count）：", AutoSize = true };
        layout.Controls.Add(_keepCountEnabledCheck, 0, row);
        _keepCountNumeric = new NumericUpDown { Minimum = 1, Maximum = 10000, Width = 80, Enabled = false };
        _keepCountEnabledCheck.CheckedChanged += (_, _) => _keepCountNumeric.Enabled = _keepCountEnabledCheck.Checked;
        layout.Controls.Add(_keepCountNumeric, 1, row);
        row++;

        layout.Controls.Add(new Label { Text = "來源目錄清單：", AutoSize = true }, 0, row);
        row++;

        _sourcesList = new ListBox { Dock = DockStyle.Fill, Height = 120 };
        layout.Controls.Add(_sourcesList, 0, row);
        layout.SetColumnSpan(_sourcesList, 2);
        var sourceButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        var addSourceButton = new Button { Text = "新增資料夾...", AutoSize = true };
        addSourceButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "選擇要加入的來源目錄" };
            if (dialog.ShowDialog(this) == DialogResult.OK && !_sourcesList.Items.Contains(dialog.SelectedPath))
            {
                _sourcesList.Items.Add(dialog.SelectedPath);
            }
        };
        var removeSourceButton = new Button { Text = "移除選取項目", AutoSize = true };
        removeSourceButton.Click += (_, _) =>
        {
            if (_sourcesList.SelectedIndex >= 0) _sourcesList.Items.RemoveAt(_sourcesList.SelectedIndex);
        };
        sourceButtons.Controls.Add(addSourceButton);
        sourceButtons.Controls.Add(removeSourceButton);
        layout.Controls.Add(sourceButtons, 2, row);
        row++;

        var saveButton = new Button { Text = "儲存設定", AutoSize = true };
        saveButton.Click += (_, _) => SaveConfigFromForm();
        layout.Controls.Add(saveButton, 0, row);

        page.Controls.Add(layout);
        return page;
    }

    private void BrowseInto(TextBox target, string description)
    {
        using var dialog = new FolderBrowserDialog { Description = description };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
        }
    }

    private void CreateDefaultConfigIfMissing()
    {
        if (File.Exists(_configPath))
        {
            MessageBox.Show("設定檔已存在。", "設定檔", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ConfigLoader.Save(ConfigLoader.CreateDefault(), _configPath);
        Log($"已建立預設設定檔：{_configPath}");
        LoadConfigIntoForm();
    }

    private void LoadConfigIntoForm()
    {
        var config = ConfigLoader.Load(_configPath);

        _configPathBox.Text = Path.GetFullPath(_configPath);
        _destinationBox.Text = config.Backup.Destination;
        _restoreDirBox.Text = config.Restore.DefaultDirectory;

        _sourcesList.Items.Clear();
        foreach (var s in config.Backup.Sources)
        {
            _sourcesList.Items.Add(s.Path);
        }

        _keepDaysEnabledCheck.Checked = config.Retention.KeepDays is not null;
        _keepDaysNumeric.Value = Math.Clamp(config.Retention.KeepDays ?? 30, (int)_keepDaysNumeric.Minimum, (int)_keepDaysNumeric.Maximum);
        _keepCountEnabledCheck.Checked = config.Retention.KeepCount is not null;
        _keepCountNumeric.Value = Math.Clamp(config.Retention.KeepCount ?? 10, (int)_keepCountNumeric.Minimum, (int)_keepCountNumeric.Maximum);
    }

    private void SaveConfigFromForm()
    {
        var config = File.Exists(_configPath) ? ConfigLoader.Load(_configPath) : ConfigLoader.CreateDefault();

        config.Backup.Destination = _destinationBox.Text;
        config.Restore.DefaultDirectory = _restoreDirBox.Text;
        config.Retention.KeepDays = _keepDaysEnabledCheck.Checked ? (int)_keepDaysNumeric.Value : null;
        config.Retention.KeepCount = _keepCountEnabledCheck.Checked ? (int)_keepCountNumeric.Value : null;

        // Preserve each existing source's Recursive/Include/Exclude when its path is kept;
        // a path newly added via "新增資料夾..." gets the config-file defaults.
        var existingByPath = config.Backup.Sources.ToDictionary(s => s.Path);
        config.Backup.Sources = _sourcesList.Items.Cast<string>()
            .Select(path => existingByPath.TryGetValue(path, out var existing) ? existing : new BackupSourceConfig { Path = path, Recursive = true })
            .ToList();

        ConfigLoader.Save(config, _configPath);
        Log($"設定已儲存：{_configPath}");
    }
}
