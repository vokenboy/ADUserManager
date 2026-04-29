using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ActiveManager.Helpers;
using ActiveManager.Services;

namespace ActiveManager.Views.Pages;

public partial class ErrorLogPage : Page, IServiceConsumer
{
    public ObservableCollection<ErrorDisplayItem> Entries { get; } = new();

    public ErrorLogPage()
    {
        InitializeComponent();
        ErrorGrid.ItemsSource = Entries;

        foreach (var entry in ErrorLogger.LogEntries)
        {
            Entries.Add(new ErrorDisplayItem(entry));
        }

        ErrorLogger.EntryAdded += OnEntryAdded;
    }

    public void SetServices(UserService? adService, ITerminationService? terminationService, DatabaseService databaseService, Views.MainWindow mainWindow)
    {
    }

    private void OnEntryAdded(ErrorLogEntry entry)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Entries.Add(new ErrorDisplayItem(entry));
        });
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        ErrorLogger.Clear();
        Entries.Clear();
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ErrorGrid.SelectedItem is not ErrorDisplayItem item) return;

        var details = $"Time: {item.Entry.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                      $"Source: {item.Entry.Source}\n" +
                      $"Message: {item.Entry.Message}\n\n" +
                      $"Stack Trace:\n{item.Entry.StackTrace ?? "None"}";
        System.Windows.MessageBox.Show(details, "Error Details",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public class ErrorDisplayItem
{
    public ErrorLogEntry Entry { get; }

    public string TimestampText => Entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string Source => Entry.Source;
    public string Message => Entry.Message;

    public ErrorDisplayItem(ErrorLogEntry entry)
    {
        Entry = entry;
    }
}
