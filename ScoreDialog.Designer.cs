#nullable enable

namespace MockRtsp;

// Layout: one grid docked Fill, three columns (label | field | button).
// Rows: note, ground-truth file, results file, time window, Score, the report (grows), Save and Close.
public sealed partial class ScoreDialog
{
    private System.ComponentModel.IContainer? components = null;

    private TableLayoutPanel _grid = null!;
    private Label _note = null!;
    private Label _lblTruth = null!;
    private TextBox _truthFile = null!;
    private Button _browseTruth = null!;
    private Label _lblResults = null!;
    private TextBox _resultsFile = null!;
    private Button _browseResults = null!;
    private Label _lblWindow = null!;
    private FlowLayoutPanel _windowRow = null!;
    private NumericUpDown _before = null!;
    private Label _lblBefore = null!;
    private NumericUpDown _after = null!;
    private Label _lblAfter = null!;
    private Button _score = null!;
    private TextBox _report = null!;
    private FlowLayoutPanel _bottom = null!;
    private Button _close = null!;
    private Button _saveReport = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _grid = new TableLayoutPanel();
        _note = new Label();
        _lblTruth = new Label();
        _truthFile = new TextBox();
        _browseTruth = new Button();
        _lblResults = new Label();
        _resultsFile = new TextBox();
        _browseResults = new Button();
        _lblWindow = new Label();
        _windowRow = new FlowLayoutPanel();
        _before = new NumericUpDown();
        _lblBefore = new Label();
        _after = new NumericUpDown();
        _lblAfter = new Label();
        _score = new Button();
        _report = new TextBox();
        _bottom = new FlowLayoutPanel();
        _close = new Button();
        _saveReport = new Button();

