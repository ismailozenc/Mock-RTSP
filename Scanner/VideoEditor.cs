namespace MockRtsp;

/// <summary>
/// Everything about one video in the scanner, edited in place and saved at once:
///   the timeline: gaps (drag their edges, Shift + drag for a new one, right-click to delete), the
///     motion graph (growing while the motion is measured), ground-truth plates and the labels strip;
///   the gap settings, with "Pick gaps again" from the saved motion data;
///   the player for the selected playing part, where the plate reading area is drawn (detection
///     area, road perspective; or taken from Birel Plaka's settings);
///   the playing parts with their labels; the selected part's vehicles (time, coming or going,
///     plate); the cars read in it with their plate pictures and readings, to add as vehicles.
/// While the video is being scanned, everything here follows the results as they arrive (the graph
/// grows, then the gaps appear, then each part's cars and prefilled labels) and stays editable.
/// The video's data lives in a ScanVideo; this control only shows and edits it. The layout lives in
/// VideoEditor.Designer.cs.
/// </summary>
public sealed partial class VideoEditor : UserControl
{
    private ScanVideo? _video;
    private string? _ffmpeg;
    private List<PlayingPart> _parts = new();
    private int _current = -1;
    private bool _loading;
    private List<PlateCar> _partCars = new();
    private readonly HashSet<PlateCar> _used = new(ReferenceEqualityComparer.Instance);   // already vehicles
    private bool _selectingCar;          // set while the code (not the user) selects a car
    private static string? _birelSettingsFile;       // the camera-settings.ini chosen last (this run)

    public VideoEditor()
    {
        InitializeComponent();
        ShowNoVideo();
    }

    /// <summary>The gap settings for a video never scanned (the scanner's defaults).</summary>
    internal Func<MotionSettings>? GapDefaults { get; set; }

    /// <summary>A line for the log.</summary>
    public event EventHandler<string>? Message;

    // ===================================================================== which video

    /// <summary>Shows a video (null: none). The part shown before is kept first (and counts as looked at).</summary>
    internal void SetVideo(ScanVideo? video, string? ffmpegPath)
    {
        if (ReferenceEquals(video, _video))
            return;

        if (_video is not null)
        {
            CommitCurrent(quiet: true);
            LeavePart();
            _video.Changed -= OnVideoChanged;
        }

        _player.SetEditing(AreaEdit.None);
        _player.Stop();
        _video = video;
        _ffmpeg = ffmpegPath;
        _current = -1;
        _used.Clear();

        if (video is null)
        {
            ShowNoVideo();
            return;
        }

        video.Changed += OnVideoChanged;
        _player.SetVideo(ffmpegPath, video.Path, video.Info);
        _player.Area = video.ReadArea;
        _timeline.SetSource(video.Previews, video.Duration);
        Enable(true);
        ShowTitle(null);
        ShowGaps();
        ShowArea();
        FillParts();
        int first = Groups().FindIndex(g => !g.IsLabelled || video.IsToCheck(g.Part));
        if (_parts.Count > 0)
            Select(Math.Max(0, first));
        else
            ShowNoParts();
    }

    /// <summary>Puts the edits of the part shown into the video (and saves). False when a row is wrong.</summary>
    internal bool Commit() => CommitCurrent(quiet: false);

    /// <summary>
    /// Before the app closes: keeps the edits, the part shown counts as looked at, and the video is
    /// let go, so nothing that happens while the window is taken down (the grid emptying its rows)
    /// can be taken for an edit and saved.
    /// </summary>
    internal void Close()
    {
        if (_video is null)
            return;
        CommitCurrent(quiet: true);
        LeavePart();
        _video.Changed -= OnVideoChanged;
        _video = null;
        _current = -1;
        _player.Stop();
    }

    /// <summary>The scan state of the video, shown after its name ("reading plates 40%", "done").</summary>
    internal void ShowTitle(string? scanState)
    {
        if (_video is not { } video)
            return;

        string what = video.LoadError is { } error
            ? $"  -  its file cannot be read: {error}"
            : $"  -  {video.Info.Width}x{video.Info.Height}, {PlateMarks.FormatTime(video.Duration)}"
              + (scanState is null ? "" : $"  -  {scanState}");
        _title.Text = video.Name + what;
    }

    private void ShowNoVideo()
    {
        _title.Text = "Select a video in the list on the left. Scan finds its gaps, plates and directions.";
        _summary.Text = "";
        _timeline.SetSource(null, 0);
        _loading = true;
        _partList.Items.Clear();
        _table.Rows.Clear();
        _loading = false;
        _cars.Items.Clear();
        _readings.Items.Clear();
        _parts = new List<PlayingPart>();
        _partTitle.Text = "";
        _carsTitle.Text = "";
        _gapNote.Text = "";
        _areaNote.Text = "";
        Enable(false);
    }

