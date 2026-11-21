#nullable enable

namespace MockRtsp;

// Layout: one grid docked Fill. Row 0 headings, row 1 the two lists with the buttons between
// them (the only row that grows), row 2 a note, row 3 OK and Cancel.
public sealed partial class PlaylistDialog
{
    private System.ComponentModel.IContainer? components = null;

    private TableLayoutPanel _grid = null!;
    private Label _lblAvailable = null!;
    private Label _lblPlaylist = null!;
    private ListBox _available = null!;
    private FlowLayoutPanel _middle = null!;
    private Button _add = null!;
    private Button _remove = null!;
    private Button _up = null!;
    private Button _down = null!;
    private Button _addFile = null!;
    private ListBox _items = null!;
    private Label _note = null!;
    private FlowLayoutPanel _bottom = null!;
    private Button _ok = null!;
    private Button _cancel = null!;

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
        _lblAvailable = new Label();
        _lblPlaylist = new Label();
        _available = new ListBox();
        _middle = new FlowLayoutPanel();
        _add = new Button();
        _remove = new Button();
        _up = new Button();
        _down = new Button();
        _addFile = new Button();
        _items = new ListBox();
        _note = new Label();
        _bottom = new FlowLayoutPanel();
        _ok = new Button();
        _cancel = new Button();

        _grid.SuspendLayout();
        _middle.SuspendLayout();
        _bottom.SuspendLayout();
        SuspendLayout();

        // ================= Headings =================
        _lblAvailable.AutoSize = true;
        _lblAvailable.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _lblAvailable.Margin = new Padding(0, 0, 0, 4);
        _lblAvailable.Text = "Videos in the folder";

        _lblPlaylist.AutoSize = true;
        _lblPlaylist.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _lblPlaylist.Margin = new Padding(0, 0, 0, 4);
        _lblPlaylist.Text = "Playlist: plays top to bottom";

        // ================= Lists =================
        _available.Dock = DockStyle.Fill;
        _available.IntegralHeight = false;
        _available.Margin = new Padding(0);
        _available.SelectionMode = SelectionMode.MultiExtended;

        _items.Dock = DockStyle.Fill;
        _items.IntegralHeight = false;
        _items.Margin = new Padding(0);
        _items.SelectionMode = SelectionMode.One;

        // ================= Buttons between the lists =================
        _add.Margin = new Padding(0, 0, 0, 6);
        _add.Size = new Size(110, 30);
        _add.Text = "Add  >";
        _remove.Margin = new Padding(0, 0, 0, 18);
        _remove.Size = new Size(110, 30);
        _remove.Text = "<  Remove";
        _up.Margin = new Padding(0, 0, 0, 6);
        _up.Size = new Size(110, 30);
        _up.Text = "Move up";
        _down.Margin = new Padding(0, 0, 0, 18);
        _down.Size = new Size(110, 30);
        _down.Text = "Move down";
        _addFile.Margin = new Padding(0);
        _addFile.Size = new Size(110, 30);
        _addFile.Text = "Other file...";

        _middle.AutoSize = true;
        _middle.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _middle.FlowDirection = FlowDirection.TopDown;
        _middle.Margin = new Padding(10, 30, 10, 0);
        _middle.WrapContents = false;
        _middle.Controls.Add(_add);
        _middle.Controls.Add(_remove);
        _middle.Controls.Add(_up);
        _middle.Controls.Add(_down);
        _middle.Controls.Add(_addFile);

        // ================= Note =================
        _note.AutoSize = true;
        _note.ForeColor = SystemColors.GrayText;
        _note.Margin = new Padding(0, 8, 0, 0);
        _note.MaximumSize = new Size(680, 0);
        _note.Text = "After the last video the first starts again (unless looping is off in the settings). The camera's picture size, frame rate and quality come from the first video's settings; videos of another shape get black bars. Each video keeps its own skips.";

        // ================= OK and Cancel =================
        _cancel.DialogResult = DialogResult.Cancel;
        _cancel.Margin = new Padding(6, 0, 0, 0);
        _cancel.Size = new Size(96, 30);
        _cancel.Text = "Cancel";
        _ok.Margin = new Padding(6, 0, 0, 0);
        _ok.Size = new Size(96, 30);
        _ok.Text = "OK";

        _bottom.AutoSize = true;
        _bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _bottom.Anchor = AnchorStyles.Right;
        _bottom.FlowDirection = FlowDirection.RightToLeft;
        _bottom.Margin = new Padding(0, 10, 0, 0);
        _bottom.WrapContents = false;
        _bottom.Controls.Add(_cancel);
        _bottom.Controls.Add(_ok);

        // ================= Grid =================
        _grid.ColumnCount = 3;
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _grid.Dock = DockStyle.Fill;
        _grid.Padding = new Padding(12);
        _grid.RowCount = 4;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.Controls.Add(_lblAvailable, 0, 0);
        _grid.Controls.Add(_lblPlaylist, 2, 0);
        _grid.Controls.Add(_available, 0, 1);
        _grid.Controls.Add(_middle, 1, 1);
        _grid.Controls.Add(_items, 2, 1);
        _grid.Controls.Add(_note, 0, 2);
        _grid.SetColumnSpan(_note, 3);
        _grid.Controls.Add(_bottom, 0, 3);
        _grid.SetColumnSpan(_bottom, 3);

        // ================= Events =================
        _add.Click += new EventHandler(OnAddClick);
        _available.DoubleClick += new EventHandler(OnAddClick);
        _remove.Click += new EventHandler(OnRemoveClick);
        _up.Click += new EventHandler(OnUpClick);
        _down.Click += new EventHandler(OnDownClick);
        _addFile.Click += new EventHandler(OnAddFileClick);
        _available.SelectedIndexChanged += new EventHandler(OnSelectionChanged);
        _items.SelectedIndexChanged += new EventHandler(OnSelectionChanged);
        _ok.Click += new EventHandler(OnOkClick);

        // ================= Form =================
        AcceptButton = _ok;
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = _cancel;
        ClientSize = new Size(720, 440);
        Controls.Add(_grid);
        Font = new Font("Segoe UI", 9F);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(560, 360);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Playlist";

        _middle.ResumeLayout(false);
        _bottom.ResumeLayout(false);
        _grid.ResumeLayout(false);
        _grid.PerformLayout();
        ResumeLayout(false);
    }
}
