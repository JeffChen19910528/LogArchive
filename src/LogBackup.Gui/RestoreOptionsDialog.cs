namespace LogBackup.Gui;

/// <summary>Small modal collecting restore options via a folder-browse dialog and checkboxes,
/// in place of typing an output path and y/N flags on a command line.</summary>
public sealed class RestoreOptionsDialog : Form
{
    private readonly TextBox _outputBox;
    private readonly CheckBox _overwriteCheck;
    private readonly CheckBox _forceCheck;

    public string OutputDirectory => _outputBox.Text;
    public bool Overwrite => _overwriteCheck.Checked;
    public bool Force => _forceCheck.Checked;

    public RestoreOptionsDialog(string defaultDirectory)
    {
        Text = "還原選項";
        Width = 480;
        Height = 220;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label { Text = "還原目的地（留空使用預設）：", AutoSize = true }, 0, 0);
        _outputBox = new TextBox { Dock = DockStyle.Fill, Text = string.Empty };
        layout.Controls.Add(_outputBox, 1, 0);
        var browseButton = new Button { Text = "瀏覽資料夾...", AutoSize = true };
        browseButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "選擇還原目的地", SelectedPath = defaultDirectory };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _outputBox.Text = dialog.SelectedPath;
            }
        };
        layout.Controls.Add(browseButton, 2, 0);

        _overwriteCheck = new CheckBox { Text = "覆蓋目的地已存在的檔案", AutoSize = true };
        layout.Controls.Add(_overwriteCheck, 0, 1);
        layout.SetColumnSpan(_overwriteCheck, 3);

        _forceCheck = new CheckBox { Text = "即使雜湊驗證失敗仍要還原", AutoSize = true };
        layout.Controls.Add(_forceCheck, 0, 2);
        layout.SetColumnSpan(_forceCheck, 3);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var okButton = new Button { Text = "開始還原", DialogResult = DialogResult.OK, AutoSize = true };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);
        layout.Controls.Add(buttonPanel, 0, 3);
        layout.SetColumnSpan(buttonPanel, 3);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
