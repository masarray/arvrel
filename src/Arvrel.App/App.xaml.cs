using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        base.OnExit(e);
    }

    private static void App_DispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        var logPath = WriteCrashLog("WPF dispatcher", e.Exception);
        try
        {
            MessageBox.Show(
                $"ARVREL encountered an unhandled error and must close.\n\n" +
                $"Diagnostic log:\n{logPath}\n\n" +
                $"{e.Exception.GetType().Name}: {e.Exception.Message}",
                "ARVREL startup/runtime error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // The dispatcher or desktop session may already be unavailable.
        }

        // Preserve fail-fast behavior and the non-zero process exit code.
        e.Handled = false;
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException($"Non-Exception unhandled object: {e.ExceptionObject}");
        WriteCrashLog("AppDomain", exception);
    }

    private static string WriteCrashLog(string source, Exception exception)
    {
        string directory;
        try
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ARVREL",
                "logs");
            Directory.CreateDirectory(directory);
        }
        catch
        {
            directory = Path.GetTempPath();
        }

        var path = Path.Combine(directory, "arvrel-crash.log");
        var entry = new StringBuilder()
            .AppendLine(new string('=', 88))
            .AppendLine($"Timestamp : {DateTimeOffset.Now:O}")
            .AppendLine($"Source    : {source}")
            .AppendLine($"Process   : {Environment.ProcessPath}")
            .AppendLine($"Runtime   : {Environment.Version}")
            .AppendLine($"OS        : {Environment.OSVersion}")
            .AppendLine()
            .AppendLine(exception.ToString())
            .AppendLine()
            .ToString();

        try
        {
            File.AppendAllText(path, entry, Encoding.UTF8);
            return path;
        }
        catch
        {
            return "Crash log could not be written.";
        }
    }
}