        ((System.ComponentModel.ISupportInitialize)_before).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_after).BeginInit();
        _grid.SuspendLayout();
        _windowRow.SuspendLayout();
        _bottom.SuspendLayout();
        SuspendLayout();

        // ================= Note =================
        _note.AutoSize = true;
        _note.ForeColor = SystemColors.GrayText;
        _note.Margin = new Padding(0, 0, 0, 8);
        _note.MaximumSize = new Size(760, 0);
        _note.Text = "Compares what your ALPR system read with the plates that were streamed (the ground-truth log). The results file is CSV with a header: a time column (time, timestamp or utc: ISO 8601 or Unix seconds or milliseconds), a plate column (plate or text) and optionally a camera column (camera, path or url).";

        // ================= Files =================
        _lblTruth.AutoSize = true;
        _lblTruth.Margin = new Padding(0, 7, 8, 0);
        _lblTruth.Text = "Ground-truth log:";
        _truthFile.Dock = DockStyle.Fill;
        _truthFile.Margin = new Padding(0, 3, 6, 3);
        _browseTruth.AutoSize = true;
        _browseTruth.Margin = new Padding(0, 2, 0, 2);
        _browseTruth.Text = "Browse...";

        _lblResults.AutoSize = true;
        _lblResults.Margin = new Padding(0, 7, 8, 0);
        _lblResults.Text = "ALPR results:";
        _resultsFile.Dock = DockStyle.Fill;
        _resultsFile.Margin = new Padding(0, 3, 6, 3);
        _browseResults.AutoSize = true;
        _browseResults.Margin = new Padding(0, 2, 0, 2);
        _browseResults.Text = "Browse...";

        // ================= Window =================
        _lblWindow.AutoSize = true;
        _lblWindow.Margin = new Padding(0, 7, 8, 0);
        _lblWindow.Text = "Count reads from:";

        _before.DecimalPlaces = 1;
        _before.Increment = 0.5M;
        _before.Margin = new Padding(0, 3, 4, 3);
        _before.Maximum = 60;
        _before.Value = 2;
        _before.Width = 70;
        _lblBefore.AutoSize = true;
        _lblBefore.Margin = new Padding(0, 7, 12, 0);
        _lblBefore.Text = "s before, to";
        _after.DecimalPlaces = 1;
        _after.Increment = 0.5M;
        _after.Margin = new Padding(0, 3, 4, 3);
        _after.Maximum = 600;
        _after.Value = 10;
        _after.Width = 70;
        _lblAfter.AutoSize = true;
        _lblAfter.Margin = new Padding(0, 7, 0, 0);
        _lblAfter.Text = "s after the plate was streamed";

        _windowRow.AutoSize = true;
        _windowRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _windowRow.Margin = new Padding(0);
        _windowRow.WrapContents = false;
        _windowRow.Controls.Add(_before);
        _windowRow.Controls.Add(_lblBefore);
        _windowRow.Controls.Add(_after);
        _windowRow.Controls.Add(_lblAfter);

        _score.AutoSize = true;
        _score.Margin = new Padding(0, 6, 0, 6);
        _score.Text = "Score";

        // ================= Report =================
        _report.Dock = DockStyle.Fill;
        _report.Font = new Font("Consolas", 9F);
        _report.Margin = new Padding(0);
        _report.Multiline = true;
        _report.ReadOnly = true;
        _report.ScrollBars = ScrollBars.Both;
        _report.WordWrap = false;

        // ================= Bottom =================
        _close.DialogResult = DialogResult.Cancel;
        _close.Margin = new Padding(6, 0, 0, 0);
        _close.Size = new Size(96, 30);
        _close.Text = "Close";
        _saveReport.AutoSize = true;
        _saveReport.Enabled = false;
        _saveReport.Margin = new Padding(6, 0, 0, 0);
        _saveReport.Text = "Save report (CSV)...";

        _bottom.AutoSize = true;
        _bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _bottom.Anchor = AnchorStyles.Right;
        _bottom.FlowDirection = FlowDirection.RightToLeft;
        _bottom.Margin = new Padding(0, 10, 0, 0);
        _bottom.WrapContents = false;
        _bottom.Controls.Add(_close);
        _bottom.Controls.Add(_saveReport);

        // ================= Grid =================
        _grid.ColumnCount = 3;
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _grid.Dock = DockStyle.Fill;
        _grid.Padding = new Padding(12);
        _grid.RowCount = 7;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.Controls.Add(_note, 0, 0);
        _grid.Controls.Add(_lblTruth, 0, 1);
        _grid.Controls.Add(_truthFile, 1, 1);
        _grid.Controls.Add(_browseTruth, 2, 1);
        _grid.Controls.Add(_lblResults, 0, 2);
        _grid.Controls.Add(_resultsFile, 1, 2);
        _grid.Controls.Add(_browseResults, 2, 2);
        _grid.Controls.Add(_lblWindow, 0, 3);
        _grid.Controls.Add(_windowRow, 1, 3);
        _grid.Controls.Add(_score, 1, 4);
        _grid.Controls.Add(_report, 0, 5);
        _grid.Controls.Add(_bottom, 0, 6);
        _grid.SetColumnSpan(_note, 3);
        _grid.SetColumnSpan(_windowRow, 2);
        _grid.SetColumnSpan(_report, 3);
        _grid.SetColumnSpan(_bottom, 3);

        // ================= Events =================
        _browseTruth.Click += new EventHandler(OnBrowseTruthClick);
        _browseResults.Click += new EventHandler(OnBrowseResultsClick);
        _score.Click += new EventHandler(OnScoreClick);
        _saveReport.Click += new EventHandler(OnSaveReportClick);

        // ================= Form =================
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = _close;
        ClientSize = new Size(800, 560);
        Controls.Add(_grid);
        Font = new Font("Segoe UI", 9F);
        MinimizeBox = false;
        MinimumSize = new Size(620, 420);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Score ALPR results";

        ((System.ComponentModel.ISupportInitialize)_before).EndInit();
        ((System.ComponentModel.ISupportInitialize)_after).EndInit();
        _windowRow.ResumeLayout(false);
        _bottom.ResumeLayout(false);
        _grid.ResumeLayout(false);
        _grid.PerformLayout();
        ResumeLayout(false);
    }
}
