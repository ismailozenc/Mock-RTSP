namespace MockRtsp;

/// <summary>
/// Scores an ALPR system's results against the ground-truth log (see PlateScorer) and shows
/// the report. The layout lives in ScoreDialog.Designer.cs.
/// </summary>
public sealed partial class ScoreDialog : Form
{
    private ScoreResult? _result;

    /// <summary>Parameterless constructor for the designer.</summary>
    public ScoreDialog() : this(string.Empty)
    {
    }

    /// <param name="groundTruthFile">The log to start with, usually today's.</param>
    public ScoreDialog(string groundTruthFile)
    {
        InitializeComponent();
        _truthFile.Text = groundTruthFile;
    }

    private void OnBrowseTruthClick(object? sender, EventArgs e) =>
        Browse(_truthFile, "Ground-truth log", "Ground-truth logs (ground-truth-*.csv)|ground-truth-*.csv|CSV files (*.csv)|*.csv");

    private void OnBrowseResultsClick(object? sender, EventArgs e) =>
        Browse(_resultsFile, "ALPR results", "CSV files (*.csv)|*.csv|All files (*.*)|*.*");

    private void Browse(TextBox box, string title, string filter)
    {
        using var dialog = new OpenFileDialog { Title = title, Filter = filter };

        string folder = Path.GetDirectoryName(box.Text) ?? string.Empty;
        if (Directory.Exists(folder))
            dialog.InitialDirectory = folder;

        if (dialog.ShowDialog(this) == DialogResult.OK)
            box.Text = dialog.FileName;
    }

    private void OnScoreClick(object? sender, EventArgs e)
    {
        try
        {
            if (!File.Exists(_truthFile.Text))
                throw new FileNotFoundException("Pick the ground-truth log first.");
            if (!File.Exists(_resultsFile.Text))
                throw new FileNotFoundException("Pick your ALPR system's results file first.");

            var truth = GroundTruthLog.Read(_truthFile.Text);
            var reads = PlateScorer.ReadResults(_resultsFile.Text);
            _result = PlateScorer.Score(truth, reads, (double)_before.Value, (double)_after.Value);

            _report.Text = PlateScorer.Summary(_result).ReplaceLineEndings(Environment.NewLine);
            _saveReport.Enabled = true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _result = null;
            _saveReport.Enabled = false;
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnSaveReportClick(object? sender, EventArgs e)
    {
        if (_result is null)
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Save the report",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = "alpr-score-" + DateTime.Now.ToString("yyyy-MM-dd-HHmm") + ".csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, PlateScorer.ToCsv(_result));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
