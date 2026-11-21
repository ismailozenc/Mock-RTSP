namespace MockRtsp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ErrorReporter.Report(e.Exception, "UI thread", showDialog: true);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ErrorReporter.Report(
                e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString() ?? "Unknown error"),
                "Unhandled exception",
                showDialog: true);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ErrorReporter.Report(e.Exception, "Unobserved task");
            e.SetObserved();
        };

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // One scanner at a time: two would both save the same videos' labels and gaps.
        using var single = new Mutex(true, "MockRtspScanner.SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("Mock RTSP Scanner is already running.", "Mock RTSP Scanner", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new ScannerForm());
    }
}
