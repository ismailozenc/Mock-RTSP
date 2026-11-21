#nullable enable

namespace MockRtsp;

// Layout: one grid docked Fill, two columns. Row 0 the summary (both columns). Row 1 (the only
// row that grows): on the left the player showing the selected part, the plate reading area's
// buttons and the list of playing parts under it, and on the right the selected part's vehicles with their buttons. Row 2 the buttons for
// moving between parts, reading plates, prefilling from them and exporting, row 3 OK and Cancel. Under the vehicles: the
// cars of the part found by the plate scan, the readings of the selected car, and their buttons.
public sealed partial class LabelsDialog
{
    private System.ComponentModel.IContainer? components = null;

    private TableLayoutPanel _grid = null!;
    private Label _summary = null!;
    private TableLayoutPanel _left = null!;
    private PartPlayer _player = null!;
    private FlowLayoutPanel _areaTools = null!;
    private CheckBox _editDetection = null!;
    private CheckBox _editPerspective = null!;
    private Button _wholePicture = null!;
    private Button _removePerspective = null!;
    private Button _fromBirel = null!;
    private Label _areaNote = null!;
    private ListView _partList = null!;
    private ColumnHeader _colNumber = null!;
    private ColumnHeader _colFrom = null!;
    private ColumnHeader _colTo = null!;
    private ColumnHeader _colLength = null!;
    private ColumnHeader _colVehicles = null!;
    private TableLayoutPanel _editor = null!;
    private Label _partTitle = null!;
    private DataGridView _table = null!;
    private DataGridViewTextBoxColumn _colTime = null!;
    private DataGridViewComboBoxColumn _colDirection = null!;
    private DataGridViewTextBoxColumn _colPlate = null!;
    private FlowLayoutPanel _partTools = null!;
    private Button _addVehicle = null!;
    private Button _removeVehicle = null!;
    private CheckBox _noVehicles = null!;
    private FlowLayoutPanel _playRow = null!;
    private Button _play = null!;
    private Label _playNote = null!;
    private Label _carsTitle = null!;
    private ImageList _plateImages = null!;
    private ListView _cars = null!;
    private ColumnHeader _colCarPlate = null!;
    private ColumnHeader _colCarDirection = null!;
    private ColumnHeader _colCarTime = null!;
    private ColumnHeader _colCarReads = null!;
    private ColumnHeader _colCarResult = null!;
    private ListView _readings = null!;
    private ColumnHeader _colReading = null!;
    private ColumnHeader _colReadingCount = null!;
    private ColumnHeader _colReadingOcr = null!;
    private FlowLayoutPanel _carTools = null!;
    private Button _useCar = null!;
    private Button _useForSelected = null!;
    private Button _useAll = null!;
    private Button _readPlates = null!;
    private Button _prefill = null!;
    private ProgressBar _scanProgress = null!;
    private Label _scanStatus = null!;
    private FlowLayoutPanel _tools = null!;
    private Button _previousUnlabelled = null!;
    private Button _nextUnlabelled = null!;
    private Button _deleteOutside = null!;
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
        components = new System.ComponentModel.Container();
        _grid = new TableLayoutPanel();
        _summary = new Label();
        _left = new TableLayoutPanel();
        _player = new PartPlayer();
        _areaTools = new FlowLayoutPanel();
        _editDetection = new CheckBox();
        _editPerspective = new CheckBox();
        _wholePicture = new Button();
        _removePerspective = new Button();
        _fromBirel = new Button();
        _areaNote = new Label();
        _partList = new ListView();
        _colNumber = new ColumnHeader();
        _colFrom = new ColumnHeader();
        _colTo = new ColumnHeader();
        _colLength = new ColumnHeader();
        _colVehicles = new ColumnHeader();
        _editor = new TableLayoutPanel();
        _partTitle = new Label();
        _table = new DataGridView();
        _colTime = new DataGridViewTextBoxColumn();
        _colDirection = new DataGridViewComboBoxColumn();
        _colPlate = new DataGridViewTextBoxColumn();
        _partTools = new FlowLayoutPanel();
        _addVehicle = new Button();
        _removeVehicle = new Button();
        _noVehicles = new CheckBox();
        _playRow = new FlowLayoutPanel();
        _play = new Button();
        _playNote = new Label();
        _carsTitle = new Label();
        _plateImages = new ImageList(components);
        _cars = new ListView();
        _colCarPlate = new ColumnHeader();
        _colCarDirection = new ColumnHeader();
        _colCarTime = new ColumnHeader();
        _colCarReads = new ColumnHeader();
        _colCarResult = new ColumnHeader();
        _readings = new ListView();
        _colReading = new ColumnHeader();
        _colReadingCount = new ColumnHeader();
        _colReadingOcr = new ColumnHeader();
        _carTools = new FlowLayoutPanel();
        _useCar = new Button();
        _useForSelected = new Button();
        _useAll = new Button();
        _readPlates = new Button();
        _prefill = new Button();
        _scanProgress = new ProgressBar();
        _scanStatus = new Label();
        _tools = new FlowLayoutPanel();
        _previousUnlabelled = new Button();
        _nextUnlabelled = new Button();
        _deleteOutside = new Button();
        _export = new Button();
        _bottom = new FlowLayoutPanel();
        _ok = new Button();
        _cancel = new Button();

