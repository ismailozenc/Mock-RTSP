#nullable enable

namespace MockRtsp;

// Layout: one grid docked Fill, two columns. Across both columns: row 0 the video's name and scan
// state, row 1 the timeline (gaps, motion graph, plates, labels strip), row 2 the gap settings,
// row 3 the summary of the labels. Row 4 (the only row that grows): on the left the player showing
// the selected part, the plate reading area's buttons and the list of playing parts under it; on the
// right the selected part's vehicles with their buttons, the cars read in the part, the readings of
// the selected car and their buttons. Row 5: moving between parts, ground-truth plates, prefilling,
// exporting, and the status line.
public sealed partial class VideoEditor
{
    private System.ComponentModel.IContainer? components = null;

    private TableLayoutPanel _grid = null!;
    private Label _title = null!;
    private Timeline _timeline = null!;
    private FlowLayoutPanel _gapRow = null!;
    private Label _lblThreshold = null!;
    private NumericUpDown _threshold = null!;
    private Label _lblMinGap = null!;
    private NumericUpDown _minGap = null!;
    private Label _lblKeep = null!;
    private NumericUpDown _keep = null!;
    private Button _pickGaps = null!;
    private Label _gapNote = null!;
    private Button _groundTruth = null!;
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
    private Button _prefill = null!;
    private Label _scanStatus = null!;
    private FlowLayoutPanel _tools = null!;
    private Button _previousUnlabelled = null!;
    private Button _nextUnlabelled = null!;
    private Button _deleteOutside = null!;
    private Button _export = null!;

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
        _title = new Label();
        _timeline = new Timeline();
        _gapRow = new FlowLayoutPanel();
        _lblThreshold = new Label();
        _threshold = new NumericUpDown();
        _lblMinGap = new Label();
        _minGap = new NumericUpDown();
        _lblKeep = new Label();
        _keep = new NumericUpDown();
        _pickGaps = new Button();
        _gapNote = new Label();
        _groundTruth = new Button();
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
        _prefill = new Button();
        _scanStatus = new Label();
        _tools = new FlowLayoutPanel();
        _previousUnlabelled = new Button();
        _nextUnlabelled = new Button();
        _deleteOutside = new Button();
        _export = new Button();

