using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace ActiveManager.Localization;

public class TranslationSource : INotifyPropertyChanged
{
    private static readonly TranslationSource _instance = new();
    public static TranslationSource Instance => _instance;

    private readonly ResourceManager _resourceManager =
        new("ActiveManager.Localization.Strings", typeof(TranslationSource).Assembly);

    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("en");

    public string this[string key]
    {
        get
        {
            try { return _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]"; }
            catch { return $"[{key}]"; }
        }
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            _currentCulture = value;
            // Notify WPF bindings that all indexer values changed
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            CultureChanged?.Invoke();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised after CurrentCulture is changed. Code-behind pages can subscribe to this
    /// in Loaded and unsubscribe in Unloaded to refresh strings set imperatively.
    /// </summary>
    public event Action? CultureChanged;
}
