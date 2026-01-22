using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using SlimeTodo.Services;
using SlimeTodo.ViewModels;

namespace SlimeTodo.Views;

public partial class NoteEditorView : UserControl
{
    private bool _webViewInitialized;
    private bool _isEditorReady;
    private string? _pendingContent;
    private TaskItemViewModel? _currentTask;

    public NoteEditorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();

        // Subscribe to SelectedTask changes
        var window = Window.GetWindow(this);
        if (window?.DataContext is MainViewModel mainVm)
        {
            mainVm.PropertyChanged += OnMainViewModelPropertyChanged;
            LoadCurrentTaskContent(mainVm.SelectedTask);
        }

        // Subscribe to theme changes
        ThemeService.Instance.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window?.DataContext is MainViewModel mainVm)
        {
            mainVm.PropertyChanged -= OnMainViewModelPropertyChanged;
        }

        // Unsubscribe from theme changes
        ThemeService.Instance.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(bool isDark)
    {
        SetTheme(isDark);
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTask) && sender is MainViewModel mainVm)
        {
            LoadCurrentTaskContent(mainVm.SelectedTask);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        if (_webViewInitialized) return;

        try
        {
            await QuillEditor.EnsureCoreWebView2Async();
            _webViewInitialized = true;

            // Handle messages from JavaScript
            QuillEditor.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Navigate to the Quill editor HTML
            var editorPath = GetEditorPath();
            if (File.Exists(editorPath))
            {
                QuillEditor.CoreWebView2.Navigate(new Uri(editorPath).AbsoluteUri);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[NoteEditor] Editor HTML not found: {editorPath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NoteEditor] WebView2 init failed: {ex.Message}");
        }
    }

    private string GetEditorPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, "Resources", "Editor", "index.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = e.WebMessageAsJson;
            var json = JsonDocument.Parse(message);
            var root = json.RootElement;

            if (root.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();

                switch (type)
                {
                    case "ready":
                        _isEditorReady = true;
                        // Apply current theme
                        SetTheme(ThemeService.Instance.IsDarkMode);
                        // Load pending content if any
                        if (_pendingContent != null)
                        {
                            SetEditorContent(_pendingContent);
                            _pendingContent = null;
                        }
                        break;

                    case "save":
                        if (root.TryGetProperty("content", out var contentElement))
                        {
                            var content = contentElement.GetString();
                            SaveContent(content);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NoteEditor] Message parse error: {ex.Message}");
        }
    }

    private void LoadCurrentTaskContent(TaskItemViewModel? task)
    {
        _currentTask = task;

        if (task == null)
        {
            SetEditorContent(string.Empty);
            return;
        }

        var content = task.Notes ?? string.Empty;
        SetEditorContent(content);
    }

    private void SetEditorContent(string content)
    {
        if (!_webViewInitialized || !_isEditorReady)
        {
            _pendingContent = content;
            return;
        }

        try
        {
            // Escape content for JavaScript
            var escapedContent = JsonSerializer.Serialize(content);
            var script = $"window.setContent({escapedContent});";
            QuillEditor.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NoteEditor] SetContent error: {ex.Message}");
        }
    }

    private void SaveContent(string? content)
    {
        if (_currentTask == null) return;

        // Check if content is essentially empty (Quill's empty state)
        var isEmpty = string.IsNullOrWhiteSpace(content) ||
                      content == "<p><br></p>" ||
                      content == "<p></p>";

        _currentTask.Notes = isEmpty ? string.Empty : content ?? string.Empty;
    }

    public void FocusEditor()
    {
        if (_webViewInitialized && _isEditorReady)
        {
            try
            {
                QuillEditor.CoreWebView2.ExecuteScriptAsync("window.focusEditor();");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NoteEditor] Focus error: {ex.Message}");
            }
        }
    }

    private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Enter in title box - focus Quill editor
            FocusEditor();
            e.Handled = true;
        }
    }

    public void SetTheme(bool isDark)
    {
        if (_webViewInitialized && _isEditorReady)
        {
            try
            {
                var script = $"window.setTheme({(isDark ? "true" : "false")});";
                QuillEditor.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NoteEditor] SetTheme error: {ex.Message}");
            }
        }
    }
}
