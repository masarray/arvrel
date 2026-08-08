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
        InstallUxFoundationResources();
        base.OnStartup(e);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // StartupUri owns MainWindow creation. Activation is the first explicit
        // application lifecycle point at which that instance is guaranteed to
        // exist. MainWindow handles the rare not-yet-loaded case itself.
        if (MainWindow is Arvrel.App.MainWindow window)
        {
            window.InitializeP6VirtualRelay();
            window.InitializeGlobalUxFoundation();
            window.InitializeRelayReadabilityHotfix();
            window.InitializeProductReadyInjectionUx();
            window.InitializeMultiIedWorkspace();
            window.ApplyAvrP02ShellPolish();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        base.OnExit(e);
    }

    private void InstallUxFoundationResources()
    {
        if (Resources.MergedDictionaries.Any(dictionary =>
                dictionary.Source?.OriginalString.Contains("ArvrelUxFoundation.xaml", StringComparison.OrdinalIgnoreCase) == true))
            return;

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/ARVREL;component/Themes/ArvrelUxFoundation.xaml",
                UriKind.Absolute)
        });
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
