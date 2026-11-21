namespace MockRtsp;

/// <summary>
/// Labels the playing parts of one video with the vehicles in them: for each part, every vehicle
/// with its direction (coming or going) and its plate, or "no vehicles". This is the ground truth a
/// vehicle detector (for example the traffic monitor) is checked against. The list shows every
/// playing part and how many are labelled; "Next to label" walks through the rest, and "Play this
/// part" jumps the camera there so the part can be watched while labelling.
/// The layout lives in LabelsDialog.Designer.cs.
/// </summary>
public sealed partial class LabelsDialog : Form
{
    private readonly double _duration;
    private readonly Func<double>? _playhead;
    private readonly Action<double>? _seek;
    private readonly string _videoName;
    private readonly List<PlayingPart> _parts;
    private readonly List<VehicleMark> _vehicles;   // all of them, including any in skipped parts
    private readonly List<double> _empty;           // moments in parts checked as having no vehicles
    private int _current = -1;
    private bool _loading;

    /// <summary>Parameterless constructor for the designer.</summary>
    public LabelsDialog() : this("video", Array.Empty<SkipRange>(), 60, Array.Empty<VehicleMark>(), Array.Empty<double>(), null, null, null)
    {
    }

    /// <param name="playhead">Where the camera is in this video now, or null when it plays another video.</param>
    /// <param name="showAt">A moment whose part is shown first, or null for the part at the playhead.</param>
    /// <param name="seek">Jumps the camera to a moment in this video, or null when it plays another video.</param>
    public LabelsDialog(string videoName, IReadOnlyList<SkipRange> skips, double durationSeconds,
        IReadOnlyList<VehicleMark> vehicles, IReadOnlyList<double> emptyParts,
        Func<double>? playhead, double? showAt, Action<double>? seek)
    {
        InitializeComponent();

        _videoName = videoName;
        _duration = durationSeconds;
        _playhead = playhead;
        _seek = seek;
        _parts = VehicleLabels.PlayingParts(skips, durationSeconds);
        _vehicles = vehicles.ToList();
        _empty = emptyParts.ToList();

        Text = $"Labels in {videoName}";
        _play.Enabled = seek is not null;
        _playNote.Text = seek is null
            ? "The camera is playing another video, so it cannot jump here."
            : "Jumps the camera to the start of this part, to watch it (for example in VLC).";

        FillParts();

        double? around = showAt ?? playhead?.Invoke();
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
            item.SubItems[4].Text = VehicleLabels.Describe(groups[i]);
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

        // At the camera's position when it is in this part, otherwise in the middle of the part.
        var part = _parts[_current];
        double time = _playhead?.Invoke() is double now && part.Contains(now, _duration) ? now : part.Middle;

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
            _seek?.Invoke(_parts[_current].Start);
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