    private void Enable(bool on)
    {
        foreach (var control in new Control[]
                 {
                     _timeline, _gapRow, _areaTools, _partList, _table, _addVehicle, _removeVehicle, _noVehicles, _cars, _readings,
                     _carTools, _previousUnlabelled, _nextUnlabelled, _deleteOutside, _export, _groundTruth, _prefill
                 })
            control.Enabled = on && _video is { CanEdit: true };
    }

    /// <summary>The video changed: a scan result arrived, or an edit was saved. Shows what changed.</summary>
    private void OnVideoChanged(object? sender, VideoChange change)
    {
        if (_video is null || IsDisposed)
            return;

        if ((change & VideoChange.Motion) != 0)
            ShowMotion();

        if ((change & VideoChange.Gaps) != 0)
        {
            ShowGaps();
            // New playing parts: keep showing the part at the same moment.
            double? at = _current >= 0 && _current < _parts.Count ? _parts[_current].Middle : null;
            CommitCurrent(quiet: true);
            _current = -1;
            FillParts();
            int index = at is double t ? _parts.FindIndex(p => p.Contains(t, _video.Duration)) : -1;
            if (_parts.Count == 0)
                ShowNoParts();
            else
                Select(Math.Max(0, index));
            return;
        }

        if ((change & VideoChange.Plates) != 0)
            _timeline.SetPlates(_video.Plates);

        if ((change & VideoChange.Area) != 0 && _player.Editing == AreaEdit.None)
            _player.Area = _video.ReadArea;

        if ((change & (VideoChange.Labels | VideoChange.Reading)) != 0)
        {
            RefreshParts();
            ReloadCurrentIfChanged();
            if ((change & VideoChange.Reading) != 0)
                ShowCars();
            ShowArea();
        }
    }

    // ===================================================================== gaps

    /// <summary>The gaps on the timeline, and the motion graph and gap settings.</summary>
    private void ShowGaps()
    {
        if (_video is not { } video)
            return;

        _timeline.SetSkipRanges(video.Skips);
        ShowMotion();
    }

    /// <summary>The motion graph (growing while measured) and the gap settings. Leaves the gaps alone (one may be dragged).</summary>
    private void ShowMotion()
    {
        if (_video is not { } video)
            return;

        var settings = video.MotionSettings ?? GapDefaults?.Invoke() ?? MotionSettings.Legacy;
        _timeline.SetMotion(video.Motion, settings.ThresholdPercent);
        _timeline.SetPlates(video.Plates);
        _timeline.SetLabels(video.Vehicles, video.EmptyParts);
        _timeline.Invalidate();

        SetValue(_threshold, settings.ThresholdPercent);
        SetValue(_minGap, settings.MinStillSeconds);
        SetValue(_keep, settings.PaddingSeconds);
        _pickGaps.Enabled = video.Scanned && video.CanEdit;
        _gapNote.Text = !video.Scanned
            ? (video.Motion is null ? "Not scanned yet: Scan finds the gaps (still parts are cut when streaming)." : "Measuring motion...")
            : video.GapsFromMotion
                ? $"{video.Skips.Count} gap(s) picked with these settings."
                : $"{video.Skips.Count} gap(s), changed by hand.";
    }

    private static void SetValue(NumericUpDown box, double value) =>
        box.Value = Math.Min(box.Maximum, Math.Max(box.Minimum, (decimal)value));