        ((System.ComponentModel.ISupportInitialize)_table).BeginInit();
        _grid.SuspendLayout();
        _left.SuspendLayout();
        _areaTools.SuspendLayout();
        _editor.SuspendLayout();
        _partTools.SuspendLayout();
        _playRow.SuspendLayout();
        _carTools.SuspendLayout();
        _tools.SuspendLayout();
        _bottom.SuspendLayout();
        SuspendLayout();

        // ================= Summary =================
        _summary.AutoSize = true;
        _summary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _summary.Margin = new Padding(0, 0, 0, 8);
        _summary.MaximumSize = new Size(900, 0);
        _summary.Text = "Playing parts";

        // ================= Playing parts =================
        _colNumber.Text = "#";
        _colNumber.Width = 40;
        _colFrom.Text = "From";
        _colFrom.Width = 64;
        _colTo.Text = "To";
        _colTo.Width = 64;
        _colLength.Text = "Length";
        _colLength.Width = 58;
        _colVehicles.Text = "Vehicles";
        _colVehicles.Width = 230;

        _partList.Columns.AddRange(new ColumnHeader[] { _colNumber, _colFrom, _colTo, _colLength, _colVehicles });
        _partList.Dock = DockStyle.Fill;
        _partList.FullRowSelect = true;
        _partList.HideSelection = false;
        _partList.Margin = new Padding(0, 8, 0, 0);
        _partList.MultiSelect = false;
        _partList.View = View.Details;

        // ================= Player, and the parts under it =================
        _player.Dock = DockStyle.Fill;
        _player.Margin = new Padding(0);

        _left.ColumnCount = 1;
        _left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _left.Dock = DockStyle.Fill;
        _left.Margin = new Padding(0, 0, 10, 0);
        // ================= Where plates are read (Birel Plaka's detection area and road perspective) =================
        _editDetection.Appearance = Appearance.Button;
        _editDetection.AutoCheck = false;
        _editDetection.AutoSize = true;
        _editDetection.Margin = new Padding(0, 3, 3, 3);
        _editDetection.Text = "Detection area (R)";
        _editDetection.UseVisualStyleBackColor = true;
        _editPerspective.Appearance = Appearance.Button;
        _editPerspective.AutoCheck = false;
        _editPerspective.AutoSize = true;
        _editPerspective.Text = "Road perspective (P)";
        _editPerspective.UseVisualStyleBackColor = true;
        _wholePicture.AutoSize = true;
        _wholePicture.Text = "Whole picture";
        _removePerspective.AutoSize = true;
        _removePerspective.Text = "No perspective";
        _fromBirel.AutoSize = true;
        _fromBirel.Text = "From Birel Plaka...";
        _areaNote.AutoSize = true;
        _areaNote.ForeColor = SystemColors.GrayText;
        _areaNote.Margin = new Padding(0, 2, 3, 0);
        _areaNote.MaximumSize = new Size(700, 0);
        _areaNote.Text = "Plates are read in the whole picture, with no road perspective.";

