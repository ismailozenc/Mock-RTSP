using System.Text;

namespace MockRtsp;

/// <summary>
/// Central place for reporting errors. Writes them to a log file and, when asked,
/// shows a dialog. Nothing in here is allowed to throw, because it is called from
/// exception handlers.
/// </summary>
public static class ErrorReporter
{
    private static readonly object Gate = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MockRtsp",
        "errors.log");

    public static void Report(Exception ex, string context, bool showDialog = false)
    {
        try
        {
            WriteLog(ex, context);
        }
        catch
        {
            // Logging failed (disk full, no permission). There is nowhere else to report it.
        }

        if (showDialog)
        {
            ShowDialog(context, ex.Message);
        }
    }

    private static void WriteLog(Exception ex, string context)
    {
        var text = new StringBuilder()
            .Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ")
            .AppendLine(context)
            .AppendLine(ex.ToString())
            .AppendLine()
            .ToString();

        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, text);
        }
    }

    private static void ShowDialog(string title, string message)
    {
        try
        {
            var owner = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Visible && !f.IsDisposed);
            Action show = () => MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

            if (owner is not null && owner.InvokeRequired)
                owner.Invoke(show);
            else
                show();
        }
        catch
        {
            // Could not show a dialog. The error is already in the log.
        }
    }
}
