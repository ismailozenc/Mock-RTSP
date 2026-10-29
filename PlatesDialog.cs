using System.Globalization;

namespace MockRtsp;

/// <summary>
/// Edits the ground-truth plates of one video: a table of time, plate and an optional region,
/// with Add at playhead and CSV import and export. The layout lives in PlatesDialog.Designer.cs.
/// </summary>
public sealed partial class PlatesDialog : Form
{
    private readonly double _duration;
    private readonly double _playhead;
    private readonly string _videoName;

    /// <summary>Parameterless constructor for the designer.</summary>
    public PlatesDialog() : this("video", Array.Empty<PlateMark>(), 0, 0)
    {
    }

    /// <param name="playheadSeconds">Where the camera is in this video now, for Add at playhead.</param>
    public PlatesDialog(string videoName, IReadOnlyList<PlateMark> plates, double playheadSeconds, double durationSeconds)
    {
        InitializeComponent();

        _videoName = videoName;
        _duration = durationSeconds;
        _playhead = playheadSeconds;
        Text = $"Plates in {videoName}";
        _addAtPlayhead.Text = $"Add at playhead ({PlateMarks.FormatTime(playheadSeconds)})";

        Fill(plates);
    }

    /// <summary>The plates, sorted by time. Read after OK.</summary>
    public IReadOnlyList<PlateMark> Plates { get; private set; } = Array.Empty<PlateMark>();

    private void Fill(IEnumerable<PlateMark> plates)
    {
        _table.Rows.Clear();
        foreach (var p in plates)
            _table.Rows.Add(PlateMarks.FormatTime(p.Time), p.Plate, Num(p.X), Num(p.Y), Num(p.Width), Num(p.Height));
    }

    private static string Num(double? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Reads the table. Throws InvalidOperationException naming the row that is wrong.</summary>
    private List<PlateMark> ReadTable()
    {
        var plates = new List<PlateMark>();

        foreach (DataGridViewRow row in _table.Rows)
        {
            if (row.IsNewRow)
                continue;

            string Cell(int i) => row.Cells[i].Value?.ToString()?.Trim() ?? string.Empty;
            if (Enumerable.Range(0, 6).All(i => Cell(i).Length == 0))
                continue;

            int number = row.Index + 1;
            if (!PlateMarks.TryParseTime(Cell(0), out double time))
                throw new InvalidOperationException($"Row {number}: '{Cell(0)}' is not a time. Use seconds (83.4) or minutes and seconds (1:23.4).");

            double? Value(int i)
            {
                string text = Cell(i).Replace(',', '.');
                if (text.Length == 0)
                    return null;

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                    ? v
                    : throw new InvalidOperationException($"Row {number}: '{Cell(i)}' is not a number.");
            }

            var plate = new PlateMark(time, Cell(1), Value(2), Value(3), Value(4), Value(5));
            if (PlateMarks.Check(plate, _duration) is { } problem)
                throw new InvalidOperationException($"Row {number}: {problem}.");

            plates.Add(plate);
        }

        return PlateMarks.Normalize(plates, _duration);
    }

    private void OnAddAtPlayheadClick(object? sender, EventArgs e)
    {
        int index = _table.Rows.Add(PlateMarks.FormatTime(_playhead), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        _table.CurrentCell = _table.Rows[index].Cells[1];
        _table.BeginEdit(true);
    }

    private void OnRemoveClick(object? sender, EventArgs e)
    {
        foreach (DataGridViewRow row in _table.SelectedRows)
        {
            if (!row.IsNewRow)
                _table.Rows.Remove(row);
        }
    }

    private void OnImportClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = $"Import plates for {_videoName}",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var plates = PlateMarks.FromCsv(File.ReadAllText(dialog.FileName), _duration);
            Fill(plates);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Import plates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnExportClick(object? sender, EventArgs e)
    {
        try
        {
            var plates = ReadTable();
            using var dialog = new SaveFileDialog
            {
                Title = $"Export plates of {_videoName}",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = Path.GetFileNameWithoutExtension(_videoName) + "-plates.csv"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                File.WriteAllText(dialog.FileName, PlateMarks.ToCsv(plates));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Export plates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        _table.EndEdit();

        try
        {
            Plates = ReadTable();
            DialogResult = DialogResult.OK;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