        _areaTools.AutoSize = true;
        _areaTools.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _areaTools.Dock = DockStyle.Fill;
        _areaTools.Margin = new Padding(0, 6, 0, 0);
        _areaTools.Controls.Add(_editDetection);
        _areaTools.Controls.Add(_editPerspective);
        _areaTools.Controls.Add(_wholePicture);
        _areaTools.Controls.Add(_removePerspective);
        _areaTools.Controls.Add(_fromBirel);
        _areaTools.Controls.Add(_areaNote);
        _areaTools.SetFlowBreak(_fromBirel, true);

        _left.RowCount = 3;
        _left.RowStyles.Add(new RowStyle(SizeType.Percent, 68F));
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.RowStyles.Add(new RowStyle(SizeType.Percent, 32F));
        _left.Controls.Add(_player, 0, 0);
        _left.Controls.Add(_areaTools, 0, 1);
        _left.Controls.Add(_partList, 0, 2);

        // ================= Selected part =================
        _partTitle.AutoSize = true;
        _partTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _partTitle.Margin = new Padding(0, 0, 0, 4);
        _partTitle.Text = "Part";

        _colTime.HeaderText = "Time";
        _colTime.Width = 80;
        _colDirection.HeaderText = "Direction";
        _colDirection.Items.AddRange(new object[] { "coming", "going" });
        _colDirection.Width = 90;
        _colPlate.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _colPlate.HeaderText = "Plate (optional)";

        _table.AllowUserToAddRows = false;
        _table.AllowUserToDeleteRows = true;
        _table.AllowUserToResizeRows = false;
        _table.BackgroundColor = SystemColors.Window;
        _table.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _table.Columns.AddRange(new DataGridViewColumn[] { _colTime, _colDirection, _colPlate });
        _table.Dock = DockStyle.Fill;
        _table.Margin = new Padding(0);
        _table.RowHeadersWidth = 30;
        _table.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        _addVehicle.AutoSize = true;
        _addVehicle.Text = "Add vehicle at the player's time";
        _removeVehicle.AutoSize = true;
        _removeVehicle.Text = "Remove";
        _noVehicles.AutoSize = true;
        _noVehicles.Margin = new Padding(10, 7, 3, 3);
        _noVehicles.Text = "No vehicles in this part";

        _partTools.AutoSize = true;
        _partTools.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _partTools.Margin = new Padding(0, 6, 0, 0);
        _partTools.WrapContents = false;
        _partTools.Controls.Add(_addVehicle);
        _partTools.Controls.Add(_removeVehicle);
        _partTools.Controls.Add(_noVehicles);

        _play.AutoSize = true;
        _play.Text = "Move the camera here";
        _playNote.AutoSize = true;
        _playNote.ForeColor = SystemColors.GrayText;
        _playNote.Margin = new Padding(6, 8, 3, 3);
        _playNote.MaximumSize = new Size(330, 0);
        _playNote.Text = "Jumps the camera's stream to the start of this part.";

        _playRow.AutoSize = true;
        _playRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _playRow.Margin = new Padding(0, 2, 0, 0);
        _playRow.WrapContents = false;
        _playRow.Controls.Add(_play);
        _playRow.Controls.Add(_playNote);

        // ================= Plates read in this part =================
        _carsTitle.AutoSize = true;
        _carsTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _carsTitle.Margin = new Padding(0, 12, 0, 4);
        _carsTitle.MaximumSize = new Size(520, 0);
        _carsTitle.Text = "Plates read in this part";

        _plateImages.ColorDepth = ColorDepth.Depth24Bit;
        _plateImages.ImageSize = new Size(108, 36);

        _colCarPlate.Text = "Plate";
        _colCarPlate.Width = 196;
        _colCarDirection.Text = "Direction";
        _colCarDirection.Width = 110;
        _colCarTime.Text = "Time";
        _colCarTime.Width = 58;
        _colCarReads.Text = "Reads";
        _colCarReads.Width = 48;
        _colCarResult.Text = "Result";
        _colCarResult.Width = 150;

