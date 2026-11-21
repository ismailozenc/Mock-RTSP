#nullable enable

namespace MockRtsp;

// Layout: one grid docked Fill, two columns. Row 0 (both columns): the video folder. Row 1 (grows):
// on the left the videos of the folder with their state, the queue's buttons, what to scan again,
// and the gap settings for new videos; on the right the editor of the selected video. Row 2 (both
// columns): the log.
public sealed partial class ScannerForm
{
    private System.ComponentModel.IContainer? components = null;

    private TableLayoutPanel _grid = null!;
    private FlowLayoutPanel _folderRow = null!;
    private Label _lblFolder = null!;
    private ComboBox _folder = null!;
    private Button _browse = null!;
    private Button _refresh = null!;
    private Label _folderInfo = null!;
    private Label _lblLanguage = null!;
    private ComboBox _language = null!;
    private TableLayoutPanel _left = null!;
    private ListView _videos = null!;
    private ColumnHeader _colName = null!;
    private ColumnHeader _colLength = null!;
    private ColumnHeader _colGaps = null!;
    private ColumnHeader _colPlates = null!;
    private ColumnHeader _colLabels = null!;
    private ColumnHeader _colScan = null!;
    private FlowLayoutPanel _queueRow = null!;
    private Button _scan = null!;
    private Button _scanAll = null!;
    private Button _stop = null!;
    private Button _unqueue = null!;
    private FlowLayoutPanel _againRow = null!;
    private CheckBox _gapsAgain = null!;
    private CheckBox _platesAgain = null!;
    private FlowLayoutPanel _defaultsRow = null!;
    private Label _lblDefaults = null!;
    private NumericUpDown _defThreshold = null!;
    private Label _lblDefMinGap = null!;
    private NumericUpDown _defMinGap = null!;
    private Label _lblDefKeep = null!;
    private NumericUpDown _defKeep = null!;
    private VideoEditor _editor = null!;
    private TextBox _log = null!;
    private System.Windows.Forms.Timer _timer = null!;

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
        components = new System.ComponentModel.Container();
        _grid = new TableLayoutPanel();
        _folderRow = new FlowLayoutPanel();
        _lblFolder = new Label();
        _folder = new ComboBox();
        _browse = new Button();
        _refresh = new Button();
        _folderInfo = new Label();
        _lblLanguage = new Label();
        _language = new ComboBox();
        _left = new TableLayoutPanel();
        _videos = new ListView();
        _colName = new ColumnHeader();
        _colLength = new ColumnHeader();
        _colGaps = new ColumnHeader();
        _colPlates = new ColumnHeader();
        _colLabels = new ColumnHeader();
        _colScan = new ColumnHeader();
        _queueRow = new FlowLayoutPanel();
        _scan = new Button();
        _scanAll = new Button();
        _stop = new Button();
        _unqueue = new Button();
        _againRow = new FlowLayoutPanel();
        _gapsAgain = new CheckBox();
        _platesAgain = new CheckBox();
        _defaultsRow = new FlowLayoutPanel();
        _lblDefaults = new Label();
        _defThreshold = new NumericUpDown();
        _lblDefMinGap = new Label();
        _defMinGap = new NumericUpDown();
        _lblDefKeep = new Label();
        _defKeep = new NumericUpDown();
        _editor = new VideoEditor();
        _log = new TextBox();
        _timer = new System.Windows.Forms.Timer(components);

