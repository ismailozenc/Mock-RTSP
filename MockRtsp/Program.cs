namespace MockRtsp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Last-resort handlers. Anything that escapes a try/catch lands here,
        // is written to the error log, and the user is told what happened.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, e) =>
            ErrorReporter.Report(e.Exception, "UI thread", showDialog: true);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ErrorReporter.Report(
                e.ExceptionObject as Exception
                    ?? new InvalidOperationException(e.ExceptionObject?.ToString() ?? "Unknown error"),
                "Unhandled exception",
                showDialog: true);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ErrorReporter.Report(e.Exception, "Unobserved task");
            e.SetObserved();
        };

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
