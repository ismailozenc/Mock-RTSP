namespace MockRtsp;

/// <summary>
/// Labels the playing parts of one video with the vehicles in them: for each part, every vehicle
/// with its direction (coming or going) and its plate, or "no vehicles". This is the ground truth a
/// vehicle detector (for example the traffic monitor) is checked against. The selected part plays
/// in the player at the top left, straight from the video file, so its vehicles, their direction
/// and their plates can be seen while labelling (pause, step frame by frame, zoom with the mouse
/// wheel). The list shows every playing part and how many are labelled; "Next to label" walks
/// through the rest, and "Move the camera here" jumps the camera's stream to the part.
/// "Read plates in every part" reads the plates of all playing parts with Birel Plaka's models and
/// logic (<see cref="PlateScanner"/>); each part then lists the cars found, with a picture of the
/// plate, every reading and the direction, to add as vehicles after checking them.
/// The layout lives in LabelsDialog.Designer.cs.
/// </summary>
public sealed partial class LabelsDialog : Form
{
    private readonly double _duration;
    private readonly Func<double>? _cameraPosition;
    private readonly Action<double>? _seekCamera;
    private readonly string _videoName;
    private readonly List<PlayingPart> _parts;
    private readonly List<VehicleMark> _vehicles;   // all of them, including any in skipped parts
    private readonly List<double> _empty;           // moments in parts checked as having no vehicles
    private int _current = -1;
    private bool _loading;
    private readonly string _videoPath;
    private readonly VideoInfo _info;
    private readonly Func<PlateScanner>? _createScanner;
    private PlateScan? _scan;
    private List<PlateCar> _partCars = new();
    private readonly HashSet<PlateCar> _used = new(ReferenceEqualityComparer.Instance);   // added as vehicles in this dialog
    private CancellationTokenSource? _scanCancel;
    private bool _selectingCar;          // set while the code (not the user) selects a car

    /// <summary>Parameterless constructor for the designer.</summary>
    public LabelsDialog() : this("video.mp4", new VideoInfo(1920, 1080, 25, 60), null, Array.Empty<SkipRange>(),
        Array.Empty<VehicleMark>(), Array.Empty<double>(), null, null, null, null, null)
    {
    }

    /// <param name="videoPath">The video file, played in the dialog's player.</param>
    /// <param name="ffmpegPath">FFmpeg, for the player; null shows a note instead of the video.</param>
    /// <param name="cameraPosition">Where a camera playing this video is now, or null when none is.</param>
    /// <param name="showAt">A moment whose part is shown first, or null for the part at the camera's position.</param>
    /// <param name="seekCamera">Jumps that camera to a moment in this video, or null when none plays it.</param>
    /// <param name="plateScan">The video's saved plate scan, or null.</param>
    /// <param name="createScanner">Makes a plate scanner (loads the models), or null when plates cannot be read here.</param>
    internal LabelsDialog(string videoPath, VideoInfo info, string? ffmpegPath, IReadOnlyList<SkipRange> skips,
        IReadOnlyList<VehicleMark> vehicles, IReadOnlyList<double> emptyParts,
        Func<double>? cameraPosition, double? showAt, Action<double>? seekCamera,
        PlateScan? plateScan, Func<PlateScanner>? createScanner)
    {
        InitializeComponent();

        _videoPath = videoPath;
        _info = info;
        _scan = plateScan;
        _createScanner = createScanner;
        _readPlates.Enabled = createScanner is not null;
        if (plateScan is not null)
            _scanStatus.Text = $"Plates read {plateScan.ScannedUtc.ToLocalTime():g}: {plateScan.Cars.Count} car(s).";

        _videoName = Path.GetFileName(videoPath);
        _duration = info.DurationSeconds;
        _cameraPosition = cameraPosition;
        _seekCamera = seekCamera;
        _parts = VehicleLabels.PlayingParts(skips, _duration);
        _vehicles = vehicles.ToList();
        _empty = emptyParts.ToList();

        Text = $"Labels in {_videoName}";
        _player.SetVideo(ffmpegPath, videoPath, info);
        _play.Enabled = seekCamera is not null;
        _playNote.Text = seekCamera is null
            ? "No camera is playing this video now."
            : "Jumps the camera's stream to the start of this part.";

        FillParts();

        double? around = showAt ?? cameraPosition?.Invoke();
        int first = around is double t ? _parts.FindIndex(p => p.Contains(t, _duration)) : -1;
        if (first < 0)
            first = Groups().FindIndex(g => !g.IsLabelled);
        if (_parts.Count > 0)
            Select(Math.Max(0, first));
        else
            ShowNoParts();
    }

