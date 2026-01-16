using System.Windows;

namespace SlimeTodo.Services;

public class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private bool _isDarkMode;
    private readonly StorageService _storageService;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                ApplyTheme();
                SaveTheme();
                ThemeChanged?.Invoke(_isDarkMode);
            }
        }
    }

    public event Action<bool>? ThemeChanged;

    private ThemeService()
    {
        _storageService = new StorageService();
        LoadTheme();
    }

    private void LoadTheme()
    {
        var data = _storageService.Load();
        _isDarkMode = data.IsDarkMode;
        ApplyTheme();
    }

    private void SaveTheme()
    {
        var data = _storageService.Load();
        data.IsDarkMode = _isDarkMode;
        _storageService.Save(data);
    }

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        var themePath = _isDarkMode
            ? "Resources/DarkTheme.xaml"
            : "Resources/Styles.xaml";

        var newTheme = new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        };

        // 기존 테마 제거하고 새 테마 적용
        var mergedDicts = app.Resources.MergedDictionaries;

        // 첫 번째 딕셔너리 (테마)를 교체
        if (mergedDicts.Count > 0)
        {
            mergedDicts[0] = newTheme;
        }
        else
        {
            mergedDicts.Insert(0, newTheme);
        }
    }
}
