#nullable enable

namespace MockRtsp;

// Layout: one grid docked Fill. Row 0 a note, row 1 the plate table (the only row that grows),
// row 2 the table's buttons, row 3 OK and Cancel.
public sealed partial class PlatesDialog
{
    private System.ComponentModel.IContainer? components = null;

    private TableLayoutPanel _grid = null!;
    private Label _note = null!;
    private DataGridView _table = null!;
    private DataGridViewTextBoxColumn _colTime = null!;
    private DataGridViewTextBoxColumn _colPlate = null!;
    private DataGridViewTextBoxColumn _colX = null!;
    private DataGridViewTextBoxColumn _colY = null!;
    private DataGridViewTextBoxColumn _colWidth = null!;
    private DataGridViewTextBoxColumn _colHeight = null!;
    private FlowLayoutPanel _tools = null!;
    private Button _addAtPlayhead = null!;
    private Button _remove = null!;
    private Button _import = null!;
    private Button _export = null!;
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
        _note = new Label();
        _table = new DataGridView();
        _colTime = new DataGridViewTextBoxColumn();
        _colPlate = new DataGridViewTextBoxColumn();
        _colX = new DataGridViewTextBoxColumn();
        _colY = new DataGridViewTextBoxColumn();
        _colWidth = new DataGridViewTextBoxColumn();
        _colHeight = new DataGridViewTextBoxColumn();
        _tools = new FlowLayoutPanel();
        _addAtPlayhead = new Button();
        _remove = new Button();
        _import = new Button();
        _export = new Button();
        _bottom = new FlowLayoutPanel();
        _ok = new Button();
        _cancel = new Button();

        ((System.ComponentModel.ISupportInitialize)_table).BeginInit();
        _grid.SuspendLayout();
        _tools.SuspendLayout();
        _bottom.SuspendLayout();
        SuspendLayout();

        // ================= Note =================
        _note.AutoSize = true;
        _note.ForeColor = SystemColors.GrayText;
        _note.Margin = new Padding(0, 0, 0, 8);
        _note.MaximumSize = new Size(700, 0);
        _note.Text = "The licence plates visible in this video, and when: the ground truth your ALPR system should read. Time is seconds (83.4) or minutes and seconds (1:23.4). The region is optional: where the plate is, as fractions of the picture from 0 to 1. Each time a plate's frame is streamed it is written to the ground-truth log. Plates in skipped parts are never streamed.";

        // ================= Table =================
        _colTime.HeaderText = "Time";
        _colTime.Width = 90;
        _colPlate.HeaderText = "Plate";
        _colPlate.Width = 160;
        _colX.HeaderText = "x";
        _colX.Width = 70;
        _colY.HeaderText = "y";
        _colY.Width = 70;
        _colWidth.HeaderText = "width";
        _colWidth.Width = 70;
        _colHeight.HeaderText = "height";
        _colHeight.Width = 70;

        _table.AllowUserToAddRows = true;
        _table.AllowUserToDeleteRows = true;
        _table.AllowUserToResizeRows = false;
        _table.BackgroundColor = SystemColors.Window;
        _table.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _table.Columns.AddRange(new DataGridViewColumn[] { _colTime, _colPlate, _colX, _colY, _colWidth, _colHeight });
        _table.Dock = DockStyle.Fill;
        _table.Margin = new Padding(0);
        _table.RowHeadersWidth = 30;
        _table.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        // ================= Table buttons =================
        _addAtPlayhead.AutoSize = true;
        _addAtPlayhead.Text = "Add at playhead";
        _remove.AutoSize = true;
        _remove.Text = "Remove";
        _import.AutoSize = true;
        _import.Margin = new Padding(18, 3, 3, 3);
        _import.Text = "Import CSV...";
        _export.AutoSize = true;
        _export.Text = "Export CSV...";

        _tools.AutoSize = true;
        _tools.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _tools.Margin = new Padding(0, 6, 0, 0);
        _tools.WrapContents = false;
        _tools.Controls.Add(_addAtPlayhead);
        _tools.Controls.Add(_remove);
        _tools.Controls.Add(_import);
        _tools.Controls.Add(_export);

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
        _grid.ColumnCount = 1;
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _grid.Dock = DockStyle.Fill;
        _grid.Padding = new Padding(12);
        _grid.RowCount = 4;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.Controls.Add(_note, 0, 0);
        _grid.Controls.Add(_table, 0, 1);
        _grid.Controls.Add(_tools, 0, 2);
        _grid.Controls.Add(_bottom, 0, 3);

        // ================= Events =================
        _addAtPlayhead.Click += new EventHandler(OnAddAtPlayheadClick);
        _remove.Click += new EventHandler(OnRemoveClick);
        _import.Click += new EventHandler(OnImportClick);
        _export.Click += new EventHandler(OnExportClick);
        _ok.Click += new EventHandler(OnOkClick);

        // ================= Form =================
        AcceptButton = _ok;
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = _cancel;
        ClientSize = new Size(740, 480);
        Controls.Add(_grid);
        Font = new Font("Segoe UI", 9F);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(600, 360);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Plates";

        ((System.ComponentModel.ISupportInitialize)_table).EndInit();
        _tools.ResumeLayout(false);
        _bottom.ResumeLayout(false);
        _grid.ResumeLayout(false);
        _grid.PerformLayout();
        ResumeLayout(false);
    }
}