        _cars.Columns.AddRange(new ColumnHeader[] { _colCarPlate, _colCarDirection, _colCarTime, _colCarReads, _colCarResult });
        _cars.Dock = DockStyle.Fill;
        _cars.FullRowSelect = true;
        _cars.HideSelection = false;
        _cars.Margin = new Padding(0);
        _cars.MultiSelect = false;
        _cars.ShowItemToolTips = true;
        _cars.SmallImageList = _plateImages;
        _cars.View = View.Details;

        _colReading.Text = "Readings of the selected car";
        _colReading.Width = 196;
        _colReadingCount.Text = "Reads";
        _colReadingCount.Width = 48;
        _colReadingOcr.Text = "Best OCR";
        _colReadingOcr.Width = 70;

        _readings.Columns.AddRange(new ColumnHeader[] { _colReading, _colReadingCount, _colReadingOcr });
        _readings.Dock = DockStyle.Fill;
        _readings.FullRowSelect = true;
        _readings.HideSelection = false;
        _readings.Margin = new Padding(0, 4, 0, 0);
        _readings.MultiSelect = false;
        _readings.View = View.Details;

        _useCar.AutoSize = true;
        _useCar.Text = "Add as vehicle";
        _useForSelected.AutoSize = true;
        _useForSelected.Text = "Put in the selected vehicle";
        _useAll.AutoSize = true;
        _useAll.Text = "Add every car of this part";

        _carTools.AutoSize = true;
        _carTools.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _carTools.Margin = new Padding(0, 6, 0, 0);
        _carTools.WrapContents = true;
        _carTools.Controls.Add(_useCar);
        _carTools.Controls.Add(_useForSelected);
        _carTools.Controls.Add(_useAll);

        _editor.ColumnCount = 1;
        _editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _editor.Dock = DockStyle.Fill;
        _editor.Margin = new Padding(0);
        _editor.RowCount = 8;
        _editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _editor.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        _editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _editor.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        _editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        _editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _editor.Controls.Add(_partTitle, 0, 0);
        _editor.Controls.Add(_table, 0, 1);
        _editor.Controls.Add(_partTools, 0, 2);
        _editor.Controls.Add(_playRow, 0, 3);
        _editor.Controls.Add(_carsTitle, 0, 4);
        _editor.Controls.Add(_cars, 0, 5);
        _editor.Controls.Add(_readings, 0, 6);
        _editor.Controls.Add(_carTools, 0, 7);

        // ================= Moving between parts =================
        _previousUnlabelled.AutoSize = true;
        _previousUnlabelled.Text = "< Previous to label";
        _nextUnlabelled.AutoSize = true;
        _nextUnlabelled.Text = "Next to label >";
        _deleteOutside.AutoSize = true;
        _deleteOutside.Margin = new Padding(18, 3, 3, 3);
        _deleteOutside.Text = "Delete labels in skipped parts";
        _export.AutoSize = true;
        _export.Margin = new Padding(18, 3, 3, 3);
        _export.Text = "Export CSV...";
        _readPlates.AutoSize = true;
        _readPlates.Margin = new Padding(18, 3, 3, 3);
        _readPlates.Text = "Read plates in every part";
        _prefill.AutoSize = true;
        _prefill.Enabled = false;
        _prefill.Text = "Prefill unlabelled parts";
        _scanProgress.Margin = new Padding(6, 6, 3, 3);
        _scanProgress.Size = new Size(140, 18);
        _scanProgress.Visible = false;
        _scanStatus.AutoSize = true;
        _scanStatus.ForeColor = SystemColors.GrayText;
        _scanStatus.Margin = new Padding(6, 3, 3, 3);
        _scanStatus.MaximumSize = new Size(360, 0);
        _scanStatus.Text = "";