    /// <summary>The vehicle labels, sorted. Read after OK.</summary>
    public IReadOnlyList<VehicleMark> Vehicles { get; private set; } = Array.Empty<VehicleMark>();

    /// <summary>Moments in parts checked as having no vehicles. Read after OK.</summary>
    public IReadOnlyList<double> EmptyParts { get; private set; } = Array.Empty<double>();

    // ===================================================================== the list of parts

    private List<PartLabels> Groups() => VehicleLabels.Group(_parts, _vehicles, _empty, _duration);

    private void FillParts()
    {
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

    /// <summary>Updates the Vehicles column, the colours and the summary from the labels.</summary>
    private void RefreshParts()
    {
        var groups = Groups();
        for (int i = 0; i < groups.Count && i < _partList.Items.Count; i++)
        {
            var item = _partList.Items[i];
            int read = _scan?.CarsIn(_parts[i], _duration).Count ?? 0;
            item.SubItems[4].Text = VehicleLabels.Describe(groups[i]) + (_scan is null ? "" : $"   [{read} read]");
            item.ForeColor = groups[i].IsLabelled ? SystemColors.WindowText : Color.DarkOrange;
        }

        var summary = VehicleLabels.Summarize(_parts, _vehicles, _empty, _duration);
        _summary.Text = summary.Describe() + ".";
        _summary.ForeColor = summary.ToLabel > 0 ? Color.DarkOrange : Color.ForestGreen;
        _deleteOutside.Enabled = summary.Outside > 0;
        _previousUnlabelled.Enabled = _nextUnlabelled.Enabled = summary.ToLabel > 0;
    }

    private void ShowNoParts()
    {
        _partTitle.Text = "This video has nothing that plays.";
        _player.Stop();
        _table.Enabled = _addVehicle.Enabled = _removeVehicle.Enabled = _noVehicles.Enabled = _play.Enabled = false;
    }

    private void OnPartSelectionChanged(object? sender, ListViewItemSelectionChangedEventArgs e)
    {
        if (_loading || !e.IsSelected || e.ItemIndex == _current)
            return;

        // Keep the edits of the part being left; if they are wrong, stay on it.
        if (!Select(e.ItemIndex))
            BeginInvoke(new Action(() => SelectInList(_current)));
    }

    /// <summary>Shows a part. Saves the edits of the part shown before; returns false if those are wrong.</summary>
    private bool Select(int index)
    {
        if (index < 0 || index >= _parts.Count)
            return false;

        if (_current >= 0 && _current != index && !CommitCurrent())
            return false;

        _current = index;
        var part = _parts[index];
        var group = Groups()[index];

        _loading = true;
        try
        {
            _table.EndEdit();
            _table.Rows.Clear();
            foreach (var v in group.Vehicles)
                _table.Rows.Add(PlateMarks.FormatTime(v.Time), v.DirectionText, v.Plate);

            _noVehicles.Checked = group.Vehicles.Count == 0 && group.MarkedEmpty;
            _noVehicles.Enabled = group.Vehicles.Count == 0;
            _partTitle.Text = $"Part {part.Number} of {_parts.Count}: {PlateMarks.FormatTime(part.Start)} to "
                              + $"{PlateMarks.FormatTime(part.End)} ({part.Length:0.0} s)";
            SelectInList(index);
        }
        finally
        {
            _loading = false;
        }

        _player.PlayPart(part.Start, part.End);
        ShowCars();
        return true;
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

    /// <summary>
    /// Puts the table of the part shown back into the labels. False (after saying what is wrong)
    /// when a row cannot be read or lies outside the part.
    /// </summary>
    private bool CommitCurrent()
    {
        if (_current < 0)
            return true;

        _table.EndEdit();
        var part = _parts[_current];
        var rows = new List<VehicleMark>();

        foreach (DataGridViewRow row in _table.Rows)
        {
            if (row.IsNewRow)
                continue;

            string Cell(int i) => row.Cells[i].Value?.ToString()?.Trim() ?? string.Empty;
            int number = row.Index + 1;

            if (!PlateMarks.TryParseTime(Cell(0), out double time))
                return Problem($"Row {number}: '{Cell(0)}' is not a time. Use seconds (83.4) or minutes and seconds (1:23.4).");

            if (!part.Contains(time, _duration))
                return Problem($"Row {number}: {PlateMarks.FormatTime(time)} is not in part {part.Number} "
                               + $"({PlateMarks.FormatTime(part.Start)} to {PlateMarks.FormatTime(part.End)}).");

            if (!VehicleLabels.TryParseDirection(Cell(1), out var direction))
                return Problem($"Row {number}: choose coming or going.");

            rows.Add(new VehicleMark(time, direction, Cell(2)));
        }

        _vehicles.RemoveAll(v => part.Contains(v.Time, _duration));
        _vehicles.AddRange(rows);

        _empty.RemoveAll(t => part.Contains(t, _duration));
        if (rows.Count == 0 && _noVehicles.Checked)
            _empty.Add(Math.Round(part.Middle, 3));

        RefreshParts();
        return true;
    }

    private bool Problem(string message)
    {
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void OnAddVehicleClick(object? sender, EventArgs e)
    {
        if (_current < 0)
            return;

        // At the moment the player shows; else where the camera is, if in this part; else the middle of the part.
        var part = _parts[_current];
        double time = _player.Position is double shown && part.Contains(shown, _duration) ? shown
            : _cameraPosition?.Invoke() is double now && part.Contains(now, _duration) ? now
            : part.Middle;

        // Values first, then editing: a cell that starts editing before it has its value would save it empty.
        _table.EndEdit();
        int index = _table.Rows.Add(PlateMarks.FormatTime(time), "coming", string.Empty);
        _noVehicles.Checked = false;
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

    /// <summary>"No vehicles" only makes sense for a part with none in the table.</summary>
    private void OnRowsChanged(object? sender, EventArgs e)
    {
        if (_loading)
            return;

        bool none = _table.Rows.Count == 0;
        _noVehicles.Enabled = none;
        if (!none)
            _noVehicles.Checked = false;
    }

    private void OnPlayClick(object? sender, EventArgs e)
    {
        if (_current >= 0)
            _seekCamera?.Invoke(_parts[_current].Start);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _scanCancel?.Cancel();
        _player.Stop();
        base.OnFormClosed(e);
    }

    // ===================================================================== plates read by the scan

    /// <summary>The cars the plate scan found in the part shown, with their plate pictures.</summary>
    private void ShowCars()
    {
        _cars.BeginUpdate();
        try
        {
            _cars.Items.Clear();
            _readings.Items.Clear();
            _plateImages.Images.Clear();
            _partCars = _scan is not null && _current >= 0 ? _scan.CarsIn(_parts[_current], _duration) : new List<PlateCar>();

            if (_scan is null)
            {
                _carsTitle.Text = _createScanner is null
                    ? "Plates cannot be read here: ffmpeg.exe or the plate models (models folder) are missing."
                    : "No plates read yet: \"Read plates in every part\" (below) reads them in all parts of this video.";
            }
            else
            {
                _carsTitle.Text = _partCars.Count == 0
                    ? "No plates were read in this part."
                    : $"{_partCars.Count} car(s) read in this part. Check the reading in the player, then add it as a vehicle.";
            }

            for (int i = 0; i < _partCars.Count; i++)
            {
                var car = _partCars[i];
                _plateImages.Images.Add(Thumbnail(car.PlateJpeg));
                var item = new ListViewItem(car.Decided ? car.Plate : car.Plate + " ?", i);
                item.SubItems.Add(car.Movement);
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
        _colCarResult.Width = Math.Max(150, _cars.ClientSize.Width - others - 4);
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
        bool car = SelectedCar is not null;
        _useCar.Enabled = car;
        _useForSelected.Enabled = car;
        _useAll.Enabled = _partCars.Any(c => !_used.Contains(c));
    }

    /// <summary>Adds the selected car (with the selected reading) as a vehicle of this part.</summary>
    private void OnUseCarClick(object? sender, EventArgs e)
    {
        if (SelectedCar is not { } car)
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
            MessageBox.Show(this, "Select a vehicle in the table above first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _table.EndEdit();
        row.Cells[2].Value = SelectedReading(car);
        if (string.IsNullOrEmpty(row.Cells[1].Value as string) && car.Direction is { } direction)
            row.Cells[1].Value = direction == VehicleDirection.Coming ? "coming" : "going";
        MarkUsed(car);
    }

    /// <summary>Adds every car of the part not added yet, each with its plate (voted, or the best guess).</summary>
    private void OnUseAllClick(object? sender, EventArgs e)
    {
        int last = -1;
        foreach (var car in _partCars.Where(c => !_used.Contains(c)).ToList())
            last = AddCar(car, car.Plate);
        AfterAdding(last);
    }

    /// <summary>A new row for a car: its best read's time, its direction when known (else left to choose), and the plate.</summary>
    private int AddCar(PlateCar car, string plate)
    {
        _table.EndEdit();
        string? direction = car.Direction switch
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

    /// <summary>After adding: a row without a direction gets the direction cell, so it is chosen next.</summary>
    private void AfterAdding(int row)
    {
        if (row < 0)
            return;

        var missing = _table.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => string.IsNullOrEmpty(r.Cells[1].Value as string));
        if (missing is not null)
        {
            _table.CurrentCell = missing.Cells[1];
            _scanStatus.Text = "The scan could not tell the direction of a vehicle: choose coming or going in the table.";
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

    /// <summary>Reads the plates in every playing part (in the background), or stops a scan that is running.</summary>
    private async void OnReadPlatesClick(object? sender, EventArgs e)
    {
        if (_scanCancel is not null)
        {
            _scanCancel.Cancel();
            return;
        }

        if (_createScanner is not { } create || _parts.Count == 0)
            return;

        if (_scan is not null && MessageBox.Show(this,
                $"The plates of this video were read on {_scan.ScannedUtc.ToLocalTime():g}. Read them again? The new reading replaces the old one (labels are not changed).",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var cancel = _scanCancel = new CancellationTokenSource();
        var parts = _parts.ToList();
        string path = _videoPath;
        var info = _info;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long lastShown = 0;

        _readPlates.Text = "Stop reading";
        _scanProgress.Value = 0;
        _scanProgress.Visible = true;
        _scanStatus.Text = "Loading the plate models...";

        void Progress(double share, string message)
        {
            long now = watch.ElapsedMilliseconds;
            if (now - Interlocked.Read(ref lastShown) < 200 && share < 1)
                return;
            Interlocked.Exchange(ref lastShown, now);

            if (IsDisposed || !IsHandleCreated)
                return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;
                _scanProgress.Value = Math.Max(0, Math.Min(100, (int)(share * 100)));
                _scanStatus.Text = $"Reading plates, {share * 100:0}%: {message}";
            }));
        }

        try
        {
            var scan = await Task.Run(() =>
            {
                using var scanner = create();
                return scanner.Scan(path, info, parts, Progress, cancel.Token);
            }, cancel.Token);

            if (IsDisposed)
                return;

            _scan = scan;
            _used.Clear();
            string saved = "";
            try
            {
                PlateScanFile.Save(path, scan);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                saved = $" They could not be saved next to the video ({ex.Message}).";
            }

            _scanStatus.Text = $"Read {scan.Cars.Count} car(s) in {parts.Count} part(s) in {watch.Elapsed.TotalSeconds:0} s.{saved}";
            RefreshParts();
            ShowCars();
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
                _scanStatus.Text = _scan is null ? "Stopped." : "Stopped; the earlier reading is kept.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _scanStatus.Text = "Reading plates failed.";
                MessageBox.Show(this, "The plates could not be read:\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _scanCancel = null;
            cancel.Dispose();
            if (!IsDisposed)
            {
                _readPlates.Text = "Read plates in every part";
                _scanProgress.Visible = false;
            }
        }
    }

    // ===================================================================== moving between parts

    private void OnNextUnlabelledClick(object? sender, EventArgs e) => GoToUnlabelled(+1);

    private void OnPreviousUnlabelledClick(object? sender, EventArgs e) => GoToUnlabelled(-1);

    private void GoToUnlabelled(int step)
    {
        if (!CommitCurrent())
            return;

        var groups = Groups();
        for (int n = 1; n <= groups.Count; n++)
        {
            int i = ((_current + step * n) % groups.Count + groups.Count) % groups.Count;
            if (!groups[i].IsLabelled)
            {
                Select(i);
                return;
            }
        }

        MessageBox.Show(this, "Every playing part is labelled.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ===================================================================== tidying and export

    private void OnDeleteOutsideClick(object? sender, EventArgs e)
    {
        if (!CommitCurrent())
            return;

        int count = VehicleLabels.CountOutside(_parts, _vehicles, _empty, _duration);
        if (count == 0)
            return;

        if (MessageBox.Show(this, $"{count} label(s) are in parts that are skipped now, so they are never streamed. Delete them?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _vehicles.RemoveAll(v => !_parts.Any(p => p.Contains(v.Time, _duration)));
        _empty.RemoveAll(t => !_parts.Any(p => p.Contains(t, _duration)));
        RefreshParts();
    }

    private void OnExportClick(object? sender, EventArgs e)
    {
        if (!CommitCurrent())
            return;

        try
        {
            using var dialog = new SaveFileDialog
            {
                Title = $"Export labels of {_videoName}",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = Path.GetFileNameWithoutExtension(_videoName) + "-labels.csv"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                File.WriteAllText(dialog.FileName, VehicleLabels.ToCsv(Groups()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Export labels", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        if (!CommitCurrent())
            return;

        Vehicles = VehicleLabels.Normalize(_vehicles, _duration);
        EmptyParts = VehicleLabels.NormalizeEmpty(_empty, _duration);
        DialogResult = DialogResult.OK;
    }
}
