using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlimeTodo.Models;
using SlimeTodo.Services;
using SlimeTodo.ViewModels;
using static SlimeTodo.Services.ThemeService;

namespace SlimeTodo;

public partial class MainWindow : Window
{
    private readonly TrayIconService _trayIconService;
    private bool _isExiting;
    private bool _isShowingGoodbye;
    private WeekViewModel? _weekViewModel;
    private DispatcherTimer? _ctrlHoldTimer;

    public MainWindow()
    {
        InitializeComponent();

        // 가지 아이콘 설정 (현재 테마에 맞게)
        UpdateWindowIcon(ThemeService.Instance.IsDarkMode);

        // 테마 변경 시 아이콘 업데이트
        ThemeService.Instance.ThemeChanged += UpdateWindowIcon;

        _trayIconService = new TrayIconService();
        _trayIconService.ShowWindowRequested += ShowFromTray;
        _trayIconService.ExitRequested += ExitApplication;
        _trayIconService.Show();

        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;

        // Ctrl 키 상태 감지 (2초 후 힌트 표시)
        PreviewKeyDown += MainWindow_PreviewKeyDown_Ctrl;
        PreviewKeyUp += MainWindow_PreviewKeyUp_Ctrl;
        Deactivated += MainWindow_Deactivated;

        // Ctrl 키 2초 홀드 타이머 초기화
        _ctrlHoldTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _ctrlHoldTimer.Tick += CtrlHoldTimer_Tick;
    }

    private void CtrlHoldTimer_Tick(object? sender, EventArgs e)
    {
        _ctrlHoldTimer?.Stop();
        if (DataContext is MainViewModel vm)
        {
            vm.ShowShortcuts = true;
        }
    }

