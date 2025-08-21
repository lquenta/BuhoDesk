using System.Globalization;
using System.Resources;
using System.Windows;

namespace BuhoShared.Services;

public class LocalizationService
{
    private static LocalizationService? _instance;
    private static readonly object _lockObject = new();
    
    private ResourceManager _resourceManager;
    private CultureInfo _currentCulture;
    
    public event EventHandler? LanguageChanged;

    public static LocalizationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObject)
                {
                    _instance ??= new LocalizationService();
                }
            }
            return _instance;
        }
    }

    private LocalizationService()
    {
        _resourceManager = new ResourceManager("BuhoShared.Resources.Strings", typeof(LocalizationService).Assembly);
        _currentCulture = CultureInfo.CurrentCulture;
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                CultureInfo.CurrentCulture = value;
                CultureInfo.CurrentUICulture = value;
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string GetString(string key)
    {
        try
        {
            return _resourceManager.GetString(key, _currentCulture) ?? key;
        }
        catch
        {
            return key;
        }
    }

    public string GetString(string key, params object[] args)
    {
        try
        {
            var format = _resourceManager.GetString(key, _currentCulture) ?? key;
            return string.Format(format, args);
        }
        catch
        {
            return key;
        }
    }

    public void SetLanguage(string languageCode)
    {
        try
        {
            var culture = new CultureInfo(languageCode);
            CurrentCulture = culture;
        }
        catch
        {
            // Fallback to default culture if language code is invalid
            CurrentCulture = CultureInfo.InvariantCulture;
        }
    }

    public void SetLanguage(CultureInfo culture)
    {
        CurrentCulture = culture;
    }

    public List<CultureInfo> GetSupportedCultures()
    {
        return new List<CultureInfo>
        {
            new CultureInfo("en"), // English
            new CultureInfo("es")  // Spanish
        };
    }

    public string GetLanguageDisplayName(CultureInfo culture)
    {
        return culture.NativeName;
    }

    public string GetLanguageDisplayName(string languageCode)
    {
        try
        {
            var culture = new CultureInfo(languageCode);
            return culture.NativeName;
        }
        catch
        {
            return languageCode;
        }
    }
}

public static class LocalizationExtensions
{
    public static string Localize(this string key)
    {
        return LocalizationService.Instance.GetString(key);
    }

    public static string Localize(this string key, params object[] args)
    {
        return LocalizationService.Instance.GetString(key, args);
    }
}