        _tools.AutoSize = true;
        _tools.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _tools.Margin = new Padding(0, 8, 0, 0);
        _tools.WrapContents = false;
        _tools.Controls.Add(_previousUnlabelled);
        _tools.Controls.Add(_nextUnlabelled);
        _tools.Controls.Add(_deleteOutside);
        _tools.Controls.Add(_export);
        _tools.Controls.Add(_readPlates);
        _tools.Controls.Add(_prefill);
        _tools.Controls.Add(_scanProgress);
        _tools.Controls.Add(_scanStatus);

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
        _grid.ColumnCount = 2;
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57F));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
        _grid.Dock = DockStyle.Fill;
        _grid.Padding = new Padding(12);
        _grid.RowCount = 4;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.Controls.Add(_summary, 0, 0);
        _grid.Controls.Add(_left, 0, 1);
        _grid.Controls.Add(_editor, 1, 1);
        _grid.Controls.Add(_tools, 0, 2);
        _grid.Controls.Add(_bottom, 0, 3);
        _grid.SetColumnSpan(_summary, 2);
        _grid.SetColumnSpan(_tools, 2);
        _grid.SetColumnSpan(_bottom, 2);

        // ================= Events =================
        _partList.ItemSelectionChanged += new ListViewItemSelectionChangedEventHandler(OnPartSelectionChanged);
        _table.RowsAdded += new DataGridViewRowsAddedEventHandler(OnRowsChanged);
        _table.RowsRemoved += new DataGridViewRowsRemovedEventHandler(OnRowsChanged);
        _table.CellClick += new DataGridViewCellEventHandler(OnCellClick);
        _table.CellDoubleClick += new DataGridViewCellEventHandler(OnCellDoubleClick);
        _addVehicle.Click += new EventHandler(OnAddVehicleClick);
        _removeVehicle.Click += new EventHandler(OnRemoveVehicleClick);
        _play.Click += new EventHandler(OnPlayClick);
        _previousUnlabelled.Click += new EventHandler(OnPreviousUnlabelledClick);
        _nextUnlabelled.Click += new EventHandler(OnNextUnlabelledClick);
        _deleteOutside.Click += new EventHandler(OnDeleteOutsideClick);
        _export.Click += new EventHandler(OnExportClick);
        _ok.Click += new EventHandler(OnOkClick);
        _cars.SelectedIndexChanged += new EventHandler(OnCarSelected);
        _cars.Resize += new EventHandler(OnCarsResize);
        _cars.DoubleClick += new EventHandler(OnUseCarClick);
        _readings.DoubleClick += new EventHandler(OnUseCarClick);
        _useCar.Click += new EventHandler(OnUseCarClick);
        _useForSelected.Click += new EventHandler(OnUseForSelectedClick);
        _useAll.Click += new EventHandler(OnUseAllClick);
        _readPlates.Click += new EventHandler(OnReadPlatesClick);
        _prefill.Click += new EventHandler(OnPrefillClick);
        _player.AreaChanged += new EventHandler(OnAreaChanged);
        _player.EditingChanged += new EventHandler(OnAreaEditingChanged);
        _editDetection.Click += new EventHandler(OnEditDetectionClick);
        _editPerspective.Click += new EventHandler(OnEditPerspectiveClick);
        _wholePicture.Click += new EventHandler(OnWholePictureClick);
        _removePerspective.Click += new EventHandler(OnRemovePerspectiveClick);
        _fromBirel.Click += new EventHandler(OnFromBirelClick);

        // ================= Form =================
        AcceptButton = _ok;
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = _cancel;
        ClientSize = new Size(1320, 840);
        Controls.Add(_grid);
        Font = new Font("Segoe UI", 9F);
        MaximizeBox = true;
        MinimizeBox = false;
        MinimumSize = new Size(1060, 680);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Labels";

        ((System.ComponentModel.ISupportInitialize)_table).EndInit();
        _partTools.ResumeLayout(false);
        _partTools.PerformLayout();
        _playRow.ResumeLayout(false);
        _playRow.PerformLayout();
        _carTools.ResumeLayout(false);
        _carTools.PerformLayout();
        _editor.ResumeLayout(false);
        _editor.PerformLayout();
        _areaTools.ResumeLayout(false);
        _areaTools.PerformLayout();
        _left.ResumeLayout(false);
        _tools.ResumeLayout(false);
        _bottom.ResumeLayout(false);
        _grid.ResumeLayout(false);
        _grid.PerformLayout();
        ResumeLayout(false);
    }
}