    private void MainWindow_PreviewKeyDown_Ctrl(object sender, KeyEventArgs e)
    {
        // Ctrl 키가 눌렸을 때 타이머 시작 (2초 후 힌트 표시)
        if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && DataContext is MainViewModel vm)
        {
            if (!e.IsRepeat && _ctrlHoldTimer != null && !_ctrlHoldTimer.IsEnabled)
            {
                _ctrlHoldTimer.Start();
            }
        }
    }

    private void MainWindow_PreviewKeyUp_Ctrl(object sender, KeyEventArgs e)
    {
        // Ctrl 키가 떼어졌을 때 타이머 정지 및 힌트 숨김
        if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && DataContext is MainViewModel vm)
        {
            _ctrlHoldTimer?.Stop();
            vm.ShowShortcuts = false;
        }
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        // 창이 비활성화되면 타이머 정지 및 힌트 숨김
        _ctrlHoldTimer?.Stop();
        if (DataContext is MainViewModel vm)
        {
            vm.ShowShortcuts = false;
        }
    }

    private void UpdateWindowIcon(bool isDarkMode)
    {
        Icon = CreateEggplantIcon(isDarkMode);
    }

    private static BitmapSource CreateEggplantIcon(bool isDarkMode)
    {
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 다크 모드: 밝은 색상, 라이트 모드: 진한 색상
            var stemColor = isDarkMode
                ? System.Drawing.Color.FromArgb(120, 200, 120)  // 밝은 초록
                : System.Drawing.Color.FromArgb(76, 153, 76);   // 진한 초록

            var bodyColor = isDarkMode
                ? System.Drawing.Color.FromArgb(160, 100, 200)  // 밝은 보라
                : System.Drawing.Color.FromArgb(102, 51, 153);  // 진한 보라

            var eyeColor = isDarkMode
                ? System.Drawing.Color.FromArgb(255, 255, 255)  // 흰색
                : System.Drawing.Color.FromArgb(245, 245, 245); // 약간 어두운 흰색

            // 가지 꼭지
            using var stemBrush = new SolidBrush(stemColor);
            g.FillEllipse(stemBrush, 10, 0, 12, 10);

            // 가지 몸통
            using var bodyBrush = new SolidBrush(bodyColor);
            g.FillEllipse(bodyBrush, 4, 6, 24, 24);

            // 눈
            using var eyeBrush = new SolidBrush(eyeColor);
            g.FillEllipse(eyeBrush, 9, 14, 5, 5);
            g.FillEllipse(eyeBrush, 18, 14, 5, 5);
        }

        var handle = bitmap.GetHicon();
        var icon = Imaging.CreateBitmapSourceFromHIcon(
            handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        return icon;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += ViewModel_PropertyChanged;
            vm.RequestSearchFocus += FocusSearchBox;
            vm.GoodbyeRequested += ShowGoodbyeAndClose;
            UpdateTrayTooltip(vm);

            // Initialize WeekViewModel
            _weekViewModel = new WeekViewModel(
                vm.GetTaskService(),
                vm.GetUndoService(),
                () => _weekViewModel?.LoadWeekData(),
                () => _weekViewModel?.LoadWeekData(),
                (showCompleted) => vm.ShowCompleted = showCompleted  // WeekView -> MainViewModel 동기화
            );
            WeekView.DataContext = _weekViewModel;

            // 할일 변경 시 WeekView 자동 갱신
            vm.GetTaskService().DataChanged += () =>
            {
                if (vm.CurrentView == ViewType.Week)
                {
                    _weekViewModel?.LoadWeekData();
                }
            };
        }
    }

    private void FocusSearchBox()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is MainViewModel vm && vm.CurrentView == ViewType.Week)
            {
                WeekView.FocusSearchBox();
            }
            else
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            }
        });
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _trayIconService.Dispose();
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        if (_isShowingGoodbye) return;

        if (DataContext is MainViewModel vm)
        {
            _isShowingGoodbye = true;
            Show();
            WindowState = WindowState.Normal;
            vm.ShowGoodbye();
        }
        else
        {
            _isExiting = true;
            Close();
        }
    }

    private void ShowGoodbyeAndClose(string message)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PetMessage = message;
            vm.ShowMessage = true;

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.2)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _isExiting = true;
                Close();
            };
            timer.Start();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.TodayTotal) ||
            e.PropertyName == nameof(MainViewModel.TodayCompleted))
        {
            if (DataContext is MainViewModel vm)
            {
                UpdateTrayTooltip(vm);
            }
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentView))
        {
            if (DataContext is MainViewModel vm && vm.CurrentView == ViewType.Week)
            {
                _weekViewModel?.LoadWeekData();
            }
        }

        // ShowCompleted 동기화: MainViewModel -> WeekViewModel
        if (e.PropertyName == nameof(MainViewModel.ShowCompleted))
        {
            if (DataContext is MainViewModel vm && _weekViewModel != null)
            {
                _weekViewModel.SyncShowCompleted(vm.ShowCompleted);
            }
        }
    }

    private void UpdateTrayTooltip(MainViewModel vm)
    {
        _trayIconService.UpdateTooltip(vm.TodayTotal, vm.TodayCompleted);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Escape - close help modal, note panel, or search
        if (e.Key == Key.Escape)
        {
            if (vm.ShowHelpModal)
            {
                vm.ShowHelpModal = false;
                e.Handled = true;
                return;
            }
            if (vm.IsNotePanelVisible)
            {
                vm.CloseNotePanelCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (vm.IsSearchMode)
            {
                vm.CloseSearchCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Ctrl shortcuts
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.D1:
                    vm.NavigateCommand.Execute("Inbox");
                    e.Handled = true;
                    return;
                case Key.D2:
                    vm.NavigateCommand.Execute("Today");
                    e.Handled = true;
                    return;
                case Key.D3:
                    vm.NavigateCommand.Execute("Week");
                    e.Handled = true;
                    return;
                case Key.D4:
                    vm.NavigateCommand.Execute("Upcoming");
                    e.Handled = true;
                    return;
                case Key.N:
                    ToggleQuickAdd();
                    e.Handled = true;
                    return;
                case Key.D:
                    ThemeService.Instance.ToggleTheme();
                    e.Handled = true;
                    return;
                case Key.F:
                    vm.ToggleSearch();
                    e.Handled = true;
                    return;
                case Key.Z:
                    // Don't intercept Ctrl+Z when note editor (Quill) is focused
                    if (!IsNoteEditorFocused())
                    {
                        vm.PerformUndo();
                        e.Handled = true;
                    }
                    return;
            }
        }

        // Quick Add Focus: Q
        if (e.Key == Key.Q && !IsTextBoxFocused())
        {
            ToggleQuickAdd();
            e.Handled = true;
        }
        // Toggle Show Completed: H
        else if (e.Key == Key.H && !IsTextBoxFocused())
        {
            vm.ToggleShowCompletedCommand.Execute(null);
            e.Handled = true;
        }
        // Toggle Note Panel: M
        else if (e.Key == Key.M && !IsTextBoxFocused())
        {
            if (vm.IsNotePanelVisible)
            {
                vm.CloseNotePanelCommand.Execute(null);
            }
            else if (vm.SelectedTask != null)
            {
                // Note panel opens automatically when task is selected
                // Focus the note editor
                NoteEditor?.FocusEditor();
            }
            e.Handled = true;
        }
        // J/K for list navigation
        else if ((e.Key == Key.J || e.Key == Key.K) && !IsTextBoxFocused())
        {
            TaskListView.FocusList();
            e.Handled = true;
        }
    }

    private bool IsTextBoxFocused()
    {
        return Keyboard.FocusedElement is System.Windows.Controls.TextBox;
    }

    private bool IsNoteEditorFocused()
    {
        // Check if Note Editor (WebView2) has focus
        if (DataContext is MainViewModel vm && vm.IsNotePanelVisible)
        {
            var focusedElement = Keyboard.FocusedElement;

            // If a TextBox is focused, check if it's inside the NoteEditor (title box)
            if (focusedElement is System.Windows.Controls.TextBox tb)
            {
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(tb);
                while (parent != null)
                {
                    if (parent is Views.NoteEditorView)
                        return true;
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
                // TextBox is focused but not in NoteEditor - app's textbox has focus
                return false;
            }

            // No TextBox focused - if note panel is visible, assume WebView2 has focus
            // WebView2 doesn't report focus correctly to WPF, so we use this heuristic
            return true;
        }
        return false;
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.CloseSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void HelpModalBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ShowHelpModal = false;
        }
    }

    private void ToggleQuickAdd()
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ToggleQuickAddCommand.Execute(null);

            if (vm.IsQuickAddVisible)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (vm.CurrentView == ViewType.Week)
                    {
                        WeekView.FocusQuickAddBox();
                    }
                    else
                    {
                        InlineQuickAddTextBox.Focus();
                        InlineQuickAddTextBox.SelectAll();
                    }
                });
            }
        }
    }

    private void InlineQuickAddTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // 자동완성 팝업이 열려있을 때 키보드 네비게이션
        if (vm.ShowHashTagSuggestions)
        {
            switch (e.Key)
            {
                case Key.Down:
                    vm.SelectNextSuggestion();
                    e.Handled = true;
                    return;
                case Key.Up:
                    vm.SelectPreviousSuggestion();
                    e.Handled = true;
                    return;
                case Key.Tab:
                case Key.Enter:
                    if (vm.ApplySelectedSuggestion())
                    {
                        // 커서를 맨 끝으로 이동
                        InlineQuickAddTextBox.CaretIndex = InlineQuickAddTextBox.Text.Length;
                        e.Handled = true;
                    }
                    return;
                case Key.Escape:
                    vm.HideHashTagSuggestions();
                    e.Handled = true;
                    return;
            }
        }
    }

    private void InlineQuickAddTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // 자동완성 팝업이 열려있을 때는 PreviewKeyDown에서 처리
        if (vm.ShowHashTagSuggestions && (e.Key == Key.Enter || e.Key == Key.Tab))
            return;

        if (e.Key == Key.Enter)
        {
            if (!string.IsNullOrWhiteSpace(vm.QuickAddText))
            {
                vm.AddTaskCommand.Execute(null);
                // 연속 입력을 위해 입력창은 유지하고 포커스도 유지
                Dispatcher.BeginInvoke(() =>
                {
                    InlineQuickAddTextBox.Focus();
                });
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseQuickAddWithAnimation();
            e.Handled = true;
        }
    }

    private void HashTagSuggestionsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm &&
            HashTagSuggestionsList.SelectedItem is HashTagSuggestion suggestion)
        {
            vm.ApplyHashTagSuggestion(suggestion.Name);
            // 포커스를 다시 입력창으로
            Dispatcher.BeginInvoke(() =>
            {
                InlineQuickAddTextBox.Focus();
                InlineQuickAddTextBox.CaretIndex = InlineQuickAddTextBox.Text.Length;
            });
        }
    }

    private void CloseQuickAddWithAnimation()
    {
        if (DataContext is MainViewModel vm && vm.IsQuickAddVisible)
        {
            vm.ToggleQuickAddCommand.Execute(null);
        }
    }
}