        ((System.ComponentModel.ISupportInitialize)_defThreshold).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_defMinGap).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_defKeep).BeginInit();
        _grid.SuspendLayout();
        _folderRow.SuspendLayout();
        _left.SuspendLayout();
        _queueRow.SuspendLayout();
        _againRow.SuspendLayout();
        _defaultsRow.SuspendLayout();
        SuspendLayout();

        // ================= Video folder =================
        _lblFolder.AutoSize = true;
        _lblFolder.Margin = new Padding(0, 7, 3, 3);
        _lblFolder.Text = "Video folder:";
        _folder.Width = 440;
        _browse.AutoSize = true;
        _browse.Text = "Browse...";
        _refresh.AutoSize = true;
        _refresh.Text = "Refresh";
        _folderInfo.AutoSize = true;
        _folderInfo.ForeColor = SystemColors.GrayText;
        _folderInfo.Margin = new Padding(8, 7, 3, 3);
        _folderInfo.Text = "";
        _lblLanguage.AutoSize = true;
        _lblLanguage.Margin = new Padding(24, 7, 3, 3);
        _lblLanguage.Text = "Language:";
        _language.DropDownStyle = ComboBoxStyle.DropDownList;   // English / Türkçe, never typed
        _language.Items.AddRange(new object[] { "English", "Türkçe" });
        _language.Width = 100;

        _folderRow.AutoSize = true;
        _folderRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _folderRow.Dock = DockStyle.Fill;
        _folderRow.Margin = new Padding(0, 0, 0, 6);
        _folderRow.WrapContents = false;
        _folderRow.Controls.Add(_lblFolder);
        _folderRow.Controls.Add(_folder);
        _folderRow.Controls.Add(_browse);
        _folderRow.Controls.Add(_refresh);
        _folderRow.Controls.Add(_folderInfo);
        _folderRow.Controls.Add(_lblLanguage);
        _folderRow.Controls.Add(_language);

        // ================= Videos and the queue =================
        _colName.Text = "Video";
        _colName.Width = 120;
        _colLength.Text = "Length";
        _colLength.Width = 50;
        _colGaps.Text = "Gaps";
        _colGaps.Width = 68;
        _colPlates.Text = "Plates read";
        _colPlates.Width = 100;
        _colLabels.Text = "Labelled";
        _colLabels.Width = 124;
        _colScan.Text = "Scan";
        _colScan.Width = 150;

        _videos.Columns.AddRange(new ColumnHeader[] { _colName, _colLength, _colGaps, _colPlates, _colLabels, _colScan });
        _videos.Dock = DockStyle.Fill;
        _videos.FullRowSelect = true;
        _videos.HideSelection = false;
        _videos.Margin = new Padding(0);
        _videos.ShowItemToolTips = true;
        _videos.View = View.Details;

        _scan.AutoSize = true;
        _scan.Text = "Scan selected";
        _scanAll.AutoSize = true;
        _scanAll.Text = "Scan all not scanned";
        _stop.AutoSize = true;
        _stop.Text = "Stop";
        _unqueue.AutoSize = true;
        _unqueue.Text = "Take out of the queue";

        _queueRow.AutoSize = true;
        _queueRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _queueRow.Dock = DockStyle.Fill;
        _queueRow.Margin = new Padding(0, 6, 0, 0);
        _queueRow.Controls.Add(_scan);
        _queueRow.Controls.Add(_scanAll);
        _queueRow.Controls.Add(_stop);
        _queueRow.Controls.Add(_unqueue);

        _gapsAgain.AutoSize = true;
        _gapsAgain.Text = "Find gaps again";
        _platesAgain.AutoSize = true;
        _platesAgain.Margin = new Padding(12, 3, 3, 3);
        _platesAgain.Text = "Read plates again";

        _againRow.AutoSize = true;
        _againRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _againRow.Dock = DockStyle.Fill;
        _againRow.Margin = new Padding(0, 2, 0, 0);
        _againRow.Controls.Add(_gapsAgain);
        _againRow.Controls.Add(_platesAgain);

        _lblDefaults.AutoSize = true;
        _lblDefaults.Margin = new Padding(0, 7, 3, 3);
        _lblDefaults.Text = "New videos: movement (%)";
        _defThreshold.DecimalPlaces = 1;
        _defThreshold.Increment = 0.1M;
        _defThreshold.Maximum = 25;
        _defThreshold.Minimum = 0.1M;
        _defThreshold.Value = 0.3M;
        _defThreshold.Width = 56;
        _lblDefMinGap.AutoSize = true;
        _lblDefMinGap.Margin = new Padding(6, 7, 3, 3);
        _lblDefMinGap.Text = "at least (s)";
        _defMinGap.DecimalPlaces = 1;
        _defMinGap.Increment = 0.5M;
        _defMinGap.Maximum = 3600;
        _defMinGap.Minimum = 1;
        _defMinGap.Value = 3;
        _defMinGap.Width = 56;
        _lblDefKeep.AutoSize = true;
        _lblDefKeep.Margin = new Padding(6, 7, 3, 3);
        _lblDefKeep.Text = "keep (s)";
        _defKeep.DecimalPlaces = 1;
        _defKeep.Increment = 0.5M;
        _defKeep.Maximum = 60;
        _defKeep.Value = 1;
        _defKeep.Width = 56;

        _defaultsRow.AutoSize = true;
        _defaultsRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _defaultsRow.Dock = DockStyle.Fill;
        _defaultsRow.Margin = new Padding(0, 2, 0, 0);
        _defaultsRow.Controls.Add(_lblDefaults);
        _defaultsRow.Controls.Add(_defThreshold);
        _defaultsRow.Controls.Add(_lblDefMinGap);
        _defaultsRow.Controls.Add(_defMinGap);
        _defaultsRow.Controls.Add(_lblDefKeep);
        _defaultsRow.Controls.Add(_defKeep);

        _left.ColumnCount = 1;
        _left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _left.Dock = DockStyle.Fill;
        _left.Margin = new Padding(0, 0, 8, 0);
        _left.RowCount = 4;
        _left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.Controls.Add(_videos, 0, 0);
        _left.Controls.Add(_queueRow, 0, 1);
        _left.Controls.Add(_againRow, 0, 2);
        _left.Controls.Add(_defaultsRow, 0, 3);

        // ================= Editor and log =================
        _editor.Dock = DockStyle.Fill;
        _editor.Margin = new Padding(0);

        _log.Dock = DockStyle.Fill;
        _log.Font = new Font("Consolas", 8.5F);
        _log.Margin = new Padding(0, 6, 0, 0);
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;

        // ================= Grid =================
        _grid.ColumnCount = 2;
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 624F));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _grid.Dock = DockStyle.Fill;
        _grid.Padding = new Padding(10);
        _grid.RowCount = 3;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        _grid.Controls.Add(_folderRow, 0, 0);
        _grid.Controls.Add(_left, 0, 1);
        _grid.Controls.Add(_editor, 1, 1);
        _grid.Controls.Add(_log, 0, 2);
        _grid.SetColumnSpan(_folderRow, 2);
        _grid.SetColumnSpan(_log, 2);

        _timer.Interval = 1000;

        // ================= Events =================
        _folder.SelectionChangeCommitted += new EventHandler(OnFolderPicked);
        _folder.KeyDown += new KeyEventHandler(OnFolderKeyDown);
        _browse.Click += new EventHandler(OnBrowseClick);
        _refresh.Click += new EventHandler(OnRefreshClick);
        _videos.SelectedIndexChanged += new EventHandler(OnVideoSelected);
        _scan.Click += new EventHandler(OnScanClick);
        _scanAll.Click += new EventHandler(OnScanAllClick);
        _stop.Click += new EventHandler(OnStopClick);
        _unqueue.Click += new EventHandler(OnUnqueueClick);
        _gapsAgain.CheckedChanged += new EventHandler(OnOptionChanged);
        _platesAgain.CheckedChanged += new EventHandler(OnOptionChanged);
        _defThreshold.ValueChanged += new EventHandler(OnOptionChanged);
        _defMinGap.ValueChanged += new EventHandler(OnOptionChanged);
        _defKeep.ValueChanged += new EventHandler(OnOptionChanged);
        _timer.Tick += new EventHandler(OnTimerTick);
        _language.SelectedIndexChanged += new EventHandler(OnLanguageChanged);

        // ================= Form =================
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1640, 940);
        Controls.Add(_grid);
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(1280, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Mock RTSP Scanner";

        ((System.ComponentModel.ISupportInitialize)_defThreshold).EndInit();
        ((System.ComponentModel.ISupportInitialize)_defMinGap).EndInit();
        ((System.ComponentModel.ISupportInitialize)_defKeep).EndInit();
        _folderRow.ResumeLayout(false);
        _folderRow.PerformLayout();
        _queueRow.ResumeLayout(false);
        _queueRow.PerformLayout();
        _againRow.ResumeLayout(false);
        _againRow.PerformLayout();
        _defaultsRow.ResumeLayout(false);
        _defaultsRow.PerformLayout();
        _left.ResumeLayout(false);
        _grid.ResumeLayout(false);
        _grid.PerformLayout();
        ResumeLayout(false);
    }
}
