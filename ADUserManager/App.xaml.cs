using System.Globalization;
using System.IO;
using System.Windows;
using ActiveManager.Localization;
using ActiveManager.Services;

namespace ActiveManager;

public partial class App : Application
{
    private static readonly string CrashLogPath = Path.Combine(
        AppContext.BaseDirectory, "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        var lang = AppSettings.Instance.Language;
        TranslationSource.Instance.CurrentCulture = new CultureInfo(lang == "lt" ? "lt" : "en");

        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            LogToFile("UI error", args.Exception);
            ErrorLogger.Log("Unhandled UI error", args.Exception);
            MessageBox.Show($"Error: {args.Exception.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogToFile("Critical error", ex);
                ErrorLogger.Log("Critical error", ex);
            }
        };
    }

    private static void LogToFile(string source, Exception ex)
    {
        try
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(CrashLogPath, msg);
        }
        catch { }
    }
}