    private void OnPickGapsClick(object? sender, EventArgs e)
    {
        if (_video is not { } video)
            return;

        var settings = new MotionSettings((double)_threshold.Value, (double)_minGap.Value, (double)_keep.Value);
        if (settings.Validate() is { Count: > 0 } problems)
        {
            MessageBox.Show(this, string.Join("\n", problems), "Pick gaps again", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!video.GapsFromMotion && MessageBox.Show(this,
                $"The gaps of {video.Name} were changed by hand. Pick them again from the motion data with these settings? Your changes are replaced.",
                "Pick gaps again", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        if (!CommitCurrent(quiet: false))
            return;
        video.PickGaps(settings);
    }

    /// <summary>The user drew, moved or deleted gaps on the timeline.</summary>
    private void OnTimelineSkipsChanged(object? sender, EventArgs e)
    {
        if (_video is not { } video || !CommitCurrent(quiet: false))
        {
            ShowGaps();
            return;
        }

        video.SetSkips(_timeline.SkipRanges);
    }

    /// <summary>A click on the timeline: the part at that moment, shown at that moment.</summary>
    private void OnTimelineSeek(object? sender, double seconds)
    {
        if (_video is null)
            return;

        int index = _parts.FindIndex(p => p.Contains(seconds, _video.Duration));
        if (index < 0)
        {
            Status($"{PlateMarks.FormatTime(seconds)} is in a gap: it is not streamed, so it has no labels.");
            return;
        }

        if (index != _current && !Select(index))
            return;
        _player.ShowMoment(seconds);
    }

    private void OnTimelineLabelPart(object? sender, double seconds)
    {
        if (_video is null)
            return;
        int index = _parts.FindIndex(p => p.Contains(seconds, _video.Duration));
        if (index >= 0 && index != _current)
            Select(index);
    }

    private void OnPlayerPositionChanged(object? sender, EventArgs e)
    {
        if (_player.Position is double t)
        {
            _timeline.Position = t;
            _timeline.Invalidate();
        }
    }

    // ===================================================================== where plates are read

    /// <summary>The area's buttons and note, and whether the plates shown were read with another area.</summary>
    private void ShowArea()
    {
        var area = _player.Area;
        _editDetection.Checked = _player.Editing == AreaEdit.Detection;
        _editPerspective.Checked = _player.Editing == AreaEdit.Perspective;
        _wholePicture.Enabled = area.Detection is not null;
        _removePerspective.Enabled = area.Perspective is not null;

        string note = $"Plates are read in {area.Describe()}.";
        if (_video?.Reading is { } reading && !area.SameAs(reading.DetectionArea, reading.RoadPerspective))
            note += " The plates shown were read with another area: scan again (Read plates again) to use this one.";
        _areaNote.Text = note;
    }

    /// <summary>A drag on the player ended (or a reset, an undo): the area is saved.</summary>
    private void OnAreaChanged(object? sender, EventArgs e)
    {
        ShowArea();
        if (_player.Editing == AreaEdit.None)
            SaveArea();
    }

    /// <summary>Editing stopped: the area drawn is saved.</summary>
    private void OnAreaEditingChanged(object? sender, EventArgs e)
    {
        ShowArea();
        if (_player.Editing == AreaEdit.None)
            SaveArea();
    }

    private void SaveArea()
    {
        if (_video is { CanEdit: true } video)
            video.SetReadArea(_player.Area);
    }

    private void OnEditDetectionClick(object? sender, EventArgs e) =>
        _player.SetEditing(_player.Editing == AreaEdit.Detection ? AreaEdit.None : AreaEdit.Detection);

    private void OnEditPerspectiveClick(object? sender, EventArgs e) =>
        _player.SetEditing(_player.Editing == AreaEdit.Perspective ? AreaEdit.None : AreaEdit.Perspective);

    private void OnWholePictureClick(object? sender, EventArgs e)
    {
        _player.SetEditing(AreaEdit.None);
        _player.Area = _player.Area.WithDetection(null);
        SaveArea();
        ShowArea();
    }

    private void OnRemovePerspectiveClick(object? sender, EventArgs e)
    {
        _player.SetEditing(AreaEdit.None);
        _player.Area = _player.Area.WithPerspective(null);
        SaveArea();
        ShowArea();
    }

    /// <summary>
    /// Takes the detection area and road perspective of one of Birel Plaka's cameras, from its
    /// camera-settings.ini (next to Birel Plaka.exe, or in profiles\&lt;name&gt;\). The file is only read.
    /// </summary>
    private void OnFromBirelClick(object? sender, EventArgs e)
    {
        string? path = _birelSettingsFile is not null && File.Exists(_birelSettingsFile) ? _birelSettingsFile : null;
        if (path is null)
        {
            using var open = new OpenFileDialog
            {
                Title = "Birel Plaka's camera settings",
                Filter = $"Birel Plaka camera settings ({BirelCameraSettings.FileName})|{BirelCameraSettings.FileName}|INI files (*.ini)|*.ini",
                FileName = BirelCameraSettings.FileName
            };
            if (open.ShowDialog(this) != DialogResult.OK)
                return;
            path = open.FileName;
        }

        List<BirelCamera> cameras;
        try
        {
            cameras = BirelCameraSettings.Read(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, "From Birel Plaka", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _birelSettingsFile = path;
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"Cameras in {path}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        foreach (var camera in cameras)
        {
            var item = new ToolStripMenuItem(camera.Describe()) { Enabled = camera.Roi is not null || camera.Perspective is not null };
            item.Click += (_, _) => TakeFromBirel(camera);
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var other = new ToolStripMenuItem("Another camera-settings.ini...");
        other.Click += (_, _) =>
        {
            _birelSettingsFile = null;
            BeginInvoke(new Action(() => OnFromBirelClick(sender, e)));
        };
        menu.Items.Add(other);
        menu.Closed += (_, _) => menu.BeginInvoke(new Action(menu.Dispose));
        menu.Show(_fromBirel, new Point(0, _fromBirel.Height));
    }

    private void TakeFromBirel(BirelCamera camera)
    {
        ReadArea area;
        try
        {
            area = camera.ToReadArea();
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, $"The area of {camera.Name} cannot be used:\n\n{ex.Message}", "From Birel Plaka",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _player.SetEditing(AreaEdit.None);
        _player.Area = area;
        SaveArea();
        ShowArea();
        Status($"Took the area of {camera.Name} ({camera.Section}). Birel Plaka keeps them as fractions of the picture, so they fit any size of the same view.");
    }

    // ===================================================================== the list of parts

    private List<PartLabels> Groups() =>
        _video is null ? new List<PartLabels>() : VehicleLabels.Group(_parts, _video.Vehicles, _video.EmptyParts, _video.Duration);

    private void FillParts()
    {
        _parts = _video?.Parts ?? new List<PlayingPart>();
        _loading = true;
        try
        {
            _partList.BeginUpdate();
            _partList.Items.Clear();
            foreach (var part in _parts)
            {
                var item = new ListViewItem(part.Number.ToString());
                item.SubItems.Add(PlateMarks.FormatTime(part.Start));
                item.SubItems.Add(PlateMarks.FormatTime(part.End));
                item.SubItems.Add($"{part.Length:0.0} s");
                item.SubItems.Add(string.Empty);
                _partList.Items.Add(item);
            }
        }
        finally
        {
            _partList.EndUpdate();
            _loading = false;
        }

        RefreshParts();
    }

    /// <summary>Updates the Vehicles column, the colours, the summary and the timeline's labels.</summary>
    private void RefreshParts()
    {
        if (_video is not { } video)
            return;

        var groups = Groups();
        int toCheck = 0;
        for (int i = 0; i < groups.Count && i < _partList.Items.Count; i++)
        {
            var item = _partList.Items[i];
            int read = video.Reading?.CarsIn(_parts[i], video.Duration).Count ?? 0;
            bool check = video.IsToCheck(_parts[i]);
            if (check)
                toCheck++;
            item.SubItems[4].Text = (check ? "to check: " : "") + VehicleLabels.Describe(groups[i]) + (video.Reading is null ? "" : $"   [{read} read]");
            item.ForeColor = !groups[i].IsLabelled ? Color.DarkOrange : check ? Color.RoyalBlue : SystemColors.WindowText;
        }

        var summary = VehicleLabels.Summarize(_parts, video.Vehicles, video.EmptyParts, video.Duration);
        _summary.Text = (video.Scanned ? summary.Describe() : "Not scanned for gaps yet: the whole video is one playing part. " + summary.Describe())
                        + (toCheck > 0 ? $"; {toCheck} prefilled from the plates, to check" : "") + ".";
        _summary.ForeColor = summary.ToLabel > 0 || toCheck > 0 ? Color.DarkOrange : Color.ForestGreen;
        _deleteOutside.Enabled = video.CanEdit && summary.Outside > 0;
        _previousUnlabelled.Enabled = _nextUnlabelled.Enabled = summary.ToLabel > 0 || toCheck > 0;
        _prefill.Enabled = video.CanEdit && video.Reading is not null;
        _timeline.SetLabels(video.Vehicles, video.EmptyParts);
        _timeline.Invalidate();
    }

    private void ShowNoParts()
    {
        _partTitle.Text = "This video has nothing that plays.";
        _player.Stop();
        _table.Rows.Clear();
        _table.Enabled = _addVehicle.Enabled = _removeVehicle.Enabled = _noVehicles.Enabled = false;
    }

    private void OnPartSelectionChanged(object? sender, ListViewItemSelectionChangedEventArgs e)
    {
        if (_loading || !e.IsSelected || e.ItemIndex == _current)
            return;

        // Keep the edits of the part being left; if they are wrong, stay on it.
        if (!Select(e.ItemIndex))
            BeginInvoke(new Action(() => SelectInList(_current)));
    }

    /// <summary>Shows a part. Keeps the edits of the part shown before; returns false if those are wrong.</summary>
    private bool Select(int index)
    {
        if (_video is not { } video || index < 0 || index >= _parts.Count)
            return false;

        if (_current >= 0 && _current != index)
        {
            if (!CommitCurrent(quiet: false))
                return false;
            LeavePart();
        }

        _current = index;
        var part = _parts[index];
        LoadTable();
        _table.Enabled = _addVehicle.Enabled = _removeVehicle.Enabled = video.CanEdit;
        SelectInList(index);
        _player.PlayPart(part.Start, part.End);
        ShowCars();
        return true;
    }

    /// <summary>The part shown has been looked at: its prefilled labels count as checked.</summary>
    private void LeavePart()
    {
        if (_video is { CanEdit: true } video && _current >= 0 && _current < _parts.Count)
            video.MarkChecked(_parts[_current]);
    }

    /// <summary>The table and the title of the part shown, from the video.</summary>
    private void LoadTable()
    {
        if (_video is not { } video || _current < 0)
            return;

        var group = Groups()[_current];
        _loading = true;
        try
        {
            _table.EndEdit();
            _table.Rows.Clear();
            foreach (var v in group.Vehicles)
                _table.Rows.Add(PlateMarks.FormatTime(v.Time), v.DirectionText, v.Plate);

            _noVehicles.Checked = group.Vehicles.Count == 0 && group.MarkedEmpty;
            _noVehicles.Enabled = video.CanEdit && group.Vehicles.Count == 0;
            ShowPartTitle();
        }
        finally
        {
            _loading = false;
        }
    }

    private void ShowPartTitle()
    {
        if (_video is not { } video || _current < 0 || _current >= _parts.Count)
            return;

        var part = _parts[_current];
        _partTitle.Text = $"Part {part.Number} of {_parts.Count}: {PlateMarks.FormatTime(part.Start)} to "
                          + $"{PlateMarks.FormatTime(part.End)} ({part.Length:0.0} s)"
                          + (video.IsToCheck(part) ? "  -  prefilled from the plates read: check it" : "");
    }

    /// <summary>
    /// After a change from elsewhere (a part read and prefilled): reloads the part shown when its
    /// vehicles differ. A table being edited (a cell open, or a row that cannot be read yet) is left alone.
    /// </summary>
    private void ReloadCurrentIfChanged()
    {
        if (_current < 0 || _current >= _parts.Count || _table.IsCurrentCellInEditMode || !TryReadTable(out var rows, out _))
            return;

        if (!rows.SequenceEqual(Groups()[_current].Vehicles))
            LoadTable();
        else
            ShowPartTitle();        // "to check" may have changed
    }

    private void SelectInList(int index)
    {
        if (index < 0 || index >= _partList.Items.Count)
            return;

        bool loading = _loading;
        _loading = true;
        _partList.Items[index].Selected = true;
        _partList.Items[index].Focused = true;
        _partList.EnsureVisible(index);
        _loading = loading;
    }

    // ===================================================================== editing a part

    /// <summary>The table's rows as vehicles, or why a row cannot be read.</summary>
    private bool TryReadTable(out List<VehicleMark> rows, out string problem)
    {
        rows = new List<VehicleMark>();
        problem = string.Empty;
        if (_video is not { } video || _current < 0)
            return true;

        var part = _parts[_current];
        foreach (DataGridViewRow row in _table.Rows)
        {
            if (row.IsNewRow)
                continue;

            string Cell(int i) => row.Cells[i].Value?.ToString()?.Trim() ?? string.Empty;
            int number = row.Index + 1;

            if (!PlateMarks.TryParseTime(Cell(0), out double time))
            {
                problem = $"Row {number}: '{Cell(0)}' is not a time. Use seconds (83.4) or minutes and seconds (1:23.4).";
                return false;
            }

            if (!part.Contains(time, video.Duration))
            {
                problem = $"Row {number}: {PlateMarks.FormatTime(time)} is not in part {part.Number} "
                          + $"({PlateMarks.FormatTime(part.Start)} to {PlateMarks.FormatTime(part.End)}).";
                return false;
            }

            if (!VehicleLabels.TryParseDirection(Cell(1), out var direction))
            {
                problem = $"Row {number}: choose coming or going.";
                return false;
            }

            rows.Add(new VehicleMark(time, direction, Cell(2)));
        }

        rows = VehicleLabels.Normalize(rows, video.Duration);
        return true;
    }

    /// <summary>
    /// Puts the table of the part shown into the video, which saves it. When a row cannot be read:
    /// quiet, the status line says why (and nothing is saved yet); otherwise a message says why.
    /// </summary>
    private bool CommitCurrent(bool quiet)
    {
        if (_video is not { CanEdit: true } video || _current < 0 || _current >= _parts.Count)
            return true;

        _table.EndEdit();
        if (!TryReadTable(out var rows, out string problem))
        {
            if (quiet)
                Status(problem + " (not saved until it is fixed)");
            else
                MessageBox.Show(this, problem, video.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        video.SetPartLabels(_parts[_current], rows, rows.Count == 0 && _noVehicles.Checked);
        return true;
    }

    private void OnCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (!_loading)
            Later(() => CommitCurrent(quiet: true));
    }

    /// <summary>Runs after the grid has finished what it is doing (at once while there is no window yet).</summary>
    private void Later(Action action)
    {
        if (IsHandleCreated)
            BeginInvoke(action);
        else
            action();
    }

    /// <summary>A direction chosen in its list counts at once (not only when the cell is left).</summary>
    private void OnCurrentCellDirty(object? sender, EventArgs e)
    {
        if (_table.IsCurrentCellDirty && _table.CurrentCell?.ColumnIndex == _colDirection.Index)
            _table.CommitEdit(DataGridViewDataErrorContexts.Commit);
    }

    private void OnNoVehiclesChanged(object? sender, EventArgs e)
    {
        if (!_loading)
            CommitCurrent(quiet: true);
    }

    private void OnAddVehicleClick(object? sender, EventArgs e)
    {
        if (_current < 0 || _video is null)
            return;

        // At the moment the player shows, else the middle of the part.
        var part = _parts[_current];
        double time = _player.Position is double shown && part.Contains(shown, _video.Duration) ? shown : part.Middle;

        // Values first, then editing: a cell that starts editing before it has its value would save it empty.
        _table.EndEdit();
        int index = _table.Rows.Add(PlateMarks.FormatTime(time), "coming", string.Empty);
        _noVehicles.Checked = false;
        CommitCurrent(quiet: true);
        _table.CurrentCell = _table.Rows[index].Cells[2];
        _table.BeginEdit(true);
    }

    private void OnRemoveVehicleClick(object? sender, EventArgs e)
    {
        var rows = _table.SelectedRows.Cast<DataGridViewRow>().ToList();
        if (rows.Count == 0 && _table.CurrentRow is { } current)
            rows.Add(current);

        foreach (var row in rows.Where(r => !r.IsNewRow))
            _table.Rows.Remove(row);
        CommitCurrent(quiet: true);
    }

    /// <summary>One click on a direction opens its list (instead of a click to select and another to open).</summary>
    private void OnCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != _colDirection.Index)
            return;

        _table.BeginEdit(true);
        if (_table.EditingControl is ComboBox combo)
            combo.DroppedDown = true;
    }

    /// <summary>Double-click a vehicle's row: the player shows that moment, paused.</summary>
    private void OnCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex == _colDirection.Index)
            return;

        if (PlateMarks.TryParseTime(_table.Rows[e.RowIndex].Cells[0].Value?.ToString() ?? string.Empty, out double time))
            _player.ShowMoment(time);
    }

    /// <summary>"No vehicles" only makes sense for a part with none in the table; rows deleted with the Delete key count at once.</summary>
    private void OnRowsChanged(object? sender, EventArgs e)
    {
        if (_loading || _video is null || IsDisposed || Disposing)
            return;

        bool none = _table.Rows.Count == 0;
        _noVehicles.Enabled = none && _video is { CanEdit: true };
        if (!none)
            _noVehicles.Checked = false;
        if (e is DataGridViewRowsRemovedEventArgs)
            Later(() => CommitCurrent(quiet: true));
    }

    // ===================================================================== the cars read in a part

    private void ShowCars()
    {
        var video = _video;
        _cars.BeginUpdate();
        try
        {
            _cars.Items.Clear();
            _readings.Items.Clear();
            _plateImages.Images.Clear();
            _partCars = video?.Reading is { } reading && _current >= 0 ? reading.CarsIn(_parts[_current], video.Duration) : new List<PlateCar>();

            // A car already in the part's vehicles (at its time, as prefilled or added before) counts as added.
            var times = _table.Rows.Cast<DataGridViewRow>().Select(r => r.Cells[0].Value?.ToString()).ToHashSet();
            foreach (var car in _partCars.Where(c => times.Contains(PlateMarks.FormatTime(c.Time))))
                _used.Add(car);

            _carsTitle.Text = video?.Reading is null
                ? "No plates read yet: Scan (on the left) reads them in every playing part."
                : _partCars.Count == 0
                    ? "No plates were read in this part" + (video.Reading.Parts.Any(p => _current >= 0 && Math.Abs(p.Start - _parts[_current].Start) < 0.01) ? "." : " (not read yet).")
                    : $"{_partCars.Count} car(s) read in this part. Check the reading in the player, then add it as a vehicle.";

            for (int i = 0; i < _partCars.Count; i++)
            {
                var car = _partCars[i];
                _plateImages.Images.Add(Thumbnail(car.PlateJpeg));
                var item = new ListViewItem(car.Decided ? car.Plate : car.Plate + " ?", i);
                item.SubItems.Add(car.DirectionText);
                item.SubItems.Add(PlateMarks.FormatTime(car.Time));
                item.SubItems.Add(car.Reads.ToString());
                item.SubItems.Add(Result(car));
                item.ToolTipText = Result(car);
                if (_used.Contains(car))
                    item.ForeColor = SystemColors.GrayText;
                _cars.Items.Add(item);
            }
        }
        finally
        {
            _cars.EndUpdate();
        }

        // The first car still to add is selected, without moving the player: the part keeps playing.
        int first = _partCars.FindIndex(c => !_used.Contains(c));
        if (first >= 0)
        {
            _selectingCar = true;
            _cars.Items[first].Selected = true;
            _cars.Items[first].Focused = true;
            _selectingCar = false;
        }

        UpdateCarButtons();
    }

    /// <summary>The result column takes the rest of the list's width, so the reasons can be read.</summary>
    private void OnCarsResize(object? sender, EventArgs e)
    {
        int others = _colCarPlate.Width + _colCarDirection.Width + _colCarTime.Width + _colCarReads.Width;
        _colCarResult.Width = Math.Max(120, _cars.ClientSize.Width - others - 4);
    }

    private string Result(PlateCar car) =>
        (_used.Contains(car) ? "added; " : "")
        + (car.Decided ? $"decided ({car.Votes} reads agree)" : car.Reason ?? "not decided");

    /// <summary>The plate crop fitted into the list's picture size, on white.</summary>
    private Image Thumbnail(byte[]? jpeg)
    {
        var size = _plateImages.ImageSize;
        var thumb = new Bitmap(size.Width, size.Height);
        using var g = Graphics.FromImage(thumb);
        g.Clear(Color.White);
        if (jpeg is null)
            return thumb;

        try
        {
            using var stream = new MemoryStream(jpeg);
            using var image = Image.FromStream(stream);
            float scale = Math.Min(size.Width / (float)image.Width, size.Height / (float)image.Height);
            float w = image.Width * scale, h = image.Height * scale;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, (size.Width - w) / 2, (size.Height - h) / 2, w, h);
        }
        catch (ArgumentException)
        {
            // A damaged picture: the list shows the text only.
        }

        return thumb;
    }

    private PlateCar? SelectedCar =>
        _cars.SelectedIndices.Count > 0 && _cars.SelectedIndices[0] < _partCars.Count ? _partCars[_cars.SelectedIndices[0]] : null;

    /// <summary>The reading picked for the selected car: the one selected below, else the car's plate.</summary>
    private string SelectedReading(PlateCar car) =>
        _readings.SelectedItems.Count > 0 ? _readings.SelectedItems[0].Text : car.Plate;

    private void OnCarSelected(object? sender, EventArgs e)
    {
        _readings.Items.Clear();
        if (SelectedCar is { } car)
        {
            foreach (var reading in car.Readings)
            {
                var item = new ListViewItem(reading.Text);
                item.SubItems.Add(reading.Count.ToString());
                item.SubItems.Add($"{reading.BestOcr * 100:0}%");
                _readings.Items.Add(item);
            }

            int index = car.Readings.ToList().FindIndex(r => r.Text == car.Plate);
            if (_readings.Items.Count > 0)
                _readings.Items[Math.Max(0, index)].Selected = true;

            if (!_selectingCar)
                _player.ShowMoment(car.Time);    // the moment of its best read, paused
        }

        UpdateCarButtons();
    }

    private void UpdateCarButtons()
    {
        bool car = SelectedCar is not null && _video is { CanEdit: true };
        _useCar.Enabled = car;
        _useForSelected.Enabled = car;
        _useAll.Enabled = _video is { CanEdit: true } && _partCars.Any(c => !_used.Contains(c));
    }

    /// <summary>Adds the selected car (with the selected reading) as a vehicle of this part.</summary>
    private void OnUseCarClick(object? sender, EventArgs e)
    {
        if (SelectedCar is not { } car || _video is not { CanEdit: true })
            return;

        int row = AddCar(car, SelectedReading(car));
        AfterAdding(row);
    }

    /// <summary>Puts the selected reading into the vehicle selected in the table (and its direction, if that is still empty).</summary>
    private void OnUseForSelectedClick(object? sender, EventArgs e)
    {
        if (SelectedCar is not { } car)
            return;

        if (_table.CurrentRow is not { } row || row.IsNewRow)
        {
            MessageBox.Show(this, "Select a vehicle in the table above first.", _video?.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _table.EndEdit();
        row.Cells[2].Value = SelectedReading(car);
        if (string.IsNullOrEmpty(row.Cells[1].Value as string) && car.MostLikelyDirection is { } direction)
            row.Cells[1].Value = direction == VehicleDirection.Coming ? "coming" : "going";
        MarkUsed(car);
        CommitCurrent(quiet: true);
    }

    /// <summary>Adds every car of the part not added yet, each with its plate (voted, or the best guess).</summary>
    private void OnUseAllClick(object? sender, EventArgs e)
    {
        int last = -1;
        foreach (var car in _partCars.Where(c => !_used.Contains(c)).ToList())
            last = AddCar(car, car.Plate);
        AfterAdding(last);
    }

    /// <summary>A new row for a car: its best read's time, its most likely direction (else left to choose), and the plate.</summary>
    private int AddCar(PlateCar car, string plate)
    {
        _table.EndEdit();
        string? direction = car.MostLikelyDirection switch
        {
            VehicleDirection.Coming => "coming",
            VehicleDirection.Going => "going",
            _ => null
        };

        int index = _table.Rows.Add(PlateMarks.FormatTime(car.Time), direction, plate);
        _noVehicles.Checked = false;
        MarkUsed(car);
        return index;
    }

    /// <summary>After adding: saved; a row without a direction gets the direction cell, so it is chosen next.</summary>
    private void AfterAdding(int row)
    {
        if (row < 0)
            return;

        CommitCurrent(quiet: true);
        var missing = _table.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => string.IsNullOrEmpty(r.Cells[1].Value as string));
        if (missing is not null)
        {
            _table.CurrentCell = missing.Cells[1];
            Status("Nothing tells the direction of a vehicle: choose coming or going in the table (it is saved then).");
        }
        else
        {
            _table.CurrentCell = _table.Rows[row].Cells[2];
        }
    }

    private void MarkUsed(PlateCar car)
    {
        _used.Add(car);
        int i = _partCars.IndexOf(car);
        if (i >= 0 && i < _cars.Items.Count)
        {
            _cars.Items[i].ForeColor = SystemColors.GrayText;
            _cars.Items[i].SubItems[4].Text = Result(car);
            _cars.Items[i].ToolTipText = Result(car);
        }

        UpdateCarButtons();
    }

    private void OnPrefillClick(object? sender, EventArgs e)
    {
        if (_video is not { CanEdit: true } video || !CommitCurrent(quiet: false))
            return;
        Status(video.Prefill());
    }

    // ===================================================================== moving between parts

    private void OnNextUnlabelledClick(object? sender, EventArgs e) => GoToUnlabelled(+1);

    private void OnPreviousUnlabelledClick(object? sender, EventArgs e) => GoToUnlabelled(-1);

    /// <summary>The next (or previous) part without labels, or prefilled and still to check.</summary>
    private void GoToUnlabelled(int step)
    {
        if (_video is not { } video || !CommitCurrent(quiet: false))
            return;

        var groups = Groups();
        for (int n = 1; n <= groups.Count; n++)
        {
            int i = ((_current + step * n) % groups.Count + groups.Count) % groups.Count;
            if (!groups[i].IsLabelled || (i != _current && video.IsToCheck(_parts[i])))
            {
                Select(i);
                return;
            }
        }

        MessageBox.Show(this, video.ToCheckCount > 0 ? "Every other playing part is labelled and checked." : "Every playing part is labelled.",
            video.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ===================================================================== tidying, ground truth, export

    private void OnDeleteOutsideClick(object? sender, EventArgs e)
    {
        if (_video is not { } video || !CommitCurrent(quiet: false))
            return;

        int count = VehicleLabels.CountOutside(_parts, video.Vehicles, video.EmptyParts, video.Duration);
        if (count == 0)
            return;

        if (MessageBox.Show(this, $"{count} label(s) are in gaps now, so they are never streamed. Delete them?",
                video.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        video.DeleteLabelsInGaps();
    }

    /// <summary>The video's ground-truth plates (for scoring an ALPR system in the RTSP app), at the player's moment.</summary>
    private void OnGroundTruthClick(object? sender, EventArgs e)
    {
        if (_video is not { CanEdit: true } video)
            return;

        using var dialog = new PlatesDialog(video.Name, video.Plates, _player.Position ?? 0, video.Duration);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            video.SetPlates(dialog.Plates);
    }

    private void OnExportClick(object? sender, EventArgs e)
    {
        if (_video is not { } video || !CommitCurrent(quiet: false))
            return;

        try
        {
            using var dialog = new SaveFileDialog
            {
                Title = $"Export labels of {video.Name}",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = Path.GetFileNameWithoutExtension(video.Name) + "-labels.csv"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                File.WriteAllText(dialog.FileName, VehicleLabels.ToCsv(Groups()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Export labels", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Status(string text)
    {
        _scanStatus.Text = text;
        if (text.Length > 0)
            Message?.Invoke(this, (_video is null ? "" : _video.Name + ": ") + text);
    }
}