        ((System.ComponentModel.ISupportInitialize)_table).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_threshold).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_minGap).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_keep).BeginInit();
        _grid.SuspendLayout();
        _gapRow.SuspendLayout();
        _left.SuspendLayout();
        _areaTools.SuspendLayout();
        _editor.SuspendLayout();
        _partTools.SuspendLayout();
        _carTools.SuspendLayout();
        _tools.SuspendLayout();
        SuspendLayout();

        // ================= Video, timeline and gap settings =================
        _title.AutoSize = true;
        _title.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _title.Margin = new Padding(0, 0, 0, 6);
        _title.Text = "Select a video on the left.";

        _timeline.Dock = DockStyle.Fill;
        _timeline.Margin = new Padding(0);

        _lblThreshold.AutoSize = true;
        _lblThreshold.Margin = new Padding(0, 7, 3, 3);
        _lblThreshold.Text = "Gaps: movement needed (% of the picture)";
        _threshold.DecimalPlaces = 1;
        _threshold.Increment = 0.1M;
        _threshold.Maximum = 25;
        _threshold.Minimum = 0.1M;
        _threshold.Value = 0.3M;
        _threshold.Width = 64;
        _lblMinGap.AutoSize = true;
        _lblMinGap.Margin = new Padding(12, 7, 3, 3);
        _lblMinGap.Text = "gaps of at least (s)";
        _minGap.DecimalPlaces = 1;
        _minGap.Increment = 0.5M;
        _minGap.Maximum = 3600;
        _minGap.Minimum = 1;
        _minGap.Value = 3;
        _minGap.Width = 64;
        _lblKeep.AutoSize = true;
        _lblKeep.Margin = new Padding(12, 7, 3, 3);
        _lblKeep.Text = "kept around movement (s)";
        _keep.DecimalPlaces = 1;
        _keep.Increment = 0.5M;
        _keep.Maximum = 60;
        _keep.Value = 1;
        _keep.Width = 64;
        _pickGaps.AutoSize = true;
        _pickGaps.Margin = new Padding(12, 3, 3, 3);
        _pickGaps.Text = "Pick gaps again";
        _gapNote.AutoSize = true;
        _gapNote.ForeColor = SystemColors.GrayText;
        _gapNote.Margin = new Padding(6, 7, 3, 3);
        _gapNote.Text = "";

        _gapRow.AutoSize = true;
        _gapRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _gapRow.Dock = DockStyle.Fill;
        _gapRow.Margin = new Padding(0, 4, 0, 4);
        _gapRow.WrapContents = true;
        _gapRow.Controls.Add(_lblThreshold);
        _gapRow.Controls.Add(_threshold);
        _gapRow.Controls.Add(_lblMinGap);
        _gapRow.Controls.Add(_minGap);
        _gapRow.Controls.Add(_lblKeep);
        _gapRow.Controls.Add(_keep);
        _gapRow.Controls.Add(_pickGaps);
        _gapRow.Controls.Add(_gapNote);

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
        _deleteOutside.Text = "Delete labels in gaps";
        _export.AutoSize = true;
        _export.Margin = new Padding(18, 3, 3, 3);
        _export.Text = "Export CSV...";
        _groundTruth.AutoSize = true;
        _groundTruth.Margin = new Padding(18, 3, 3, 3);
        _groundTruth.Text = "Ground-truth plates...";
        _prefill.AutoSize = true;
        _prefill.Enabled = false;
        _prefill.Margin = new Padding(18, 3, 3, 3);
        _prefill.Text = "Prefill unlabelled parts";
        _scanStatus.AutoSize = true;
        _scanStatus.ForeColor = SystemColors.GrayText;
        _scanStatus.Margin = new Padding(6, 3, 3, 3);
        _scanStatus.MaximumSize = new Size(520, 0);
        _scanStatus.Text = "";

        _tools.AutoSize = true;
        _tools.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _tools.Margin = new Padding(0, 8, 0, 0);
        _tools.WrapContents = false;
        _tools.Controls.Add(_previousUnlabelled);
        _tools.Controls.Add(_nextUnlabelled);
        _tools.Controls.Add(_deleteOutside);
        _tools.Controls.Add(_export);
        _tools.Controls.Add(_groundTruth);
        _tools.Controls.Add(_prefill);
        _tools.Controls.Add(_scanStatus);


        // ================= Grid =================
        _grid.ColumnCount = 2;
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57F));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
        _grid.Dock = DockStyle.Fill;
        _grid.Padding = new Padding(8);
        _grid.RowCount = 6;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.Controls.Add(_title, 0, 0);
        _grid.Controls.Add(_timeline, 0, 1);
        _grid.Controls.Add(_gapRow, 0, 2);
        _grid.Controls.Add(_summary, 0, 3);
        _grid.Controls.Add(_left, 0, 4);
        _grid.Controls.Add(_editor, 1, 4);
        _grid.Controls.Add(_tools, 0, 5);
        _grid.SetColumnSpan(_title, 2);
        _grid.SetColumnSpan(_timeline, 2);
        _grid.SetColumnSpan(_gapRow, 2);
        _grid.SetColumnSpan(_summary, 2);
        _grid.SetColumnSpan(_tools, 2);

        // ================= Events =================
        _partList.ItemSelectionChanged += new ListViewItemSelectionChangedEventHandler(OnPartSelectionChanged);
        _table.RowsAdded += new DataGridViewRowsAddedEventHandler(OnRowsChanged);
        _table.RowsRemoved += new DataGridViewRowsRemovedEventHandler(OnRowsChanged);
        _table.CellClick += new DataGridViewCellEventHandler(OnCellClick);
        _table.CellDoubleClick += new DataGridViewCellEventHandler(OnCellDoubleClick);
        _addVehicle.Click += new EventHandler(OnAddVehicleClick);
        _removeVehicle.Click += new EventHandler(OnRemoveVehicleClick);
        _previousUnlabelled.Click += new EventHandler(OnPreviousUnlabelledClick);
        _nextUnlabelled.Click += new EventHandler(OnNextUnlabelledClick);
        _deleteOutside.Click += new EventHandler(OnDeleteOutsideClick);
        _export.Click += new EventHandler(OnExportClick);
        _cars.SelectedIndexChanged += new EventHandler(OnCarSelected);
        _cars.Resize += new EventHandler(OnCarsResize);
        _cars.DoubleClick += new EventHandler(OnUseCarClick);
        _readings.DoubleClick += new EventHandler(OnUseCarClick);
        _useCar.Click += new EventHandler(OnUseCarClick);
        _useForSelected.Click += new EventHandler(OnUseForSelectedClick);
        _useAll.Click += new EventHandler(OnUseAllClick);
        _prefill.Click += new EventHandler(OnPrefillClick);
        _groundTruth.Click += new EventHandler(OnGroundTruthClick);
        _pickGaps.Click += new EventHandler(OnPickGapsClick);
        _table.CellEndEdit += new DataGridViewCellEventHandler(OnCellEndEdit);
        _table.CurrentCellDirtyStateChanged += new EventHandler(OnCurrentCellDirty);
        _noVehicles.CheckedChanged += new EventHandler(OnNoVehiclesChanged);
        _player.PositionChanged += new EventHandler(OnPlayerPositionChanged);
        _timeline.SeekRequested += new EventHandler<double>(OnTimelineSeek);
        _timeline.SkipRangesChanged += new EventHandler(OnTimelineSkipsChanged);
        _timeline.LabelPartRequested += new EventHandler<double>(OnTimelineLabelPart);
        _player.AreaChanged += new EventHandler(OnAreaChanged);
        _player.EditingChanged += new EventHandler(OnAreaEditingChanged);
        _editDetection.Click += new EventHandler(OnEditDetectionClick);
        _editPerspective.Click += new EventHandler(OnEditPerspectiveClick);
        _wholePicture.Click += new EventHandler(OnWholePictureClick);
        _removePerspective.Click += new EventHandler(OnRemovePerspectiveClick);
        _fromBirel.Click += new EventHandler(OnFromBirelClick);

        // ================= Control =================
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_grid);
        Font = new Font("Segoe UI", 9F);
        Size = new Size(1100, 820);

        ((System.ComponentModel.ISupportInitialize)_table).EndInit();
        ((System.ComponentModel.ISupportInitialize)_threshold).EndInit();
        ((System.ComponentModel.ISupportInitialize)_minGap).EndInit();
        ((System.ComponentModel.ISupportInitialize)_keep).EndInit();
        _gapRow.ResumeLayout(false);
        _gapRow.PerformLayout();
        _partTools.ResumeLayout(false);
        _partTools.PerformLayout();
        _carTools.ResumeLayout(false);
        _carTools.PerformLayout();
        _editor.ResumeLayout(false);
        _editor.PerformLayout();
        _areaTools.ResumeLayout(false);
        _areaTools.PerformLayout();
        _left.ResumeLayout(false);
        _tools.ResumeLayout(false);
        _grid.ResumeLayout(false);
        _grid.PerformLayout();
        ResumeLayout(false);
    }
}
